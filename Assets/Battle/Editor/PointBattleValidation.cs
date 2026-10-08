using System;
using System.IO;
using Emerge.Battle.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Battle.Editor
{
    [InitializeOnLoad]
    public static class PointBattleValidation
    {
        private const string Pending = "Light.PointBattleValidation.Pending";
        [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id, verb; }
        private static double nextPoll;
        static PointBattleValidation() { EditorApplication.update += Update; EditorApplication.playModeStateChanged += PlayChanged; }
        private static void Update()
        {
            if (SessionState.GetBool(Pending, false)) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/point-battle-command.json"));
            if (!File.Exists(path)) return;
            Command command; try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(Pending + ".Last", "")) return;
            File.Delete(path); SessionState.SetString(Pending + ".Last", command.id);
            try
            {
                if (command.verb == "all") RunAll();
                else if (command.verb == "core") RunCore();
                else if (command.verb == "install") PointBattleSetup.Install();
                else if (command.verb == "migrate") PointBattleSetup.MigrateLegacyScenes();
                else if (command.verb == "legacy-demo") EditorSceneManager.OpenScene(BattleDemoSetup.ScenePaths[0]);
                else if (command.verb == "demo") PointBattleSetup.OpenDemo();
                else if (command.verb == "refresh") AssetDatabase.Refresh();
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        [MenuItem("Tools/点数战斗/运行功能与存读档验证")]
        public static void RunAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
                if (scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty) throw new InvalidOperationException("请先保存当前场景改动。");
            SessionState.SetString(Pending + ".Setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            if (!RunCore()) return;
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            SessionState.SetBool(Pending + ".Background", Application.runInBackground); Application.runInBackground = true;
            SessionState.SetBool(Pending, true); EditorSceneManager.OpenScene("Assets/Scenes/PropsDemo.unity"); EditorApplication.EnterPlaymode();
        }
        public static bool RunCore()
        {
            PointBattleSetup.Install();
            bool core = PointBattleTest.RunChecks();
            bool point = Emerge.Checks.Tests.PointSystemTest.RunChecks();
            bool legacy = BattleSkillTableTest.RunChecks();
            Debug.Log("POINT_BATTLE_CORE " + core + " POINT_CHAIN " + point + " LEGACY " + legacy);
            return core && point && legacy;
        }
        private static void PlayChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                PointBattleTest.Results = JsonUtility.FromJson<PointBattleTest.Report>(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/point-battle-results.json"))));
                PointBattlePlayTest.Completed -= Complete; PointBattlePlayTest.Completed += Complete;
                new GameObject("点数战斗实际流程测试").AddComponent<PointBattlePlayTest>();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false); SessionState.SetBool("Light.GameFlow.SuppressForValidation", false);
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                EditorApplication.delayCall += () =>
                {
                    var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Pending + ".Setup", ""));
                    if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
                };
            }
        }
        private static void Complete(bool passed) { SessionState.SetBool(Pending + ".Passed", passed); EditorApplication.ExitPlaymode(); }
    }
}
