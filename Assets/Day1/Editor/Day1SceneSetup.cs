using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Battle;
using Emerge.Characters;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.PixelMap;
using Emerge.Props;
using Emerge.Props.Editor;
using PixelPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Day1.Editor
{
    [InitializeOnLoad]
    public static class Day1SceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Day1ResearchStation.unity";
        private const string MapLibraryPath = "Assets/Day1/Maps/Day1MapLibrary.asset";
        private static double nextPoll;
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result { public string id, state, message; public bool success; }
        static Day1SceneSetup() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            const string path = "Validation/day1-setup-command.json";
            if (!File.Exists(path)) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
            if (command == null || string.IsNullOrWhiteSpace(command.id) || SessionState.GetString("Light.Day1.SetupCommand", "") == command.id) return;
            SessionState.SetString("Light.Day1.SetupCommand", command.id);
            var result = new Result { id = command.id, state = "complete" };
            try
            {
                if (command.verb == "install") Install();
                else if (command.verb == "preview") Preview();
                else if (command.verb == "open") Open();
                else throw new InvalidOperationException("Unsupported Day1 command: " + command.verb);
                result.success = true; result.message = "Day1 操作完成。";
            }
            catch (Exception e) { result.state = "failed"; result.message = e.ToString(); Debug.LogException(e); }
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/day1-setup-result.json", JsonUtility.ToJson(result, true));
        }
        [MenuItem("Tools/剧情/Day1/创建可玩占位场景")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式。");
            var characters = CharacterCatalog.LoadDefault();
            var props = Day1ContentBuilder.Build(characters);
            var story = Day1StoryBuilder.Ensure(characters);
            PropAssetFactory.EnsureFolder("Assets/Day1/Maps");
            var library = AssetDatabase.LoadAssetAtPath<MapBlockLibrary>(MapLibraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<MapBlockLibrary>(); AssetDatabase.CreateAsset(library, MapLibraryPath); }
            library.SetPropLibrary(props); EditorUtility.SetDirty(library);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Scene previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var map = Day1MapBuilder.Build(scene, library);
                    var markers = map.transform.Find("Markers");
                    var runtime = new GameObject("Day1 剧情流程");
                    var flow = runtime.AddComponent<Day1FlowController>();
                    flow.story = story; flow.characters = characters;
                    flow.bufferExit = markers.Find("BufferExit"); flow.seaGate = markers.Find("SeaGate");
                    flow.shadowStart = markers.Find("ShadowStart"); flow.shadowEnd = markers.Find("ShadowEnd");
                    flow.assembly = markers.Find("ControlAssembly");
                    flow.actor = BuildPlayer(characters.Find(CharacterIds.HuanYujian), props, markers.Find("Spawn").position);
                    var placements = new GameObject("Day1 角色与交互点").transform; placements.SetParent(map.transform);
                    foreach (string key in Day1ContentBuilder.Keys)
                    {
                        var definition = Day1ContentBuilder.Load(key);
                        var go = new GameObject(key + " · " + definition.DisplayName); go.transform.SetParent(placements);
                        go.transform.position = markers.Find(key).position;
                        go.AddComponent<PropInstance>().Configure(definition);
                        var point = go.AddComponent<Day1Interaction>(); point.key = key; point.flow = flow; point.homePosition = go.transform.position;
                        flow.interactions.Add(point);
                    }
                    flow.admissionGate = Gate("身份核验门（通过测试后开放）", flow.bufferExit.position);
                    flow.outdoorGate = Gate("站外作业门（领取物资后开放）", new Vector3(-9, 4.5f));
                    var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
                    var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5.6f;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .12f, .17f);
                    camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                    cameraObject.AddComponent<AudioListener>(); cameraObject.AddComponent<CameraFollow>().Configure(flow.actor.transform);
                    cameraObject.transform.position = flow.actor.transform.position + Vector3.back * 10;
                    if (File.Exists(Day1ControlRoomSetup.ArtPath)) Day1ControlRoomSetup.ApplyToScene(scene);
                    PropAssetFactory.EnsureFolder("Assets/Scenes");
                    if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Day1 scene could not be saved.");
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                }
            }
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var existing = scenes.Find(s => s.path == ScenePath);
            if (existing == null) scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); else existing.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray(); AssetDatabase.SaveAssets();
            Debug.Log("DAY1_INSTALL_OK: " + ScenePath + "; existing scene and edited content are preserved.");
        }
        private static PlayerInteractor BuildPlayer(CharacterDefinition character, PropLibrary props, Vector3 spawn)
        {
            var go = new GameObject("桓玉鉴（玩家 / 已有角色占位）"); go.transform.position = spawn;
            go.AddComponent<SaveIdentity>().AssignId("day1-player-huan-yujian");
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.65f, .38f); collider.offset = new Vector2(0, .16f);
            collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
            var movement = go.AddComponent<PlayerMovement>();
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = character.mapSprite;
            var visual = go.AddComponent<PlayerVisual>(); var serialized = new SerializedObject(visual);
            serialized.FindProperty("movement").objectReferenceValue = movement;
            foreach (string key in new[] { "idle", "stepA", "stepB" }) serialized.FindProperty(key).objectReferenceValue = character.mapSprite;
            serialized.ApplyModifiedPropertiesWithoutUndo(); go.AddComponent<FeetYSort>();
            go.AddComponent<PropGameState>(); var interactor = go.AddComponent<PlayerInteractor>(); interactor.propLibrary = props;
            go.AddComponent<CheckActorState>();
            var battle = go.AddComponent<BattleController>(); battle.catalog = Day1ContentBuilder.LoadBattleCatalog(); battle.character = character;
            return interactor;
        }
        private static GameObject Gate(string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/Crate.png");
            renderer.color = new Color(.55f, .68f, .77f); go.transform.localScale = new Vector3(1, 2, 1);
            renderer.sortingOrder = 1000; go.AddComponent<BoxCollider2D>().size = Vector2.one;
            return go;
        }
        [MenuItem("Tools/剧情/Day1/打开可玩场景")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式。");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("已有场景存在未保存改动，已保留；请保存后打开 Day1。");
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Tools/剧情/Day1/导出地图预览")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("预览需要编辑模式且场景无未保存改动。");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            RenderTexture target = null; Texture2D output = null;
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                var camera = Camera.main; camera.GetComponent<CameraFollow>().enabled = false;
                camera.transform.position = new Vector3(-3, -1.5f, -10); camera.orthographicSize = 17;
                // Match runtime's initial gathered party in the overview.
                var flow = UnityEngine.Object.FindObjectOfType<Day1FlowController>();
                var positions = new[] { new Vector3(0, 2), new Vector3(3, 2), new Vector3(-1, 1), new Vector3(4, 1), new Vector3(0, -1), new Vector3(3, -1) };
                int i = 0;
                foreach (var point in flow.interactions.Where(p => p.Prop.Character != null)) point.transform.position = positions[i++];
                foreach (var prop in UnityEngine.Object.FindObjectsOfType<PropInstance>()) prop.ApplyDefinition();
                target = new RenderTexture(1280, 1024, 24); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; output = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                output.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); output.Apply();
                Directory.CreateDirectory("Validation"); File.WriteAllBytes("Validation/day1-map-preview.png", output.EncodeToPNG());
                camera.targetTexture = null;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                // These are unsaved preview-only modifications to the newly loaded Day1 scene.
                EditorSceneManager.CloseScene(scene, true); EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
