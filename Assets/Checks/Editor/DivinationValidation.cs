using System;
using System.Globalization;
using System.IO;
using Emerge.Checks.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Checks.Editor
{
    // Local commands are limited to validation and capture of this project's divination demo.
    [InitializeOnLoad]
    public static class DivinationValidation
    {
        private const string Prefix = "Emerge.Checks.DivinationValidation.";
        private const string PendingKey = Prefix + "Pending";
        private static double nextPoll;
        private static bool finishing;
        [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class CommandResult
        { public string id, verb, state, message, completedUtc; public bool success; }

        static DivinationValidation()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
            EditorApplication.update += Update;
        }

        [MenuItem("Tools/检定系统/验证起卦排盘")]
        public static void Run() => Begin("manual-" + Guid.NewGuid().ToString("N"), "validate");
        [MenuItem("Tools/检定系统/预览起卦排盘界面")]
        public static void Preview() => Begin("manual-" + Guid.NewGuid().ToString("N"), "preview");

        private static void Begin(string id, string verb)
        {
            if (SessionState.GetBool(PendingKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请停止 Play 模式后运行起卦排盘验证。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请等待脚本与资源导入完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存改动；请保存后运行起卦排盘验证。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CheckDemoSetup.ScenePath) == null)
                throw new InvalidOperationException("请先创建观测站测试情境。");
            SessionState.SetString(Prefix + "Setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "Paused", EditorApplication.isPaused);
            SessionState.SetBool(Prefix + "Suppression", SessionState.GetBool("Light.GameFlow.SuppressForValidation", false));
            SessionState.SetFloat(Prefix + "TimeScale", Time.timeScale);
            SessionState.SetString(Prefix + "Id", id); SessionState.SetString(Prefix + "Verb", verb);
            SessionState.SetString(Prefix + "StartedUtc", DateTime.UtcNow.ToString("O"));
            SessionState.SetString(Prefix + "Started", EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetBool(Prefix + "Passed", false); SessionState.SetBool(Prefix + "Finished", false);
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            Application.runInBackground = true; EditorApplication.isPaused = false;
            try
            {
                EditorSceneManager.OpenScene(CheckDemoSetup.ScenePath, OpenSceneMode.Single);
                SessionState.SetBool(PendingKey, true); finishing = false;
                WriteCommandResult(id, verb, "running", true, verb == "preview" ? "正在捕获起卦与排盘界面。" : "正在验证起卦、排盘与真实检定流程。");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception)
            { SessionState.SetBool(PendingKey, true); Restore(); throw; }
        }

        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Application.runInBackground = true; EditorApplication.isPaused = false; Time.timeScale = 1;
                DivinationSelfTest.Completed -= Complete;
                DivinationSelfTest.PreviewCompleted -= Complete;
                var runner = new GameObject("起卦排盘验证（临时）").AddComponent<DivinationSelfTest>();
                if (SessionState.GetString(Prefix + "Verb", "validate") == "preview")
                {
                    DivinationSelfTest.PreviewCompleted += Complete;
                    var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
                    runner.BeginPreview();
                }
                else { DivinationSelfTest.Completed += Complete; runner.RunChecks(); }
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                // Restore after other play-state listeners have completed their cleanup.
                finishing = true;
                EditorApplication.delayCall += Restore;
            }
        }

        private static void Complete(bool passed)
        {
            SessionState.SetBool(Prefix + "Passed", passed); SessionState.SetBool(Prefix + "Finished", true);
            DivinationSelfTest.Completed -= Complete; DivinationSelfTest.PreviewCompleted -= Complete;
            EditorApplication.ExitPlaymode();
        }

        private static void Update()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                // Also restore from the update loop if a deferred callback is skipped during domain reload.
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                    (finishing || SessionState.GetBool(Prefix + "Finished", false)))
                { Restore(); return; }
                EditorApplication.QueuePlayerLoopUpdate();
                double started;
                if (!finishing && double.TryParse(SessionState.GetString(Prefix + "Started", ""), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out started) && EditorApplication.timeSinceStartup - started > 90)
                {
                    SessionState.SetBool(Prefix + "Passed", false); finishing = true;
                    Debug.LogError("DIVINATION_VALIDATION_TIMEOUT");
                    if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else Restore();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7;
            string commandPath = ProjectPath("Validation/divination-command.json");
            if (!File.Exists(commandPath)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(Prefix + "LastCommand", "")) return;
            try
            {
                var previous = JsonUtility.FromJson<CommandResult>(File.ReadAllText(ProjectPath("Validation/divination-command-result.json")));
                if (previous != null && previous.id == command.id && previous.state != "running")
                { SessionState.SetString(Prefix + "LastCommand", command.id); return; }
            }
            catch (Exception) { }
            SessionState.SetString(Prefix + "LastCommand", command.id);
            if (command.verb != "validate" && command.verb != "preview")
            { WriteCommandResult(command.id, command.verb, "rejected", false, "只支持 validate 和 preview。"); return; }
            try { Begin(command.id, command.verb); }
            catch (Exception exception)
            { WriteCommandResult(command.id, command.verb, "rejected", false, exception.Message); Debug.LogWarning("[DivinationValidation] " + exception.Message); }
        }

        private static void Restore()
        {
            if (!SessionState.GetBool(PendingKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool passed = SessionState.GetBool(Prefix + "Passed", false) && SessionState.GetBool(Prefix + "Finished", false);
            string error = "";
            try
            {
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Prefix + "Setup", ""));
                if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            }
            catch (Exception exception) { passed = false; error = exception.Message; }
            finally
            {
                DivinationSelfTest.Completed -= Complete; DivinationSelfTest.PreviewCompleted -= Complete;
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                EditorApplication.isPaused = SessionState.GetBool(Prefix + "Paused", false);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(Prefix + "Suppression", false));
                Time.timeScale = SessionState.GetFloat(Prefix + "TimeScale", 1);
                SessionState.SetBool(PendingKey, false); finishing = false;
            }
            WriteCommandResult(SessionState.GetString(Prefix + "Id", ""), SessionState.GetString(Prefix + "Verb", "validate"),
                "complete", passed, !string.IsNullOrEmpty(error) ? error : passed ? "起卦排盘验证或预览完成，原场景已恢复。" : "验证或预览未完成；请查看报告。");
        }

        private static void WriteCommandResult(string id, string verb, string state, bool success, string message)
        {
            string path = ProjectPath("Validation/divination-command-result.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new CommandResult { id = id, verb = verb, state = state, success = success,
                message = message, completedUtc = DateTime.UtcNow.ToString("O") }, true));
        }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path));
    }
}
