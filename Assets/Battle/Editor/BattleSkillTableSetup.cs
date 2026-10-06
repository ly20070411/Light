using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Emerge.Battle.Editor
{
    [InitializeOnLoad]
    public static class BattleSkillTableSetup
    {
        public const string LegacyCatalogPath = "Assets/Resources/Battle/LegacyV05/BattleCatalog.asset";
        public const string ConfigPath = "Assets/Battle/Config/SkillTableV06";
        private const string CommandPath = "Validation/battle-skill-table-command.json";
        private const string ResultPath = "Validation/battle-skill-table-command-result.json";
        private const string LastIdKey = "Light.BattleSkillTable.LastId";
        private static double nextPoll;

        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result { public string id, state, message; public bool success; }
        private sealed class SkillRow
        {
            public BattleSkillKind kind;
            public string name, description;
            public BattleFamily family;
            public BattleTarget target;
            public BattleEffect effect;
            public int originalCost, uses;
            public float power;
            public bool enhanced;
        }

        static BattleSkillTableSetup() { EditorApplication.update += Poll; }

        [MenuItem("Tools/战斗系统/安装完整30条技能表")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请停止运行并等待脚本导入结束。");
            var catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDemoSetup.CatalogPath);
            if (catalog == null)
            {
                BattleDemoSetup.Install();
                catalog = AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleDemoSetup.CatalogPath);
            }
            if (catalog == null || catalog.rules == null) throw new InvalidOperationException("缺少可迁移的基础战斗库。");

            EnsureFolder(ConfigPath);
            BattleCatalog legacy = PreserveLegacy(catalog);
            var rules = CopyAsset(legacy.rules, ConfigPath + "/Rules.asset");
            rules.balanceVersion = BattleSkillTableRules.BalanceVersion;
            rules.maxHP = 100; rules.maxMP = 30; rules.roundMana = 18; rules.shieldCap = 100;
            rules.advancedOptions = BattleSkillTableRules.NormalOfferCount;
            EditorUtility.SetDirty(rules);

            EnsureFolder(ConfigPath + "/Enemies"); EnsureFolder(ConfigPath + "/Encounters");
            var enemies = legacy.enemies.Select(original =>
            {
                var enemy = CopyAsset(original, ConfigPath + "/Enemies/" + original.id + ".asset");
                enemy.defense = .15f;
                if (enemy.id == "B01") { enemy.maxHP = 340; enemy.retaliation = .30f; }
                EditorUtility.SetDirty(enemy); return enemy;
            }).ToArray();
            var encounters = legacy.encounters.Select(original =>
            {
                var encounter = CopyAsset(original, ConfigPath + "/Encounters/" + original.id + ".asset");
                encounter.enemies = original.enemies.Select(slot => new BattleEnemySlot {
                    enemy = enemies.First(enemy => enemy.id == slot.enemy.id), healthOverride = slot.healthOverride }).ToArray();
                if (encounter.id == "ENC05")
                {
                    encounter.swiftVictoryRounds = 5;
                    var boss = enemies.First(enemy => enemy.id == "B01");
                    encounter.description = boss.maxHP + " HP，潮棘反震实际 HP 伤害的 " + boss.retaliation.ToString("P0") +
                        "，致命攻击也触发。减伤、护盾和治疗可应对；首领抵抗眩晕，低血量可重聚两次。";
                }
                EditorUtility.SetDirty(encounter); return encounter;
            }).ToArray();

            var skills = new List<BattleSkillDefinition>();
            foreach (string id in new[] { "ATK_BASIC", "DEF_GUARD", "MP_CLEANSE" })
            {
                var original = legacy.Skill(id);
                if (original == null) throw new InvalidOperationException("旧库缺少常驻技能 " + id);
                var basic = CopyAsset(original, ConfigPath + "/" + id + ".asset");
                basic.kind = BattleSkillKind.Legacy; basic.alwaysAvailable = true;
                basic.enhancedAvailable = basic.isUltimate = basic.isPassive = false;
                if (id == "DEF_GUARD")
                {
                    basic.power = .40f;
                    basic.description = "本轮减伤 40%，上限 50%";
                }
                EditorUtility.SetDirty(basic); skills.Add(basic);
            }
            foreach (SkillRow row in Rows())
            {
                string id = "TABLE_" + ((int)row.kind).ToString("00") + "_" + row.kind.ToString().ToUpperInvariant();
                string path = ConfigPath + "/" + id + ".asset";
                var skill = AssetDatabase.LoadAssetAtPath<BattleSkillDefinition>(path);
                if (skill == null)
                {
                    skill = ScriptableObject.CreateInstance<BattleSkillDefinition>();
                    AssetDatabase.CreateAsset(skill, path);
                }
                skill.id = id; skill.displayName = row.name; skill.kind = row.kind; skill.family = row.family;
                skill.target = row.target; skill.effect = row.effect; skill.power = row.power; skill.maximumUses = row.uses;
                skill.isUltimate = (int)row.kind >= 25 && (int)row.kind <= 28;
                skill.isPassive = (int)row.kind >= 29;
                skill.mpCost = skill.isPassive ? 0 : Mathf.Clamp(BattleRules.Round(row.originalCost * BattleSkillTableRules.ManaCostScale), 1, 30);
                // Usage measurements showed these high-cost skills were rarely worth casting.
                switch (row.kind)
                {
                    case BattleSkillKind.MountainBarrier: skill.mpCost = 12; break;
                    case BattleSkillKind.SolarCurse: skill.mpCost = 8; break;
                    case BattleSkillKind.SixLineFateGu: skill.mpCost = 16; break;
                    case BattleSkillKind.RevolvingGu: skill.mpCost = 8; break;
                    case BattleSkillKind.DrawCalamity: skill.mpCost = 10; break;
                    case BattleSkillKind.AllLinesChange: skill.mpCost = 24; break;
                    case BattleSkillKind.FateVerdict: skill.mpCost = 20; break;
                    case BattleSkillKind.HeavenBurial: skill.mpCost = 20; break;
                    case BattleSkillKind.YinYangSlash: skill.mpCost = 18; break;
                }
                skill.alwaysAvailable = false; skill.enhancedAvailable = row.enhanced; skill.appliesVulnerability = false;
                skill.regenerationTicks = 2;
                string scale = skill.isPassive ? "独立被动，不占主动候选。" : skill.isUltimate ? "第3轮起进入独立终结候选；倍率固定1。" :
                    "所属五亲每点使技能倍率+10%。";
                string enhancement = row.enhanced ? "所属五亲达到3点时，本场第一次使用自动强化，费用相同；之后恢复普通效果。" : "";
                skill.description = row.description + "\n" + scale + enhancement;
                EditorUtility.SetDirty(skill); skills.Add(skill);
            }

            // Validate the proposed data before changing the live resource's references/GUID.
            var proposed = ScriptableObject.CreateInstance<BattleCatalog>();
            proposed.resourcePath = BattleCatalog.ResourcePath; proposed.rules = rules; proposed.skills = skills.ToArray();
            proposed.enemies = enemies; proposed.items = legacy.items; proposed.encounters = encounters;
            bool valid = proposed.Validate(out string error);
            UnityEngine.Object.DestroyImmediate(proposed);
            if (!valid) throw new InvalidOperationException(error);

            catalog.resourcePath = BattleCatalog.ResourcePath; catalog.rules = rules; catalog.skills = skills.ToArray();
            catalog.enemies = enemies; catalog.encounters = encounters;
            // Existing scenes retain the main catalog GUID and resolve their encounter references by stable ID.
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("BATTLE_SKILL_TABLE_INSTALL_OK: v0.6，28主动、2被动、3常驻；旧版库保存至 " + LegacyCatalogPath);
        }

        private static BattleCatalog PreserveLegacy(BattleCatalog source)
        {
            var legacy = AssetDatabase.LoadAssetAtPath<BattleCatalog>(LegacyCatalogPath);
            if (legacy != null) return legacy;
            if (source.rules.balanceVersion == BattleSkillTableRules.BalanceVersion)
                throw new InvalidOperationException("主库已为新版，但旧版副本缺失；停止迁移以免用新版伪造旧保存配置。");
            EnsureFolder("Assets/Resources/Battle/LegacyV05/Skills");
            legacy = CopyAsset(source, LegacyCatalogPath);
            // Old saves keep their original identity; the restore path explicitly chooses this archive by version.
            legacy.resourcePath = BattleCatalog.ResourcePath;
            legacy.rules = CopyAsset(source.rules, "Assets/Resources/Battle/LegacyV05/Rules.asset");
            legacy.skills = source.skills.Select(skill => CopyAsset(skill,
                "Assets/Resources/Battle/LegacyV05/Skills/" + skill.id + ".asset")).ToArray();
            EditorUtility.SetDirty(legacy); AssetDatabase.SaveAssets();
            return legacy;
        }

        private static T CopyAsset<T>(T source, string path) where T : ScriptableObject
        {
            var copy = AssetDatabase.LoadAssetAtPath<T>(path);
            if (copy != null) return copy;
            if (source == null || !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path))
                throw new InvalidOperationException("复制配置失败：" + path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string parent = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string folder = parent + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(parent, segments[index]);
                parent = folder;
            }
        }

        private static SkillRow Row(BattleSkillKind kind, string name, BattleFamily family, BattleTarget target,
            BattleEffect effect, int originalCost, float power, int uses, bool enhanced, string description)
        {
            return new SkillRow { kind = kind, name = name, family = family, target = target, effect = effect,
                originalCost = originalCost, power = power, uses = uses, enhanced = enhanced, description = description };
        }

        private static SkillRow[] Rows()
        {
            return new[]
            {
                Row(BattleSkillKind.HeavenRadiance, "乾爻·天行昭", BattleFamily.Wealth, BattleTarget.Enemy, BattleEffect.Damage, 20, 22, 3, true,
                    "普通：单体伤害22；获得4回合乾元增伤，初始+8%，之后每轮增加8%，最多+20%。强化：直接获得+20%增伤，本次基础伤害额外+12，无视目标15%防御。"),
                Row(BattleSkillKind.MetalSever, "兑爻·金泽断爻", BattleFamily.Wealth, BattleTarget.Enemy, BattleEffect.Damage, 17, 28, 4, true,
                    "普通：单体伤害28，目标防御降低12%，持续3回合。强化：破防提高到25%，并驱散目标一层增益。"),
                Row(BattleSkillKind.StealHexagram, "爻蛊·窃卦", BattleFamily.Wealth, BattleTarget.Enemy, BattleEffect.Damage, 20, 17, 2, true,
                    "普通：单体伤害17，窃取目标一层增益转给自己，持续3回合。强化：最多窃取两层；自身受到的伤害+10%，持续3回合。"),
                Row(BattleSkillKind.PrisonBreak, "噬嗑·破狱", BattleFamily.Wealth, BattleTarget.Enemy, BattleEffect.Damage, 16, 24, 3, false,
                    "单体伤害24；若目标正在防御或拥有护盾，基础伤害提高到34，并使其防御降低10%，持续3回合。"),
                Row(BattleSkillKind.HiddenStrike, "巽蛇·匿影斩", BattleFamily.Wealth, BattleTarget.Enemy, BattleEffect.Damage, 15, 20, 3, false,
                    "单体伤害20；50%概率使目标下一次攻击落空。"),
                Row(BattleSkillKind.ThunderMark, "震爻·雷动惊爻", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 18, 26, 4, true,
                    "普通：单体雷伤26，附加4回合震标，最多6层。强化：先附加本次震标，再引爆全体敌人身上的全部震标，每层额外13伤害并消耗标记；眩晕受影响敌人一次，抗眩晕目标改为削弱20%。"),
                Row(BattleSkillKind.FlameForge, "离爻·炎光铸爻", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 19, 21, 3, true,
                    "普通：单体火伤21，附加2回合灼烧，每轮5伤害，并具有洞察效果。强化：灼烧扩散至所有敌人，每轮伤害提高到8，并驱散目标一层增益。"),
                Row(BattleSkillKind.MountainBarrier, "艮爻·层山锁爻", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Bind, 24, 15, 2, true,
                    "普通：山障碰撞单体一次，伤害15；减速对应目标输出降低30%，持续2回合。强化：输出降低提高到50%，额外禁锢一次；抗眩晕目标改为本轮削弱20%。"),
                Row(BattleSkillKind.ShadowMark, "纳甲引晦", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 25, 0, 2, true,
                    "普通：标记单体5回合，每轮8术法伤害，无即时伤害。强化：扩散至另外两名敌人；被标记目标受到的全部伤害+12%，持续5回合。"),
                Row(BattleSkillKind.SolarCurse, "日辰刑爻", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Reduction, 16, 0, 3, true,
                    "普通：使单体输出降低15%，持续3回合，无直接伤害。强化：输出降低提高到30%。"),
                Row(BattleSkillKind.GuNursery, "卦山养蛊", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 28, 8, 2, true,
                    "普通：生成养蛊领域3回合，每轮一只蛊虫随机攻击一次，伤害8，无即时伤害。强化：每轮两只蛊虫，合计16伤害；领域结束时清空自身护盾。"),
                Row(BattleSkillKind.TripleDoom, "蛊断·三命皆凶", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 32, 36, 1, false,
                    "单体伤害36；若目标生命值高于50%，基础伤害提高到48。"),
                Row(BattleSkillKind.WindBlades, "巽爻·长风衍卦", BattleFamily.Offspring, BattleTarget.AllEnemies, BattleEffect.Damage, 15, 10, 4, true,
                    "普通：每名敌人各受一次风刃伤害10。强化：每敌额外挂一个盘旋风刃，持续2回合，每轮一次6切割伤害。"),
                Row(BattleSkillKind.HeartLight, "贲爻·照心", BattleFamily.Offspring, BattleTarget.Enemy, BattleEffect.Damage, 14, 18, 2, false,
                    "单体光伤18，驱散目标一层增益；若目标有标记，额外伤害8。"),
                Row(BattleSkillKind.ReturningBreath, "复卦·归息", BattleFamily.Offspring, BattleTarget.Self, BattleEffect.Heal, 12, 30, 3, false,
                    "立即回复30生命；接下来2回合每回合额外回复6生命。"),
                Row(BattleSkillKind.SixLineFateGu, "六爻断命蛊", BattleFamily.Offspring, BattleTarget.Enemy, BattleEffect.Damage, 40, 19, 1, true,
                    "普通：独立判定吉凶，吉象50%造成53伤害；凶象造成19伤害，自身承受28反噬。强化：吉象概率提高到75%。"),
                Row(BattleSkillKind.ThunderFire, "丰爻·雷火并作", BattleFamily.Offspring, BattleTarget.Enemy, BattleEffect.Damage, 24, 30, 2, false,
                    "单体雷火伤害30；目标有灼烧或震标时额外伤害10。"),
                Row(BattleSkillKind.EarthWard, "坤爻·厚土载冥", BattleFamily.Parent, BattleTarget.Self, BattleEffect.Shield, 22, 45, 3, true,
                    "普通：获得45护盾，持续3回合；后续2回合每轮回复4生命。强化：护盾提高到75，护盾抵挡敌方直接攻击时回复5MP，持续3回合。护盾总上限100。"),
                Row(BattleSkillKind.WaterClone, "坎爻·玄水藏变", BattleFamily.Parent, BattleTarget.Enemy, BattleEffect.Damage, 21, 17, 3, true,
                    "普通：召唤一个分身，无即时伤害；含本轮在内持续4回合，每轮结束随机攻击一次，每击17伤害。强化：增加一个分身，换位获得本轮20%减伤。"),
                Row(BattleSkillKind.LunarBlessing, "月建扶卦", BattleFamily.Parent, BattleTarget.Self, BattleEffect.Reduction, 26, 0, 2, true,
                    "普通：主角伤害+10%，持续8回合，无直接伤害。强化：增伤提高到22%，持续期间击杀敌人回复8MP。"),
                Row(BattleSkillKind.TripleChange, "卦变·三爻同动", BattleFamily.Parent, BattleTarget.Self, BattleEffect.Reduction, 35, 0, 1, true,
                    "普通：领域持续6回合，每轮等概率触发一项：增伤15%、减伤15%、回复10生命、输出降低15%、受伤+15%或每轮灼伤4。除即时治疗外持续2回合；所有效果给予主角。强化：增益给主角，负面给随机一名敌人。"),
                Row(BattleSkillKind.MysticArmor, "师爻·玄甲", BattleFamily.Parent, BattleTarget.Self, BattleEffect.Shield, 18, 40, 2, false,
                    "获得40护盾，持续3回合；护盾存在期间受到敌方攻击时反弹8伤害。反射伤害不引发再次反射。"),
                Row(BattleSkillKind.RevolvingGu, "变爻轮回蛊", BattleFamily.Sibling, BattleTarget.Enemy, BattleEffect.Damage, 18, 19, 3, true,
                    "普通：单体伤害19。强化：本次额外伤害14，等概率获得乾元增伤12%持续2回合、附加3回合震标或2回合灼烧（每轮5）；代价为2回合无法回复MP。"),
                Row(BattleSkillKind.DrawCalamity, "比卦·引灾", BattleFamily.Sibling, BattleTarget.Enemy, BattleEffect.Damage, 20, 22, 2, false,
                    "单体伤害22；引灾持续2回合，将目标当轮及下一轮的行动意图替换为基础攻击。"),
                Row(BattleSkillKind.AllLinesChange, "六爻尽变，万象归卜", BattleFamily.Officer, BattleTarget.AllEnemies, BattleEffect.Damage, 50, 60, 1, false,
                    "全体敌人各受60伤害；主角增伤25%、减伤20%；全体敌人防御降低20%，上述增减益均持续4回合。"),
                Row(BattleSkillKind.FateVerdict, "爻定吉凶", BattleFamily.Officer, BattleTarget.AllEnemies, BattleEffect.Damage, 45, 45, 1, false,
                    "独立判定吉凶，各50%：吉象获得50护盾和30%增伤，持续4回合；凶象对所有敌人各造成45伤害，自身承受38反噬，反噬无视护盾。"),
                Row(BattleSkillKind.HeavenBurial, "乾坤一掷·葬天卜", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 48, 62, 1, false,
                    "单体伤害62，对另外两名敌人各造成22溅射伤害；主角下回合防御降低15%。"),
                Row(BattleSkillKind.YinYangSlash, "阴阳斩·鬼哭神嚎", BattleFamily.Officer, BattleTarget.Enemy, BattleEffect.Damage, 42, 55, 1, false,
                    "单体伤害55；若目标生命值低于30%，基础伤害提高到70。"),
                Row(BattleSkillKind.RecoilTalent, "天赋·卜卦反噬", BattleFamily.Sibling, BattleTarget.Self, BattleEffect.Damage, 0, 0, 0, false,
                    "敌方直接攻击命中主角时，30%概率反弹7伤害并回复3MP。反震、反弹、持续伤害及自身反噬不触发此被动。"),
                Row(BattleSkillKind.CriticalTalent, "天赋·动爻惊煞", BattleFamily.Sibling, BattleTarget.Self, BattleEffect.Damage, 0, 0, 0, false,
                    "暴击后，下一个单体技能伤害+20%；每回合最多触发一次。初始暴击概率20%，暴击倍率1.5，后续根据测试调整。")
            };
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(CommandPath)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(CommandPath)); }
            catch (Exception exception) { Debug.LogException(exception); return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(LastIdKey, "")) return;
            SessionState.SetString(LastIdKey, command.id);
            try
            {
                if (command.verb != "install") throw new InvalidOperationException("技能表命令只支持 install。");
                Install(); WriteResult(command.id, "complete", true, "完整30条技能表、3个常驻技能与旧版独立副本已安装。");
            }
            catch (Exception exception) { WriteResult(command.id, "rejected", false, exception.ToString()); Debug.LogException(exception); }
        }

        private static void WriteResult(string id, string state, bool success, string message)
        {
            Directory.CreateDirectory("Validation");
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result { id = id, state = state, success = success, message = message }, true));
        }
    }
}
