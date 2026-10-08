using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks;
using Emerge.Checks.Editor;
using Emerge.Props;
using Emerge.Props.Editor;
using Emerge.Battle.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Battle.Editor
{
    public static class PointBattleSetup
    {
        public const string ScenePath = "Assets/Scenes/PointBattleDemo.unity";
        private const string Root = "Assets/Resources/Battle/PointEncounters";
        [MenuItem("Tools/点数战斗/安装配置与测试场景")]
        public static void Install()
        {
            PointSystemSetup.Install(); PropAssetFactory.EnsureFolder(Root);
            string path = "Assets/Resources/" + PointBattleDefinition.ResourcePath + ".asset";
            var rules = AssetDatabase.LoadAssetAtPath<PointBattleDefinition>(path);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<PointBattleDefinition>();
                rules.enemies = new List<PointEnemyProfile> {
                    Profile("point.sentinel", "巡守影", 36, new PointEnemyMove { name = "试探", amount = 4 }, new PointEnemyMove { name = "举盾", effect = PointEnemyEffect.Shield, amount = 5 }, new PointEnemyMove { name = "重击", amount = 7 }),
                    Profile("point.miasma", "蚀雾", 24, new PointEnemyMove { name = "侵蚀", effect = PointEnemyEffect.Corrosion, amount = 2 }, new PointEnemyMove { name = "雾袭", amount = 4 }),
                    Profile("point.stalker", "潜行影", 24, new PointEnemyMove { name = "破绽", effect = PointEnemyEffect.Vulnerability, amount = 25 }, new PointEnemyMove { name = "二连击", amount = 3, hits = 2 }),
                    Profile("point.guard", "守卫影", 20, new PointEnemyMove { name = "掩护", effect = PointEnemyEffect.Shield, amount = 4 }, new PointEnemyMove { name = "猛攻", amount = 4 })
                };
                AssetDatabase.CreateAsset(rules, path);
            }
            InstallLegacyProfiles(rules);
            AddStoryItem("IdentityCard", "day1.identity-card", "身份认证卡");
            AddStoryItem("EnvironmentFile", "day1.environment-file", "环境数据档案");
            AddStoryItem("InspectionTools", "day1.tools", "检修工具");
            AddStoryItem("LivingSupplies", "day1.supplies", "生活物资");
            AddStoryItem("StoryAnchor", "story.item-03", "现实稳定锚");
            AddStoryItem("StoryCoins", "story.item-01", "铜钱");
            foreach (string folder in new[] { "Assets/Story/InitialDraft/Items", "Assets/Day1/Config" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:PropDefinition", new[] { folder }))
                {
                    var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (prop != null && (folder.EndsWith("/Items", StringComparison.Ordinal) || prop.HasAction(PropActions.Pickup)))
                        AddStoryItem("Story_" + guid, prop.InventoryKey, prop.DisplayName);
                }
            }
            var enemies = rules.enemies.Select(p => Enemy(p)).ToArray();
            var encounters = new[] {
                Encounter("point.single", "单敌 · 识破重击", new[] { enemies[0] }),
                Encounter("point.dual", "双敌 · 优先击破", new[] { enemies[1], enemies[2] }),
                Encounter("point.crowd", "四敌 · 群体压力", new[] { enemies[3], enemies[1], enemies[2], enemies[3] })
            };
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                // Use an additive scene so installation never replaces the user's open scene.
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var root = new GameObject("点数战斗测试"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.tag = "MainCamera"; camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = new Color(.06f, .1f, .14f);
                var player = new GameObject("主角"); player.transform.SetParent(root.transform); player.tag = "Player";
                var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true;
                player.AddComponent<PixelPrototype.PlayerMovement>(); player.AddComponent<PropGameState>(); player.AddComponent<PlayerInteractor>();
                player.AddComponent<CheckActorState>(); player.AddComponent<PointLoadoutUI>(); player.AddComponent<Emerge.GameFlow.SaveIdentity>().AssignId("point-battle-demo-player");
                root.AddComponent<PointConfigurationZone>().warehouse = true; root.AddComponent<PointRefitStation>();
                var demo = root.AddComponent<PointBattleDemo>(); demo.player = player; demo.encounters = encounters;
                EditorSceneManager.SaveScene(scene, ScenePath); EditorSceneManager.CloseScene(scene, true);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
        }
        private static void InstallLegacyProfiles(PointBattleDefinition rules)
        {
            var profiles = new[] {
                Profile("E01", "海蚀影", 24, new PointEnemyMove { name = "试探", amount = 4 }, new PointEnemyMove { name = "蓄力", effect = PointEnemyEffect.Strength, amount = 1 }, new PointEnemyMove { name = "冲撞", amount = 7 }),
                Profile("E02", "护壳体", 28, new PointEnemyMove { name = "结壳", effect = PointEnemyEffect.Shield, amount = 5 }, new PointEnemyMove { name = "撞击", amount = 6 }),
                Profile("E03", "扰识体", 20, new PointEnemyMove { name = "蚀念", effect = PointEnemyEffect.Corrosion, amount = 2 }, new PointEnemyMove { name = "扰识", effect = PointEnemyEffect.Vulnerability, amount = 25 }, new PointEnemyMove { name = "暗袭", amount = 4 }),
                Profile("B01", "死海残响", 60, new PointEnemyMove { name = "双重回响", amount = 5, hits = 2 }, new PointEnemyMove { name = "聚潮", effect = PointEnemyEffect.Strength, amount = 2 }, new PointEnemyMove { name = "海啸", amount = 10 }, new PointEnemyMove { name = "回流", effect = PointEnemyEffect.Heal, amount = 4 })
            };
            var catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            foreach (var profile in profiles)
            {
                if (rules.enemies.Any(e => e.id == profile.id)) continue;
                var original = catalog?.Enemy(profile.id);
                if (original != null) { profile.name = original.displayName; profile.portrait = original.battlePortrait; profile.color = original.color; }
                rules.enemies.Add(profile); EditorUtility.SetDirty(rules);
            }
        }
        [MenuItem("Tools/点数战斗/接入原五个战斗测试场景")]
        public static void MigrateLegacyScenes()
        {
            Install(); var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (string path in BattleDemoSetup.ScenePaths)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存场景：" + path);
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var context = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BattleDemoContext>(true)).Single();
                    var actor = context.player.gameObject;
                    if (actor.GetComponent<CheckActorState>() == null) actor.AddComponent<CheckActorState>();
                    if (actor.GetComponent<PointLoadoutUI>() == null) actor.AddComponent<PointLoadoutUI>();
                    if (actor.GetComponent<PointBattleController>() == null) actor.AddComponent<PointBattleController>();
                    var zone = context.GetComponent<PointConfigurationZone>() ?? context.gameObject.AddComponent<PointConfigurationZone>();
                    zone.warehouse = true; zone.size = new Vector2(30, 20);
                    var station = context.GetComponent<PointRefitStation>() ?? context.gameObject.AddComponent<PointRefitStation>(); station.range = 20;
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
        }
        private static PointEnemyProfile Profile(string id, string name, int hp, params PointEnemyMove[] moves)
            => new PointEnemyProfile { id = id, name = name, health = hp, pattern = moves.ToList() };
        private static BattleEnemyDefinition Enemy(PointEnemyProfile profile)
        {
            string path = Root + "/" + profile.id + ".asset";
            var enemy = AssetDatabase.LoadAssetAtPath<BattleEnemyDefinition>(path);
            if (enemy == null) { enemy = ScriptableObject.CreateInstance<BattleEnemyDefinition>(); enemy.id = profile.id; enemy.displayName = profile.name; enemy.color = profile.color; AssetDatabase.CreateAsset(enemy, path); }
            return enemy;
        }
        private static BattleEncounterDefinition Encounter(string id, string name, BattleEnemyDefinition[] enemies)
        {
            string path = Root + "/" + id + ".asset"; var encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>(path);
            if (encounter == null) { encounter = ScriptableObject.CreateInstance<BattleEncounterDefinition>(); encounter.id = id; encounter.displayName = name;
                encounter.enemies = enemies.Select(e => new BattleEnemySlot { enemy = e }).ToArray(); encounter.useFixedSeed = true; encounter.fixedSeed = 91073;
                AssetDatabase.CreateAsset(encounter, path); }
            return encounter;
        }
        private static void AddStoryItem(string file, string key, string name)
        {
            var catalog = Resources.Load<PointItemCatalog>(PointItemCatalog.ResourcePath); if (catalog.Find(key) != null) return;
            string path = PointSystemSetup.Root + "/" + file + ".asset";
            var item = ScriptableObject.CreateInstance<PointItemDefinition>(); item.randomizeOnAcquire = true;
            item.data = new PointItemData { key = key, displayName = name, description = "获得时随机生成效果；配置至任一六亲提供 1 成长点。" };
            AssetDatabase.CreateAsset(item, path); catalog.items.Add(item); EditorUtility.SetDirty(catalog);
        }
        [MenuItem("Tools/点数战斗/打开测试场景")]
        public static void OpenDemo() { Install(); EditorSceneManager.OpenScene(ScenePath); }
    }
}
