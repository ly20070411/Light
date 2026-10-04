using System;
using System.Collections.Generic;
using System.IO;
using Emerge.Checks.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Checks.Editor
{
    [InitializeOnLoad]
    public static class CheckValidation
    {
        private const string PendingKey = "Emerge.Checks.Validation.Pending";
        private const string SetupKey = "Emerge.Checks.Validation.SceneSetup";
        private const string LastCommandKey = "Emerge.Checks.Validation.LastCommand";
        private const string ActiveCommandKey = "Emerge.Checks.Validation.ActiveCommand";
        private const string SuppressionKey = "Emerge.Checks.Validation.OldSuppression";
        private const string BackgroundKey = "Emerge.Checks.Validation.OldBackground";
        private static double nextCommandPoll, started;
        private static bool receivedCompletion;

        [Serializable] private sealed class SetupSnapshot { public SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id; public string verb; }
        [Serializable] private sealed class CommandResult
        { public string id, verb, state, message, completedUtc; public bool success; }

        static CheckValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Update;
        }

        [MenuItem("Tools/检定系统/运行流程自检")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先停止 Play 模式，再运行检定自检。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CheckDemoSetup.ScenePath) == null)
                throw new InvalidOperationException("请先创建观测站测试情境。");
            // Never save or discard unrelated unsaved user work on behalf of the test.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存改动；请保存后运行自检，避免测试切换场景时丢失改动。");
            SessionState.SetString(SetupKey, JsonUtility.ToJson(new SetupSnapshot { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(SuppressionKey, SessionState.GetBool("Light.GameFlow.SuppressForValidation", false));
            SessionState.SetBool(BackgroundKey, Application.runInBackground);
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            EditorSceneManager.OpenScene(CheckDemoSetup.ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(new CheckSelfTest.Report
            { unityVersion = Application.unityVersion, scene = CheckDemoSetup.ScenePath, passed = false }, true));
            SessionState.SetBool(PendingKey, true);
            Application.runInBackground = true;
            started = EditorApplication.timeSinceStartup;
            receivedCompletion = false;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                receivedCompletion = false;
                Application.runInBackground = true;
                Time.timeScale = 1;
                CheckSelfTest.Completed -= Complete;
                CheckSelfTest.Completed += Complete;
                new GameObject("检定流程自动验证（临时）").AddComponent<CheckSelfTest>().RunChecks();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                CheckSelfTest.Completed -= Complete;
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(SuppressionKey, false));
                Application.runInBackground = SessionState.GetBool(BackgroundKey, false);
                Time.timeScale = 1;
                string savedSetup = SessionState.GetString(SetupKey, "");
                if (!string.IsNullOrEmpty(savedSetup))
                {
                    var setup = JsonUtility.FromJson<SetupSnapshot>(savedSetup);
                    if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
                }
                string command = SessionState.GetString(ActiveCommandKey, "");
                if (!string.IsNullOrEmpty(command))
                {
                    bool passed = false;
                    try { passed = JsonUtility.FromJson<CheckSelfTest.Report>(File.ReadAllText(ReportPath)).passed; } catch (Exception) { }
                    WriteCommandResult(new Command { id = command, verb = "validate" }, passed, "complete", passed ? "检定流程自检通过。" : "检定流程自检失败；请检查报告。");
                    SessionState.SetString(ActiveCommandKey, "");
                }
            }
        }

        private static void Complete(bool passed)
        {
            receivedCompletion = true;
            CheckSelfTest.Completed -= Complete;
            EditorApplication.ExitPlaymode();
        }

        private static void Update()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.isPlaying && !receivedCompletion && EditorApplication.timeSinceStartup - started > 60)
                {
                    var report = new CheckSelfTest.Report { unityVersion = Application.unityVersion, scene = CheckDemoSetup.ScenePath,
                        completedUtc = DateTime.UtcNow.ToString("O"), passed = false };
                    try { report = JsonUtility.FromJson<CheckSelfTest.Report>(File.ReadAllText(ReportPath)); } catch (Exception) { }
                    report.passed = false; report.completedUtc = DateTime.UtcNow.ToString("O");
                    report.checks.Add(new CheckSelfTest.Check { name = "Runtime checks complete before timeout", passed = false, observed = "Timed out after 60 seconds" });
                    Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)); File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                    Debug.LogError("CHECK_VALIDATION_TIMEOUT"); receivedCompletion = true; EditorApplication.ExitPlaymode();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextCommandPoll || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextCommandPoll = EditorApplication.timeSinceStartup + .7;
            string path = ProjectPath("Validation/check-command.json");
            if (!File.Exists(path)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(LastCommandKey, "")) return;
            // A completed command is not replayed merely because the Editor was restarted.
            try
            {
                var previous = JsonUtility.FromJson<CommandResult>(File.ReadAllText(ProjectPath("Validation/check-command-result.json")));
                if (previous != null && previous.id == command.id && previous.state != "running")
                { SessionState.SetString(LastCommandKey, command.id); return; }
            }
            catch (Exception) { }
            // This local command file accepts two bounded editor operations only.
            SessionState.SetString(LastCommandKey, command.id);
            try
            {
                if (command.verb == "install")
                { CheckDemoSetup.Install(); WriteCommandResult(command, true, "complete", "观测站测试情境已创建。"); }
                else if (command.verb == "validate")
                {
                    SessionState.SetString(ActiveCommandKey, command.id);
                    Run(); WriteCommandResult(command, true, "running", "检定流程自检正在运行。");
                }
                else WriteCommandResult(command, false, "rejected", "只支持 install 和 validate。");
            }
            catch (Exception exception)
            {
                SessionState.SetString(ActiveCommandKey, "");
                WriteCommandResult(command, false, "failed", exception.Message);
                Debug.LogWarning("[Checks] " + exception.Message);
            }
        }

        private static void WriteCommandResult(Command command, bool success, string state, string message)
        {
            string path = ProjectPath("Validation/check-command-result.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new CommandResult { id = command.id, verb = command.verb,
                state = state, success = success, message = message, completedUtc = DateTime.UtcNow.ToString("O") }, true));
        }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path));
        private static string ReportPath => ProjectPath("Validation/check-results.json");
    }
}
