using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Props;
using Emerge.Props.Editor;
using PixelPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.GameFlow.Editor
{
    public static class GameFlowInstaller
    {
        public const string PrefabPath = "Assets/Resources/GameFlow/GameFlow.prefab";
        public const string GameScenePath = "Assets/Scenes/PropsDemo.unity";
        private static readonly string[] ScenePaths = { "Assets/Scenes/PixelRoom.unity", GameScenePath };

        [MenuItem("Tools/菜单与存档/安装或更新")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[GameFlow] 请停止 Play 模式后安装菜单与存档。");
                return;
            }
            SaveOpenSceneChanges();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EnsurePrefab();
                foreach (string path in ScenePaths)
                {
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                        throw new InvalidOperationException("无法安装菜单与存档，场景不存在：" + path);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    InstallPlayer(scene);
                    if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                        throw new InvalidOperationException("无法保存已安装存档身份的场景：" + path);
                }
                EnsureBuildScenes();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            Debug.Log("[GameFlow] 已移植主菜单、暂停设置和存档。进入 Play 后显示主菜单，背包与道具交付进度会随存档恢复。");
        }

        // Called by the scene generator as well as explicit installation.
        public static void InstallPlayer(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            var players = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerMovement>(true)).ToArray();
            bool changed = false, needsInteraction = false;
            foreach (var player in players)
            {
                if (player.GetComponent<PropGameState>() == null)
                {
                    Undo.AddComponent<PropGameState>(player.gameObject);
                    changed = true;
                }
                var interactor = player.GetComponent<PlayerInteractor>();
                if (interactor == null || interactor.propLibrary == null) needsInteraction = true;
            }
            if (needsInteraction)
            {
                PropBootstrap.InstallPlayer(scene);
                changed = true;
            }

            var usedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var player in players)
            {
                var identity = player.GetComponent<SaveIdentity>();
                if (identity == null)
                {
                    identity = Undo.AddComponent<SaveIdentity>(player.gameObject);
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(identity.Id) || !usedIds.Add(identity.Id))
                {
                    Undo.RecordObject(identity, "分配角色存档 ID");
                    identity.AssignId(Guid.NewGuid().ToString("N"));
                    usedIds.Add(identity.Id);
                    EditorUtility.SetDirty(identity);
                    changed = true;
                }
                if (PrefabUtility.IsPartOfPrefabInstance(identity))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(identity);
            }
            if (changed) EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void SaveOpenSceneChanges()
        {
            // Preserve every loaded scene before switching the editor's scene setup.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !scene.isDirty) continue;
                if (string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("当前有未命名场景，请先保存场景，再安装菜单与存档。");
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("无法保留当前场景改动：" + scene.path);
            }
        }

        private static void EnsurePrefab()
        {
            EnsureFolder("Assets/Resources/GameFlow");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    if (ConfigureRoot(prefabRoot)) PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
                return;
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Game Flow");
                SceneManager.MoveGameObjectToScene(root, preview);
                ConfigureRoot(root);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("无法创建菜单预制体：" + PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static bool ConfigureRoot(GameObject root)
        {
            bool changed = false;
            if (root.name != "Game Flow") { root.name = "Game Flow"; changed = true; }
            var controller = root.GetComponent<GameSessionController>();
            if (controller == null) { controller = root.AddComponent<GameSessionController>(); changed = true; }
            if (root.GetComponent<GameMenuUI>() == null) { root.AddComponent<GameMenuUI>(); changed = true; }
            if (controller.gameScenePath != GameScenePath)
            {
                controller.gameScenePath = GameScenePath;
                EditorUtility.SetDirty(controller);
                changed = true;
            }
            return changed;
        }

        private static void EnsureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = false;
            foreach (string path in ScenePaths)
            {
                var existing = scenes.Find(scene => scene.path == path);
                if (existing == null) { scenes.Add(new EditorBuildSettingsScene(path, true)); changed = true; }
                else if (!existing.enabled) { existing.enabled = true; changed = true; }
            }
            if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
