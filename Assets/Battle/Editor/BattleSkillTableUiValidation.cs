using System;
using System.IO;
using Emerge.Battle.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Battle.Editor
{
    [InitializeOnLoad]
    public static class BattleSkillTableUiValidation
    {
        private const string Prefix = "Light.BattleSkillUi.";
        private const string Suppression = "Light.GameFlow.SuppressForValidation";
        private static double nextPoll;
        [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result { public string id, state, message; public bool success; }
        static BattleSkillTableUiValidation()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += PlayChanged;
        }
        [MenuItem("Tools/战斗系统/验证并捕获新版技能界面")]
        public static void Run() { Begin("manual-" + Guid.NewGuid().ToString("N")); }
        private static void Begin(string id)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                SessionState.GetBool(Prefix + "Pending", false) || SessionState.GetBool("Emerge.Checks.BattleValidation.Pending", false))
                throw new InvalidOperationException("请停止 Play 模式，等待导入与其他战斗验证完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存当前场景。");
            SessionState.SetString(Prefix + "Setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetString(Prefix + "Id", id);
            SessionState.SetBool(Prefix + "PreviousSuppression", SessionState.GetBool(Suppression, false));
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "Paused", EditorApplication.isPaused);
            SessionState.SetFloat(Prefix + "TimeScale", Time.timeScale);
            SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Prefix + "Finished", false); SessionState.SetBool(Prefix + "Passed", false);
            EditorSceneManager.OpenScene(BattleDemoSetup.ScenePaths[0]);
            SessionState.SetBool(Prefix + "Pending", true); SessionState.SetBool(Suppression, true);
            Application.runInBackground = true; EditorApplication.isPaused = false;
            WriteResult(id, "running", true, "正在真实 Play 模式验证技能列表、增强、状态与吉凶动画并截图。");
            EditorApplication.EnterPlaymode();
        }
        private static void PlayChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Pending", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                Time.timeScale = 1; Application.runInBackground = true;
                var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
                BattleSkillTableUiPreview.Completed -= Complete; BattleSkillTableUiPreview.Completed += Complete;
                new GameObject("新版技能 UI 验证（临时）").AddComponent<BattleSkillTableUiPreview>().BeginPreview();
            }
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Restore;
        }
        private static void Complete(bool passed)
        {
            SessionState.SetBool(Prefix + "Passed", passed); SessionState.SetBool(Prefix + "Finished", true);
            BattleSkillTableUiPreview.Completed -= Complete;
            EditorApplication.ExitPlaymode();
        }
        private static void Restore()
        {
            if (!SessionState.GetBool(Prefix + "Pending", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool passed = SessionState.GetBool(Prefix + "Passed", false) && SessionState.GetBool(Prefix + "Finished", false);
            string message = passed ? "新版技能 UI 验证与截图完成，已恢复原场景。" : "新版技能 UI 验证未通过，请查看 battle-skill-ui-results.json。";
            try
            {
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Prefix + "Setup", ""));
                if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            }
            catch (Exception exception) { passed = false; message = exception.Message; }
            finally
            {
                BattleSkillTableUiPreview.Completed -= Complete;
                SessionState.SetBool(Suppression, SessionState.GetBool(Prefix + "PreviousSuppression", false));
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                EditorApplication.isPaused = SessionState.GetBool(Prefix + "Paused", false);
                Time.timeScale = SessionState.GetFloat(Prefix + "TimeScale", 1);
                SessionState.SetBool(Prefix + "Pending", false);
            }
            WriteResult(SessionState.GetString(Prefix + "Id", ""), "complete", passed, message);
        }
        private static void Poll()
        {
            if (SessionState.GetBool(Prefix + "Pending", false))
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (!SessionState.GetBool(Prefix + "Finished", false) && EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0) > 150)
                { SessionState.SetBool(Prefix + "Finished", true); if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else Restore(); }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7;
            const string path = "Validation/battle-skill-ui-command.json";
            if (!File.Exists(path)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrEmpty(command.id) || command.id == SessionState.GetString(Prefix + "LastId", "")) return;
            SessionState.SetString(Prefix + "LastId", command.id);
            try
            {
                if (command.verb != "preview") throw new InvalidOperationException("新版技能 UI 验证仅支持 preview。");
                Begin(command.id);
            }
            catch (Exception exception) { WriteResult(command.id, "rejected", false, exception.Message); }
        }
        private static void WriteResult(string id, string state, bool success, string message)
        {
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/battle-skill-ui-command-result.json", JsonUtility.ToJson(new Result { id = id, state = state, success = success, message = message }, true));
        }
    }
}
