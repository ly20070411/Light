using System.Linq;

namespace Emerge.Battle
{
    public static class BattleDescriptions
    {
        public static string Skill(BattleSkillDefinition s, BattleRules r, bool buildRules = true)
        {
            string target = s.target == BattleTarget.Enemy ? "单体敌人" : s.target == BattleTarget.AllEnemies ? "所有敌人" : "自身";
            string effect;
            switch (s.effect)
            {
                case BattleEffect.Damage: effect = "基础伤害：" + s.power; break;
                case BattleEffect.Heal: effect = "基础回复：" + s.power + " HP"; break;
                case BattleEffect.Shield: effect = "基础护盾：" + s.power; break;
                case BattleEffect.Reduction: effect = "基础减伤：" + s.power.ToString("P0"); break;
                case BattleEffect.Regeneration: effect = "下轮开始，每轮回复 " + s.power + " HP，持续 " + s.regenerationTicks + " 轮。"; break;
                case BattleEffect.NextMana: effect = "下轮额外恢复 " + s.power + " MP。"; break;
                case BattleEffect.Cleanse: effect = "解除削弱、灼伤、破绽；下轮额外恢复 " + s.power + " MP。"; break;
                case BattleEffect.Silence: effect = buildRules ? "封诀一次行动，将耗 MP 技能替换为普攻。" : "卦势 ≥ 0：封诀一次行动；负分时改为削弱 10%。"; break;
                default: effect = buildRules ? "眩晕一次行动；抗控敌人转为基础 20% 削弱 × 属性倍率。" : "卦势 2：眩晕一次行动；否则按评分施加削弱。"; break;
            }
            return s.displayName + "\n" + BattleRules.FamilyName(s.family) + " · " + target + "\n消耗：" + s.mpCost + " MP\n" + effect +
                (s.appliesVulnerability ? (buildRules ? "\n伤害后施加固定易伤。" : "\n卦势 ≥ 0：伤害后施加易伤。") : "") +
                "\n结算后可继续行动。";
        }
        public static string SkillStatuses(BattleSkillDefinition s, BattleRules r, bool buildRules = true)
        {
            if (s.appliesVulnerability) return "易伤\n后续直接伤害 +" + r.vulnerability.ToString("P0") + "。\n持续 " + r.vulnerabilityHits + " 次命中" + (buildRules ? "。" : "；代表爻为动爻时 " + (r.vulnerabilityHits + 1) + " 次。") + "\n重复施加刷新，不叠加。";
            switch (s.effect)
            {
                case BattleEffect.Shield: return "护盾\n先抵消伤害，再损失 HP；可累加至 " + r.shieldCap + "。\n下一个玩家回合开始时清除。";
                case BattleEffect.Reduction: return "减伤\n敌方伤害按比例降低，上限 " + r.reductionCap.ToString("P0") + "。\n同类取较高值，不相加；下轮玩家开始时清除。";
                case BattleEffect.Regeneration: return "生息\n从下一玩家回合开始回复，持续 " + s.regenerationTicks + " 次。\n同类取较高回复量并刷新持续次数。";
                case BattleEffect.NextMana: return "回蓝\n下轮与自然回蓝 " + r.roundMana + " MP 一起结算。\n额外回蓝取较高值，不相加，上限 " + r.nextManaCap + "。";
                case BattleEffect.Cleanse: return "负面状态 / 回蓝\n削弱：攻击伤害降低；灼伤：结束玩家回合损失 " + r.burnDamage + " HP，持续 " + r.burnTicks + " 次。\n破绽：承受直接伤害 +" + r.exposure.ToString("P0") + "，至下一敌方阶段结束。\n清心立即移除三种状态；回蓝在下轮生效，取较高值。";
                case BattleEffect.Silence: return buildRules ? "封诀\n本次耗 MP 技能替换为 0 MP 普攻。\n固定一次行动，与卦势无关。" : "封诀 / 削弱\n封诀：本次耗 MP 技能替换为 0 MP 普攻。\n削弱：攻击伤害降低 10%，持续一次行动。";
                case BattleEffect.Bind: return buildRules ? "眩晕 / 定力 / 削弱\n眩晕跳过一次行动，然后获得定力。\n首领或有定力时转为基础 20% 削弱 × 属性倍率，上限 50%。\n固定一次行动，与卦势无关。" : "眩晕 / 定力 / 削弱\n眩晕跳过一次行动，然后获得定力，抵抗下一行动机会前的眩晕。\n评分 -2/-1、0、1：削弱 10%、20%、30%。\n首领或有定力时，眩晕改为削弱 " + r.bossWeakness.ToString("P0") + "。";
                default: return "";
            }
        }
        public static string Item(BattleItemDefinition item)
        {
            string effect = item.effect == BattleItemEffect.Heal ? "回复 " + item.power + " HP。" : item.effect == BattleItemEffect.Mana ? "立即恢复 " + item.power + " MP。" :
                item.effect == BattleItemEffect.DamageAll ? "所有存活敌人受到 " + item.power + " 基础伤害。" : "解除主角的削弱、灼伤、破绽。";
            return item.displayName + "\n" + effect + "\n消耗 1 件物品；不耗 MP，不定卦。\n使用后可继续行动。";
        }
        public static string Intent(BattleEnemyDefinition def, BattleEnemyState e, BattleRules rules, bool buildRules = true)
        {
            if (e.hp <= 0) return def.displayName + "\n已倒下，不会行动。";
            var s = def.skills.First(x => x.id == e.intentSkillId);
            string effect = s.effect == EnemyEffect.Shield ? "获得护盾 " + s.power : s.effect == EnemyEffect.Heal ? "回复 " + s.power + " HP" :
                s.effect == EnemyEffect.Charge ? "蓄势；下一轮准备释放 " + def.skills.First(x => x.effect == EnemyEffect.ChargedDamage).power + " 点基础伤害" :
                "对主角造成 " + s.power + " 点基础伤害" + (s.effect == EnemyEffect.Weaken ? "，并削弱 " + s.weakness.ToString("P0") : "");
            if (s.effect == EnemyEffect.Burn) effect += "\n灼伤：玩家回合结束损失 " + rules.burnDamage + " HP，持续 " + rules.burnTicks + " 次";
            if (s.effect == EnemyEffect.Exposure) effect += "\n破绽：承受直接伤害 +" + rules.exposure.ToString("P0") + "，至下一敌方阶段结束";
            if (s.effect == EnemyEffect.ChargedDamage) effect += "\n释放后敌人易伤 +25%，持续 3 次命中";
            string control = e.stunned ? "\n眩晕：本次行动跳过。" : e.silenced && s.mpCost > 0 ? "\n封诀：将改用 0 MP 普攻。" : "";
            return def.displayName + " · 敌方意图\n" + s.displayName + "\n" + effect + "。\n消耗 " + s.mpCost + " MP" + control +
                (e.weakness > 0 ? "\n攻击伤害降低 " + e.weakness.ToString("P0") + "。" : "") + "\n最终伤害受主角的减伤与护盾影响。" +
                (buildRules && def.retaliation > 0 ? "\n潮棘：反震实际 HP 伤害的 " + def.retaliation.ToString("P0") + "，减伤与护盾有效。致命攻击也触发；同归于尽判失败。" : "");
        }
    }
}
