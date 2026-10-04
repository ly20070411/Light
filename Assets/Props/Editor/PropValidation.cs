using System.IO;
using Emerge.PixelMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Props.Editor
{
    [InitializeOnLoad]
    public static class PropValidation
    {
        private const string PendingKey = "Emerge.Props.Validation.Pending";
        private const string ReportKey = "Emerge.Props.Validation.Report";
        private const string SceneKey = "Emerge.Props.Validation.Scene";
        private static double started;
        static PropValidation() { EditorApplication.playModeStateChanged += PlayStateChanged; }
        [MenuItem("Tools/道具系统/运行验证")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            SessionState.SetString(SceneKey, original);
            var scene = EditorSceneManager.OpenScene(PropBootstrap.DemoScenePath);
            var report = new PropSelfTest.Report();
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(PropAssetFactory.LibraryPath);
            var mapLibrary = AssetDatabase.LoadAssetAtPath<MapBlockLibrary>("Assets/PixelMap/Library/DefaultBlockLibrary.asset");
            Add(report, "Map library links to prop library", library != null && mapLibrary != null && mapLibrary.PropLibrary == library, "Serialized asset reference");
            var ids = new System.Collections.Generic.HashSet<string>(); bool valid = library != null && library.Props.Count >= 3;
            if (library != null) foreach (var d in library.Props) valid &= d != null && !string.IsNullOrEmpty(d.Id) && ids.Add(d.Id);
            Add(report, "Definitions have distinct stable asset IDs", valid, "Default library definitions");
            var player = Object.FindObjectOfType<PixelPrototype.PlayerMovement>();
            Add(report, "Demo player has interactor and inventory state", player != null && player.GetComponent<PlayerInteractor>() != null && player.GetComponent<PropGameState>() != null, "Playable input integration");
            var root = new GameObject("Editor placement test");
            var definition = library.Props[1];
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            var instance = PropPlacementService.Place(definition, root.transform, new Vector3(40, 40, 0), 0, MapPlacementMode.Grid, Vector2.one);
            string id = instance.InstanceId;
            Add(report, "Grid placement stores coordinates and definition", instance.GridCoordinate == new Vector2Int(40, 40) && instance.PlacementMode == MapPlacementMode.Grid && instance.Definition == definition, "Same placement service used by map brush");
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group); Undo.PerformUndo();
            Add(report, "Placement supports Undo", root.GetComponentsInChildren<PropInstance>(true).Length == 0, "Undo removes prop hierarchy");
            Undo.PerformRedo(); instance = root.GetComponentInChildren<PropInstance>();
            Add(report, "Placement supports Redo with same instance ID", instance != null && instance.InstanceId == id, "Stable save identity after redo");
            PropSceneSync.SyncNow();
            var duplicate = Object.Instantiate(instance.gameObject, root.transform).GetComponent<PropInstance>();
            PropSceneSync.SyncNow();
            Add(report, "Duplicated instances get independent save IDs", duplicate.InstanceId != instance.InstanceId && instance.InstanceId == id, "Original identity preserved");
            Object.DestroyImmediate(duplicate.gameObject);
            Add(report, "Non-solid props can be erased without colliders", PropPlacementService.Erase(root.transform, new Vector3(40, 40.2f, 0)), "Sprite bounds picking");
            Undo.PerformUndo();
            Add(report, "Erase supports Undo", root.GetComponentInChildren<PropInstance>() != null, "Restores erased prop");
            Object.DestroyImmediate(root);
            // Discard only temporary test placements. The scene was saved before testing.
            EditorSceneManager.OpenScene(PropBootstrap.DemoScenePath);
            SessionState.SetString(ReportKey, JsonUtility.ToJson(report));
            WriteReport(report);
            SessionState.SetBool(PendingKey, true);
            var gameView = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) EditorWindow.GetWindow(gameView).Show();
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            EditorApplication.EnterPlaymode();
        }
        private static void Add(PropSelfTest.Report report, string name, bool passed, string observed)
        { report.checks.Add(new PropSelfTest.Check { name = name, passed = passed, observed = observed }); }
        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                PropSelfTest.InitialReportJson = SessionState.GetString(ReportKey, "");
                PropSelfTest.Completed += Complete;
                new GameObject("道具系统自动验证").AddComponent<PropSelfTest>();
                EditorApplication.update += Timeout;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", false);
                SessionState.SetBool(PendingKey, false); EditorApplication.update -= Timeout;
                // Show the playable demo and its editor after successful validation.
                PropEditorWindow.Open(); Emerge.PixelMap.Editor.PixelMapEditorWindow.OpenPropsTab();
            }
        }
        private static void Complete(bool passed)
        { PropSelfTest.Completed -= Complete; EditorApplication.update -= Timeout; EditorApplication.ExitPlaymode(); }
        private static void Timeout()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started < 60) return;
            EditorApplication.update -= Timeout;
            var report = JsonUtility.FromJson<PropSelfTest.Report>(SessionState.GetString(ReportKey, "{}"));
            Add(report, "Play Mode validation completed", false, "Timed out after 60 seconds");
            report.completedUtc = System.DateTime.UtcNow.ToString("O"); WriteReport(report);
            Debug.LogError("PROPS_VALIDATION_TIMEOUT"); EditorApplication.ExitPlaymode();
        }
        private static void WriteReport(PropSelfTest.Report report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/props-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
