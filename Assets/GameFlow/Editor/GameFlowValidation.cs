using System;
using System.IO;
using System.Linq;
using Emerge.GameFlow.Tests;
using Emerge.Props;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.GameFlow.Editor
{
    [InitializeOnLoad]
    public static class GameFlowValidation
    {
        private const string PendingKey = "Light.GameFlow.Validation.Pending";
        private const string ReportKey = "Light.GameFlow.Validation.Report";
        private const string StartedKey = "Light.GameFlow.Validation.Started";
        public const string DemoScenePath = "Assets/Scenes/PropsDemo.unity";
        private static double started;
        static GameFlowValidation() { EditorApplication.playModeStateChanged += PlayStateChanged; }

        [MenuItem("Tools/菜单与存档/运行移植验证")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty) continue;
                if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("请先保存未命名场景，再运行菜单与存档验证。");
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("无法保存场景：" + scene.path);
            }
            EditorSceneManager.OpenScene(DemoScenePath);
            var report = new GameFlowSmokeChecks.Report { unityVersion = Application.unityVersion, scene = DemoScenePath };
            Add(report, "Gameplay demo is included in build scenes", EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == DemoScenePath), DemoScenePath);
            var player = UnityEngine.Object.FindObjectOfType<PixelPrototype.PlayerMovement>();
            Add(report, "Demo player provides the prop backpack and interactor", player != null && player.GetComponent<PlayerInteractor>() != null && player.GetComponent<PropGameState>() != null,
                "PlayerMovement / PlayerInteractor / PropGameState");
            var props = UnityEngine.Object.FindObjectsOfType<PropInstance>(true);
            Add(report, "Demo contains the configured fruit and handover guide", props.Any(prop => prop.Definition != null && prop.Definition.InventoryKey == "fruit" && prop.Definition.HasAction(PropActions.Pickup)) &&
                props.Any(prop => prop.Definition != null && prop.Definition.itemHandover != null && prop.Definition.itemHandover.enabled && prop.Definition.itemHandover.itemKey == "fruit"),
                "Tests use actual editable Fruit.asset and Guide.asset");
            Add(report, "Placed prop identities are stable and unique", props.All(prop => !string.IsNullOrWhiteSpace(prop.InstanceId)) && props.Select(prop => prop.InstanceId).Distinct().Count() == props.Length,
                "Includes invisible picked-item restore data");
            SessionState.SetString(ReportKey, JsonUtility.ToJson(report));
            WriteReport(report);
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", false);
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(StartedKey, false);
            Application.runInBackground = true;
            EditorApplication.EnterPlaymode();
        }

        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                SessionState.SetBool(StartedKey, true);
                Application.runInBackground = true;
                GameFlowSmokeChecks.Completed += Complete;
                var runner = new GameObject("菜单与存档移植自动验证").AddComponent<GameFlowSmokeChecks>();
                runner.Initialize(SessionState.GetString(ReportKey, ""));
                EditorApplication.update += PumpAndTimeout;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PendingKey, false);
                SessionState.SetBool(StartedKey, false);
                EditorApplication.update -= PumpAndTimeout;
                GameFlowSmokeChecks.Completed -= Complete;
                Time.timeScale = 1f;
            }
        }

        private static void Complete(bool passed)
        {
            GameFlowSmokeChecks.Completed -= Complete;
            EditorApplication.update -= PumpAndTimeout;
            EditorApplication.ExitPlaymode();
        }

        private static void PumpAndTimeout()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started < 60d) return;
            EditorApplication.update -= PumpAndTimeout;
            GameFlowSmokeChecks.Completed -= Complete;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/menu-save-results.json"));
            GameFlowSmokeChecks.Report report;
            try { report = JsonUtility.FromJson<GameFlowSmokeChecks.Report>(File.ReadAllText(path)); }
            catch { report = JsonUtility.FromJson<GameFlowSmokeChecks.Report>(SessionState.GetString(ReportKey, "{}")); }
            Add(report, "Menu/save runtime verification finished", false, "Timed out after 60 seconds");
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.passed = false;
            WriteReport(report);
            Debug.LogError("MENU_SAVE_VALIDATION_TIMEOUT");
            EditorApplication.ExitPlaymode();
        }

        private static void Add(GameFlowSmokeChecks.Report report, string name, bool passed, string observed)
        { report.checks.Add(new GameFlowSmokeChecks.CheckResult { name = name, passed = passed, observed = observed }); }
        private static void WriteReport(GameFlowSmokeChecks.Report report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/menu-save-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
