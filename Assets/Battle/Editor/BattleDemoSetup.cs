using System;
using System.Collections.Generic;
using Emerge.Battle.Demo;
using Emerge.GameFlow;
using Emerge.Props;
using Emerge.Props.Editor;
using PixelPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Battle.Editor
{
    public static class BattleDemoSetup
    {
        public const string CatalogPath = "Assets/Resources/Battle/BattleCatalog.asset";
        public static readonly string[] ScenePaths = { "Assets/Scenes/BattleSingleDemo.unity", "Assets/Scenes/BattleDualDemo.unity",
            "Assets/Scenes/BattleTripleDemo.unity", "Assets/Scenes/BattleDefenseDemo.unity", "Assets/Scenes/BattleBossDemo.unity" };
        private const string Config = "Assets/Battle/Config/";
        [MenuItem("Tools/战斗系统/打开测试场景/单敌教学")] public static void OpenSingle() => Open(0);
        [MenuItem("Tools/战斗系统/打开测试场景/双敌干扰")] public static void OpenDual() => Open(1);
        [MenuItem("Tools/战斗系统/打开测试场景/三敌遭遇")] public static void OpenTriple() => Open(2);
        [MenuItem("Tools/战斗系统/打开测试场景/防御遭遇")] public static void OpenDefense() => Open(3);
        [MenuItem("Tools/战斗系统/打开测试场景/首领")] public static void OpenBoss() => Open(4);
        private static void Open(int index)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePaths[index]) == null) Install();
            EditorSceneManager.OpenScene(ScenePaths[index]);
        }
        [MenuItem("Tools/战斗系统/安装基础配置与五个测试场景")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式");
            PropAssetFactory.EnsureFolder("Assets/Battle/Config"); PropAssetFactory.EnsureFolder("Assets/Resources/Battle"); PropAssetFactory.EnsureFolder("Assets/Scenes");
            var rules = Asset<BattleRules>("Rules", _ => { });
            var skills = new[]
            {
                Skill("ATK_BASIC", "普攻", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 3, 7, "单体伤害 7"),
                Skill("ATK_HEAVY", "破甲重击", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 8, 20, "伤害 20；卦势≥0 挂易伤", true),
                Skill("ATK_SWEEP", "荡秽横扫", BattleFamily.Officer, BattleTarget.AllEnemies, BattleEffect.Damage, 12, 14, "所有敌人伤害 14"),
                Skill("DEF_SHIELD", "护身符阵", BattleFamily.Parent, BattleTarget.Self, BattleEffect.Shield, 6, 18, "护盾 18，上限 40"),
                Skill("DEF_GUARD", "固守结界", BattleFamily.Parent, BattleTarget.Self, BattleEffect.Reduction, 2, .35f, "本轮减伤 35%，上限 50%"),
                Skill("HEAL_DIRECT", "归元", BattleFamily.Wealth, BattleTarget.Self, BattleEffect.Heal, 8, 24, "回复 24 HP"),
                Skill("HEAL_REGEN", "生息", BattleFamily.Wealth, BattleTarget.Self, BattleEffect.Regeneration, 8, 10, "下轮起回复 10 HP ×3轮"),
                Skill("MP_BREATH", "调息", BattleFamily.Offspring, BattleTarget.Self, BattleEffect.NextMana, 4, 8, "下轮额外恢复 8 MP"),
                Skill("MP_CLEANSE", "清心", BattleFamily.Offspring, BattleTarget.Self, BattleEffect.Cleanse, 4, 4, "解除削弱；下轮回蓝 4"),
                Skill("CTRL_SEAL", "封诀", BattleFamily.Sibling, BattleTarget.Enemy, BattleEffect.Silence, 6, 1, "卦势≥0 封技；否则削弱"),
                Skill("CTRL_BIND", "缚灵", BattleFamily.Sibling, BattleTarget.Enemy, BattleEffect.Bind, 8, 1, "卦势2 眩晕；否则削弱")
            };
            var e1 = Enemy("E01", "海蚀影", 80, 12, 4, false, new[] {
                ES("claw", "侵蚀抓击", EnemyEffect.Damage, 0, 12, 60), ES("heavy", "撕裂重击", EnemyEffect.Damage, 6, 22, 40, 1) });
            var e2 = Enemy("E02", "护壳体", 110, 12, 4, false, new[] {
                ES("shell-hit", "壳击", EnemyEffect.Damage, 0, 12, 60),
                new EnemySkillDefinition { id = "shell", displayName = "护壳", effect = EnemyEffect.Shield, mpCost = 6, power = 20, weight = 40, maximumHealthFraction = .85f, maximumShield = 8, cooldownRounds = 1 } });
            var e3 = Enemy("E03", "扰识体", 70, 12, 4, false, new[] {
                ES("shock", "扰识冲击", EnemyEffect.Damage, 0, 8, 30),
                new EnemySkillDefinition { id = "mind", displayName = "蚀念", effect = EnemyEffect.Weaken, mpCost = 6, power = 6, weakness = .2f, weight = 25, cooldownRounds = 1 },
                ES("ember", "秽火", EnemyEffect.Burn, 4, 5, 25, 1), ES("mark", "破绽印", EnemyEffect.Exposure, 4, 4, 20, 1) });
            var boss = Enemy("B01", "死海残响", 400, 20, 6, true, new[] {
                ES("tide", "海潮", EnemyEffect.Damage, 0, 18, 45), ES("break", "断界", EnemyEffect.Damage, 8, 30, 20, 1),
                new EnemySkillDefinition { id = "heal", displayName = "重聚", effect = EnemyEffect.Heal, mpCost = 8, power = 24, weight = 10, maximumHealthFraction = .35f, maximumUses = 2, cooldownRounds = 1 },
                ES("charge", "残响蓄势", EnemyEffect.Charge, 0, 0, 25, 1), ES("release", "残响迸发", EnemyEffect.ChargedDamage, 10, 42, 1) });
            var items = new[] { Item("ITEM_MED", "医疗包", BattleItemEffect.Heal, 35, "HP +35"), Item("ITEM_MP", "凝神剂", BattleItemEffect.Mana, 12, "MP +12"),
                Item("ITEM_CLEAN", "驱秽符", BattleItemEffect.Cleanse, 1, "清除负面"), Item("ITEM_BLAST", "散灵符", BattleItemEffect.DamageAll, 10, "群伤 10") };
            var encounters = new[] {
                Encounter("ENC01", "教学单敌", "单个海蚀影（100 HP）。熟悉连续行动、起卦和结束回合。", new[] { Slot(e1, 100) }),
                Encounter("ENC02", "攻击与干扰", "海蚀影与扰识体。测试目标切换、清心和封诀。", new[] { Slot(e1), Slot(e3) }),
                Encounter("ENC03", "三敌遭遇", "两只海蚀影与扰识体。测试群攻、敌方顺序和护盾。", new[] { Slot(e1), Slot(e1), Slot(e3) }),
                Encounter("ENC04", "防御遭遇", "护壳体与海蚀影。测试护壳、易伤和防御时点。", new[] { Slot(e2), Slot(e1) }),
                Encounter("ENC05", "首领 · 死海残响", "400 HP，潮棘反震实际伤害的 60%，致命攻击也触发。用减伤、护盾和治疗规划攻势；抵抗眩晕，低血量可重聚两次。", new[] { Slot(boss) }) };
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BattleCatalog>(); catalog.rules = rules; catalog.skills = skills;
                catalog.enemies = new[] { e1, e2, e3, boss }; catalog.items = items; catalog.encounters = encounters;
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
            BattlePlaceholderAssets.Install(catalog);
            var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < encounters.Length; i++)
            {
                var terminal = Asset<PropDefinition>("Terminal-" + encounters[i].id, p =>
                { p.displayName = encounters[i].displayName + "终端"; p.category = "战斗"; p.sprite = Art("Table"); p.worldSize = new Vector2(2, 2);
                    p.battleEncounter = encounters[i]; p.interactionLabel = "进入战斗"; p.interactionRange = 2; p.cooldown = 0; });
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePaths[i]) == null) CreateScene(ScenePaths[i], encounters[i], catalog, terminal);
                var entry = build.Find(s => s.path == ScenePaths[i]); if (entry == null) build.Add(new EditorBuildSettingsScene(ScenePaths[i], true)); else entry.enabled = true;
            }
            EditorBuildSettings.scenes = build.ToArray(); AssetDatabase.SaveAssets();
            Debug.Log("BATTLE_INSTALL_OK: 11技能、4敌人、4物品、5遭遇与5场景");
        }
        private static T Asset<T>(string name, Action<T> fill) where T : ScriptableObject
        { var path = Config + name + ".asset"; var a = AssetDatabase.LoadAssetAtPath<T>(path); if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>(); fill(a); AssetDatabase.CreateAsset(a, path); return a; }
        private static BattleSkillDefinition Skill(string id, string name, BattleFamily family, BattleTarget target, BattleEffect effect, int cost, float power, string description, bool vuln = false)
            => Asset<BattleSkillDefinition>(id, s => { s.id = id; s.displayName = name; s.family = family; s.target = target; s.effect = effect;
                s.mpCost = cost; s.power = power; s.description = description; s.appliesVulnerability = vuln;
                s.alwaysAvailable = id == "ATK_BASIC" || id == "DEF_GUARD" || id == "MP_CLEANSE";
                s.maximumUses = id == "ATK_BASIC" || id == "DEF_GUARD" ? 0 : id == "ATK_HEAVY" || id == "HEAL_DIRECT" || id == "CTRL_SEAL" ? 3 : id == "DEF_SHIELD" || id == "MP_BREATH" || id == "MP_CLEANSE" ? 4 : 2; });
        private static EnemySkillDefinition ES(string id, string name, EnemyEffect effect, int cost, int power, int weight, int cooldown = 0)
            => new EnemySkillDefinition { id = id, displayName = name, effect = effect, mpCost = cost, power = power, weight = weight, cooldownRounds = cooldown };
        private static BattleEnemyDefinition Enemy(string id, string name, int hp, int mp, int recovery, bool resists, EnemySkillDefinition[] skills)
            => Asset<BattleEnemyDefinition>(id, e => { e.id = id; e.displayName = name; e.maxHP = hp; e.maxMP = mp; e.roundMana = recovery; e.resistsStun = resists; e.retaliation = id == "B01" ? .6f : 0; e.skills = skills; });
        private static BattleItemDefinition Item(string id, string name, BattleItemEffect effect, int power, string description)
            => Asset<BattleItemDefinition>(id, e => { e.id = id; e.inventoryKey = id; e.displayName = name; e.effect = effect; e.power = power; e.description = description; });
        private static BattleEnemySlot Slot(BattleEnemyDefinition enemy, int hp = 0) => new BattleEnemySlot { enemy = enemy, healthOverride = hp };
        private static BattleEncounterDefinition Encounter(string id, string name, string description, BattleEnemySlot[] enemies)
            => Asset<BattleEncounterDefinition>(id, e => { e.id = id; e.displayName = name; e.description = description; e.enemies = enemies; e.swiftVictoryRounds = id == "ENC01" ? 3 : id == "ENC05" ? 8 : 5; });
        private static void CreateScene(string path, BattleEncounterDefinition encounter, BattleCatalog catalog, PropDefinition terminal)
        {
            Scene previous = SceneManager.GetActiveScene(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); var context = new GameObject("战斗测试情境").AddComponent<BattleDemoContext>(); context.encounter = encounter;
                var floorRoot = new GameObject("地板");
                for (int y = -5; y <= 5; y++) for (int x = -8; x <= 8; x++)
                { var tile = new GameObject("地板"); tile.transform.SetParent(floorRoot.transform); tile.transform.position = new Vector3(x, y);
                    var sr = tile.AddComponent<SpriteRenderer>(); sr.sprite = Art("Floor0"); sr.sortingOrder = -10000; }
                var player = new GameObject("Player"); player.tag = "Player"; player.transform.position = new Vector3(0, -1);
                var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var feet = player.AddComponent<CapsuleCollider2D>(); feet.direction = CapsuleDirection2D.Horizontal; feet.size = new Vector2(.65f, .38f); feet.offset = new Vector2(0, .16f);
                var movement = player.AddComponent<PlayerMovement>(); var visual = new GameObject("玩家外观"); visual.transform.SetParent(player.transform, false);
                visual.AddComponent<SpriteRenderer>().sprite = Art("HeroIdle"); visual.AddComponent<FeetYSort>(); var animation = visual.AddComponent<PlayerVisual>();
                SetObject(animation, "movement", movement); SetObject(animation, "idle", Art("HeroIdle")); SetObject(animation, "stepA", Art("HeroStepA")); SetObject(animation, "stepB", Art("HeroStepB"));
                player.AddComponent<PropGameState>(); player.AddComponent<PlayerInteractor>(); player.AddComponent<SaveIdentity>().AssignId("battle-demo-player");
                context.player = player.AddComponent<BattleController>(); context.player.catalog = catalog;
                var terminalGo = new GameObject("战斗终端（E交互）"); terminalGo.transform.position = new Vector3(0, 1); terminalGo.AddComponent<PropInstance>().Configure(terminal);
                var cameraRoot = new GameObject("Main Camera"); cameraRoot.tag = "MainCamera"; var camera = cameraRoot.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 5.6f; camera.backgroundColor = new Color(.05f, .09f, .14f); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .1f; camera.farClipPlane = 100; cameraRoot.AddComponent<AudioListener>(); cameraRoot.AddComponent<CameraFollow>().Configure(player.transform);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("场景保存失败");
            }
            finally { if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        private static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/" + name + ".png");
        private static void SetObject(UnityEngine.Object target, string name, UnityEngine.Object value)
        { var s = new SerializedObject(target); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
