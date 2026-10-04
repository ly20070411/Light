using System;
using System.Collections.Generic;
using Emerge.Checks.Demo;
using Emerge.GameFlow;
using Emerge.Props;
using Emerge.Props.Editor;
using PixelPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Checks.Editor
{
    public static class CheckDemoSetup
    {
        public const string ScenePath = CheckDemoContext.ScenePath;
        public const string EventPath = "Assets/Checks/Demo/ObservationGate.asset";
        public const string TerminalPath = "Assets/Checks/Demo/ObservationTerminal.asset";
        public const string RepairKitPath = "Assets/Checks/Demo/RepairKit.asset";
        public const string LibraryPath = "Assets/Checks/Demo/CheckDemoLibrary.asset";

        [MenuItem("Tools/检定系统/创建观测站测试情境")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先停止 Play 模式，再创建检定情境。");
            PropAssetFactory.EnsureFolder("Assets/Checks/Demo");
            PropAssetFactory.EnsureFolder("Assets/Scenes");
            var definition = EnsureEvent();
            var terminal = EnsureTerminal(definition);
            var kit = EnsureRepairKit();
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PropLibrary>();
                library.Add(terminal); library.Add(kit);
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            if (library.Add(terminal) | library.Add(kit)) EditorUtility.SetDirty(library);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene(library, terminal, kit);
            var buildScenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var entry = buildScenes.Find(item => item.path == ScenePath);
            if (entry == null) { buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = buildScenes.ToArray(); }
            else if (!entry.enabled) { entry.enabled = true; EditorBuildSettings.scenes = buildScenes.ToArray(); }
            AssetDatabase.SaveAssets();
            Debug.Log("CHECK_DEMO_INSTALL_OK: " + ScenePath + "；已有情境及其他场景保持原有内容。");
        }

        [MenuItem("Tools/检定系统/打开观测站测试情境")]
        public static void OpenDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Install();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存改动；请保存后打开检定情境。");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static CheckEventDefinition EnsureEvent()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CheckEventDefinition>(EventPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<CheckEventDefinition>();
            asset.eventId = "observation-gate";
            asset.title = "失效的隔离门";
            asset.speaker = "观测站终端";
            asset.intro = "隔离门仍在锁定。终端能接收通行申请，也可以修复控制器，或强行切断门锁。你准备怎样行动？";
            asset.completedText = "隔离门事件已经处理。沿大门或刚发现的侧路继续前进。";
            asset.revealDifficultyBeforeChoice = false;
            asset.options = new List<CheckOptionDefinition>
            {
                new CheckOptionDefinition
                {
                    id = "communicate", label = "提交通行申请", behavior = CheckBehavior.Sibling, targetValue = 8,
                    success = new CheckOutcomeDefinition { text = "身份说明通过验证。隔离门缓缓打开。", continuation = "你进入观测站，寻找失联的研究员。", grantedFlags = new[] { "check-gate-open" } },
                    failure = new CheckOutcomeDefinition { text = "申请未获通过。终端提供了墙边检修通道的位置。", continuation = "你改走检修侧路，继续调查失联的研究员。", grantedFlags = new[] { "check-side-route" } }
                },
                new CheckOptionDefinition
                {
                    id = "repair", label = "修复门控线路", behavior = CheckBehavior.Offspring, targetValue = 9,
                    requiredItemKey = "repair-kit", requiredItemAmount = 1, consumeRequiredItem = true,
                    success = new CheckOutcomeDefinition { text = "你用维修包替换了烧毁的线路，门控恢复正常。", continuation = "隔离门打开。你进入观测站。", grantedFlags = new[] { "check-gate-open" } },
                    failure = new CheckOutcomeDefinition { text = "更换线路后，旧控制器仍无法启动。拆开面板时，你找到了一条检修通道。", continuation = "维修包已经使用。你改走检修侧路，继续调查。", grantedFlags = new[] { "check-side-route" } }
                },
                new CheckOptionDefinition
                {
                    id = "force", label = "强行切断门锁", behavior = CheckBehavior.Wealth, targetValue = 12,
                    success = new CheckOutcomeDefinition { text = "门锁被切断，隔离门脱离了锁定。", continuation = "你沿大门进入观测站。", grantedFlags = new[] { "check-gate-open" } },
                    failure = new CheckOutcomeDefinition { text = "门锁弹出侵染液！你后退时发现墙边有一条检修侧路。", continuation = "侵染增加 15。你改走侧路，仍能继续调查。", grantedFlags = new[] { "check-side-route" }, contaminationDelta = 15 }
                }
            };
            if (!asset.Validate(out string error)) { UnityEngine.Object.DestroyImmediate(asset); throw new InvalidOperationException(error); }
            AssetDatabase.CreateAsset(asset, EventPath);
            return asset;
        }

        private static PropDefinition EnsureTerminal(CheckEventDefinition check)
        {
            var asset = AssetDatabase.LoadAssetAtPath<PropDefinition>(TerminalPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<PropDefinition>();
            asset.displayName = "观测站门控终端"; asset.category = "检定"; asset.sprite = Art("Table");
            asset.worldSize = new Vector2(2, 2); asset.isSolid = true; asset.colliderSize = new Vector2(1.4f, .45f);
            asset.actions = PropActions.Dialogue; asset.checkEvent = check;
            asset.interactionLabel = "处理隔离门"; asset.interactionRange = 2; asset.cooldown = 0;
            asset.description = "测试三种行动：申请通行、维修线路、强行切断门锁。";
            AssetDatabase.CreateAsset(asset, TerminalPath); return asset;
        }

        private static PropDefinition EnsureRepairKit()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PropDefinition>(RepairKitPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<PropDefinition>();
            asset.displayName = "维修包"; asset.category = "拾取"; asset.sprite = Art("Crate"); asset.tint = new Color(1, .82f, .5f);
            asset.worldSize = new Vector2(1, 1.5f); asset.actions = PropActions.Pickup; asset.interactionLabel = "拾取维修包";
            asset.inventoryKey = "repair-kit"; asset.pickupAmount = 1; asset.hideAfterPickup = true; asset.cooldown = 0;
            asset.description = "修复隔离门需要消耗一个维修包。";
            AssetDatabase.CreateAsset(asset, RepairKitPath); return asset;
        }

        private static void CreateScene(PropLibrary library, PropDefinition terminal, PropDefinition kit)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene demo = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(demo);
                var context = new GameObject("观测站情境与说明").AddComponent<CheckDemoContext>();
                var floors = new GameObject("观测站地板");
                Sprite floor = Art("Floor0");
                for (int y = -5; y <= 5; y++) for (int x = -7; x <= 7; x++)
                {
                    var tile = new GameObject("地板"); tile.transform.SetParent(floors.transform, false);
                    tile.transform.position = new Vector3(x, y, 0);
                    var renderer = tile.AddComponent<SpriteRenderer>(); renderer.sprite = floor; renderer.sortingOrder = -10000;
                }
                var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = new Vector3(0, -1, 0);
                var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var feet = player.AddComponent<CapsuleCollider2D>(); feet.direction = CapsuleDirection2D.Horizontal;
                feet.size = new Vector2(.65f, .38f); feet.offset = new Vector2(0, .16f);
                feet.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
                var movement = player.AddComponent<PlayerMovement>();
                var visual = new GameObject("玩家外观"); visual.transform.SetParent(player.transform, false);
                visual.AddComponent<SpriteRenderer>().sprite = Art("HeroIdle"); visual.AddComponent<FeetYSort>();
                var animation = visual.AddComponent<PlayerVisual>();
                SetObject(animation, "movement", movement); SetObject(animation, "idle", Art("HeroIdle"));
                SetObject(animation, "stepA", Art("HeroStepA")); SetObject(animation, "stepB", Art("HeroStepB"));
                player.AddComponent<PropGameState>(); player.AddComponent<PlayerInteractor>().propLibrary = library;
                player.AddComponent<SaveIdentity>().AssignId("check-demo-player");
                player.AddComponent<CheckActorState>().attributes = new ActorCheckAttributes
                { parent = 7, offspring = 9, officer = 7, wealth = 10, sibling = 8, self = 8 };
                Place(terminal, new Vector3(0, .6f, 0)); Place(kit, new Vector3(-2, -1, 0));
                var cameraRoot = new GameObject("Main Camera"); cameraRoot.tag = "MainCamera";
                var camera = cameraRoot.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5.6f;
                camera.backgroundColor = new Color(.07f, .11f, .16f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.allowHDR = false; camera.allowMSAA = false;
                cameraRoot.AddComponent<AudioListener>(); cameraRoot.AddComponent<CameraFollow>().Configure(player.transform);
                if (!EditorSceneManager.SaveScene(demo, ScenePath)) throw new InvalidOperationException("无法保存检定示例场景。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (demo.IsValid() && demo.isLoaded) EditorSceneManager.CloseScene(demo, true);
            }
        }

        private static void Place(PropDefinition definition, Vector3 position)
        { var root = new GameObject(definition.DisplayName); root.transform.position = position; root.AddComponent<PropInstance>().Configure(definition); }
        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/" + name + ".png");
        private static void SetObject(UnityEngine.Object target, string name, UnityEngine.Object value)
        { var serialized = new SerializedObject(target); serialized.FindProperty(name).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
