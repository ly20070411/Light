using System;
using System.Globalization;
using System.IO;
using Emerge.Battle.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Battle.Editor
{
    // Only installation, validation and UI capture of the battle demos are exposed locally.
    [InitializeOnLoad]
    public static class BattleValidation
    {
        private const string Prefix = "Emerge.Checks.BattleValidation.";
        private const string PendingKey = Prefix + "Pending";
        private static double nextPoll;
        private static bool finishing;
        [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class CommandResult
        { public string id, verb, state, message, completedUtc; public bool success; }

        static BattleValidation()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
            EditorApplication.update += Update;
        }

        [MenuItem("Tools/战斗系统/运行战斗验证")]
        public static void Run() { if (UsesSkillTable()) BattleSkillTableValidation.Run(); else Begin("manual-" + Guid.NewGuid().ToString("N"), "validate"); }
        [MenuItem("Tools/战斗系统/捕获战斗界面")]
        public static void Preview() { if (UsesSkillTable()) BattleSkillTableUiValidation.Run(); else Begin("manual-" + Guid.NewGuid().ToString("N"), "preview"); }
        [MenuItem("Tools/战斗系统/运行流派与物品数值模拟")]
        public static void Balance() { if (UsesSkillTable()) BattleSkillTableValidation.Balance(); else Begin("manual-" + Guid.NewGuid().ToString("N"), "balance"); }
        private static bool UsesSkillTable() => Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath)?.rules?.balanceVersion == BattleSkillTableRules.BalanceVersion;

        private static void Begin(string id, string verb)
        {
            if (SessionState.GetBool(PendingKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请停止 Play 模式后运行战斗系统验证。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请等待脚本与资源导入完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存改动；请保存后运行战斗系统验证。");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BattleDemoSetup.ScenePaths[0]) == null)
                throw new InvalidOperationException("请先安装基础配置与五个测试场景。");
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
                EditorSceneManager.OpenScene(BattleDemoSetup.ScenePaths[0], OpenSceneMode.Single);
                SessionState.SetBool(PendingKey, true); finishing = false;
                WriteCommandResult(id, verb, "running", true, verb == "preview" ? "正在捕获战斗界面。" : "正在验证连续行动、战斗结算与真实界面流程。");
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
                BattleSelfTest.Completed -= Complete;
                BattleSelfTest.PreviewCompleted -= Complete;
                var runner = new GameObject("战斗系统验证（临时）").AddComponent<BattleSelfTest>();
                // WaitForEndOfFrame captures only advance while the Game view is selected.
                if (SessionState.GetString(Prefix + "Verb", "validate") != "balance")
                {
                    var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    if (gameViewType != null) EditorWindow.GetWindow(gameViewType).Focus();
                }
                if (SessionState.GetString(Prefix + "Verb", "validate") == "balance")
                {
                    BattleBalanceTest.Completed += Complete;
                    runner.gameObject.AddComponent<BattleBalanceTest>().Run();
                }
                else if (SessionState.GetString(Prefix + "Verb", "validate") == "preview")
                {
                    BattleSelfTest.PreviewCompleted += Complete;
                    var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                    if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
                    runner.BeginPreview();
                }
                else { BattleSelfTest.Completed += Complete; runner.RunChecks(); }
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
            BattleSelfTest.Completed -= Complete; BattleSelfTest.PreviewCompleted -= Complete;
            BattleBalanceTest.Completed -= Complete;
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
                    CultureInfo.InvariantCulture, out started) && EditorApplication.timeSinceStartup - started > (SessionState.GetString(Prefix + "Verb", "") == "balance" ? 600 : 150))
                {
                    SessionState.SetBool(Prefix + "Passed", false); finishing = true;
                    Debug.LogError("BATTLE_VALIDATION_TIMEOUT");
                    if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else Restore();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7;
            string commandPath = ProjectPath("Validation/battle-command.json");
            if (!File.Exists(commandPath)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(Prefix + "LastCommand", "")) return;
            try
            {
                var previous = JsonUtility.FromJson<CommandResult>(File.ReadAllText(ProjectPath("Validation/battle-command-result.json")));
                if (previous != null && previous.id == command.id && previous.state != "running")
                { SessionState.SetString(Prefix + "LastCommand", command.id); return; }
            }
            catch (Exception) { }
            SessionState.SetString(Prefix + "LastCommand", command.id);
            if (command.verb == "install") { try { BattleDemoSetup.Install(); WriteCommandResult(command.id, command.verb, "complete", true, "五个战斗场景已安装。"); } catch (Exception exception) { WriteCommandResult(command.id, command.verb, "rejected", false, exception.ToString()); } return; }
            if (command.verb != "validate" && command.verb != "preview" && command.verb != "balance")
            { WriteCommandResult(command.id, command.verb, "rejected", false, "只支持 install、validate、preview 和 balance。"); return; }
            try { Begin(command.id, command.verb); }
            catch (Exception exception)
            { WriteCommandResult(command.id, command.verb, "rejected", false, exception.Message); Debug.LogWarning("[BattleValidation] " + exception.Message); }
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
                BattleSelfTest.Completed -= Complete; BattleSelfTest.PreviewCompleted -= Complete;
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                EditorApplication.isPaused = SessionState.GetBool(Prefix + "Paused", false);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(Prefix + "Suppression", false));
                Time.timeScale = SessionState.GetFloat(Prefix + "TimeScale", 1);
                SessionState.SetBool(PendingKey, false); finishing = false;
            }
            WriteCommandResult(SessionState.GetString(Prefix + "Id", ""), SessionState.GetString(Prefix + "Verb", "validate"),
                "complete", passed, !string.IsNullOrEmpty(error) ? error : passed ? "战斗系统验证或预览完成，原场景已恢复。" : "验证或预览未完成；请查看报告。");
        }

        private static void WriteCommandResult(string id, string verb, string state, bool success, string message)
        {
            string path = ProjectPath("Validation/battle-command-result.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new CommandResult { id = id, verb = verb, state = state, success = success,
                message = message, completedUtc = DateTime.UtcNow.ToString("O") }, true));
        }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path));
    }
}
