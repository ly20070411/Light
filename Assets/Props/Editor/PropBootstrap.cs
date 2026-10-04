using Emerge.PixelMap;
using Emerge.PixelMap.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Props.Editor
{
    public static class PropBootstrap
    {
        public const string DemoScenePath = "Assets/Scenes/PropsDemo.unity";
        [MenuItem("Tools/道具系统/安装玩家交互与示例")]
        public static void Install()
        {
            var props = PropAssetFactory.EnsureDefaultLibrary();
            var mapLibrary = PixelMapAssetFactory.EnsureDefaultLibrary();
            if (mapLibrary.PropLibrary == null) { mapLibrary.SetPropLibrary(props); EditorUtility.SetDirty(mapLibrary); }
            var source = SceneManager.GetActiveScene();
            string sourcePath = source.path;
            if (string.IsNullOrEmpty(source.path))
            { Debug.LogWarning("请先保存当前场景，然后安装道具示例。"); return; }
            InstallPlayer(source);
            EditorSceneManager.SaveScene(source);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoScenePath) == null)
            {
                // Clone the user's saved scene and preserve the original and its placements.
                if (!EditorSceneManager.SaveScene(source, DemoScenePath, true)) return;
                var demo = EditorSceneManager.OpenScene(DemoScenePath);
                var root = new GameObject("交互道具示例").AddComponent<PixelMapRoot>(); root.Configure(mapLibrary);
                Vector3 spawn = new Vector3(-5, -3, 0);
                foreach (var player in Object.FindObjectsOfType<PlayerInteractor>())
                    if (player.gameObject.scene == demo) { spawn = player.transform.position; break; }
                Place("Terminal", spawn + new Vector3(2, 0, 0), root);
                Place("Supplies", spawn + new Vector3(0, 2, 0), root);
                Place("Guide", spawn + new Vector3(4, 0, 0), root);
                EditorSceneManager.SaveScene(demo);
                EditorSceneManager.OpenScene(sourcePath);
            }
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(item => item.path == DemoScenePath))
            { scenes.Add(new EditorBuildSettingsScene(DemoScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
            Debug.Log("PROPS_INSTALL_OK: player, definitions, map library and PropsDemo scene.");
        }
        public static void InstallPlayer(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var movement in root.GetComponentsInChildren<PixelPrototype.PlayerMovement>(true))
                {
                    var interactor = movement.GetComponent<PlayerInteractor>();
                    if (interactor == null) interactor = Undo.AddComponent<PlayerInteractor>(movement.gameObject);
                    if (interactor.propLibrary == null) interactor.propLibrary = PropAssetFactory.EnsureDefaultLibrary();
                    EditorSceneManager.MarkSceneDirty(scene);
                }
        }
        private static void Place(string file, Vector3 position, PixelMapRoot root)
        {
            var definition = AssetDatabase.LoadAssetAtPath<PropDefinition>(PropAssetFactory.DefinitionsPath + "/" + file + ".asset");
            PropPlacementService.Place(definition, root.transform, position, 0, MapPlacementMode.Grid, Vector2.one);
        }
        [MenuItem("Tools/道具系统/打开示例场景")]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DemoScenePath); PropEditorWindow.Open();
        }
    }
}
