using System;
using System.Collections.Generic;
using Emerge.Props;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Checks.Editor
{
    public static class PointSystemSetup
    {
        public const string Root = "Assets/Checks/PointSystem";
        public const string ScenePath = "Assets/Scenes/PointSystemDemo.unity";
        [MenuItem("Tools/点数系统/安装示例道具")]
        public static void Install()
        {
            PropAssetFactory.EnsureFolder(Root);
            PropAssetFactory.EnsureFolder("Assets/Resources/Checks");
            var catalog = AssetDatabase.LoadAssetAtPath<PointItemCatalog>("Assets/Resources/Checks/PointItemCatalog.asset");
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<PointItemCatalog>(); AssetDatabase.CreateAsset(catalog, "Assets/Resources/Checks/PointItemCatalog.asset"); }
            AddIfMissing(catalog, "Sword", new PointItemData { key = "points.sword", displayName = "剑",
                description = "成长 +1；当前点数小于局外点数时，将当前点数变为局外点数。",
                modifiers = new List<PointModifierRule> { new PointModifierRule { comparison = PointComparison.Less,
                    operation = PointOperation.Set, operand = new PointOperand { source = PointSource.Outside } } } });
            AddIfMissing(catalog, "Talisman", new PointItemData { key = "points.talisman", displayName = "符咒",
                description = "成长 +1；执行到此道具时，当前点数 +2。",
                modifiers = new List<PointModifierRule> { new PointModifierRule { operation = PointOperation.Add,
                    operand = new PointOperand { source = PointSource.Constant, constant = 2 } } } });
            AddIfMissing(catalog, "Knot", new PointItemData { key = "points.knot", displayName = "平安结（点数占位）",
                description = "成长 +1；没有额外点数变动。" });
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }
        private static void AddIfMissing(PointItemCatalog catalog, string filename, PointItemData data)
        {
            if (catalog.Find(data.key) != null) return;
            string path = Root + "/" + filename + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<PointItemDefinition>(path);
            if (item == null) { item = ScriptableObject.CreateInstance<PointItemDefinition>(); item.data = data; AssetDatabase.CreateAsset(item, path); }
            catalog.items.Add(item);
        }
        [MenuItem("Tools/点数系统/创建并打开测试场景")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式。");
            Install();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) { EditorSceneManager.OpenScene(ScenePath); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.orthographic = true; camera.orthographicSize = 5; camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(.08f, .12f, .18f); camera.clearFlags = CameraClearFlags.SolidColor;
            var player = new GameObject("点数测试主角"); player.tag = "Player";
            var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true;
            player.AddComponent<PixelPrototype.PlayerMovement>(); player.AddComponent<PropGameState>();
            player.AddComponent<PlayerInteractor>(); player.AddComponent<CheckActorState>();
            var demo = new GameObject("点数测试场景说明").AddComponent<PointSystemDemo>();
            demo.player = player;
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
        }
    }
}
