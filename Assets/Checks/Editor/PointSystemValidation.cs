using System;
using System.IO;
using Emerge.Checks.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Checks.Editor
{
    [InitializeOnLoad]
    public static class PointSystemValidation
    {
        private const string Pending = "Light.PointSystemValidation.Pending";
        [Serializable] private sealed class Setup { public UnityEditor.SceneManagement.SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id, verb; }
        private static double nextPoll;
        static PointSystemValidation() { EditorApplication.playModeStateChanged += PlayChanged; EditorApplication.update += Update; }
        private static void Update()
        {
            if (SessionState.GetBool(Pending, false)) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/point-system-command.json"));
            if (!File.Exists(path)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(Pending + ".Last", "")) return;
            // Consume local validation requests once, including across editor restarts.
            try { File.Delete(path); } catch (IOException) { return; }
            SessionState.SetString(Pending + ".Last", command.id);
            try
            {
                if (command.verb == "all") RunAllBatch();
                else if (command.verb == "core") InstallAndValidate();
                else if (command.verb == "demo") PointSystemSetup.CreateDemo();
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        [MenuItem("Tools/点数系统/运行功能验证")]
        public static void Run()
        {
            InstallAndValidate();
        }
        private static bool InstallAndValidate()
        {
            PointSystemSetup.Install();
            bool points = PointSystemTest.RunChecks();
            bool battle = Emerge.Battle.Tests.BattleSkillTableTest.RunChecks();
            return points && battle;
        }
        // -batchmode -executeMethod Emerge.Checks.Editor.PointSystemValidation.RunAllBatch
        public static void RunAllBatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式。");
            foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
                if (scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty) throw new InvalidOperationException("当前场景有未保存改动，请先保存。");
            SessionState.SetString(Pending + ".Setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            bool passed;
            try { passed = InstallAndValidate(); }
            catch (Exception exception) { Debug.LogException(exception); passed = false; }
            if (!passed) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            SessionState.SetBool(Pending + ".Background", Application.runInBackground);
            Application.runInBackground = true;
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene("Assets/Scenes/PropsDemo.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void PlayChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // The report survives domain reload through its JSON artifact.
                PointSystemTest.Results = JsonUtility.FromJson<PointSystemTest.Report>(System.IO.File.ReadAllText(
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Validation/point-system-results.json"))));
                PointSystemPlayTest.Completed -= Complete;
                PointSystemPlayTest.Completed += Complete;
                new GameObject("点数系统实际流程测试").AddComponent<PointSystemPlayTest>();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Pending, false);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", false);
                Application.runInBackground = SessionState.GetBool(Pending + ".Background", false);
                if (!Application.isBatchMode) EditorApplication.delayCall += () =>
                {
                    var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Pending + ".Setup", ""));
                    if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
                };
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(Pending + ".Passed", false) ? 0 : 1);
            }
        }
        public static void Complete(bool passed)
        {
            SessionState.SetBool(Pending + ".Passed", passed);
            EditorApplication.ExitPlaymode();
        }
    }
}
