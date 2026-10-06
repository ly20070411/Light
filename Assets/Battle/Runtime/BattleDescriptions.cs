using System.Collections.Generic;
using System.Linq;

namespace Emerge.Battle
{
    public static class BattleDescriptions
    {
        public static string Build(BattleSession session)
        {
            if (session == null) return "";
            var lines = new List<string> { "五亲加点 · 入战后保持本场配置" };
            if (session.attributes != null)
                foreach (BattleFamily family in System.Enum.GetValues(typeof(BattleFamily)))
                    lines.Add(BattleRules.FamilyName(family) + "：" + BattleBuildRules.Points(session.attributes, family) + " 点");
            lines.Add("每点提高对应五亲技能基础效果 10%；基础技能也享受加成。\n终结技能使用自身基础效果。");
            if (session.version >= 4)
                lines.Add("妻财：直接输出、破防与窃取；官鬼：雷火、诅咒与控制；子孙：风刃、驱散与恢复；父母：护盾、分身、领域与反伤；兄弟：变爻爆发与引灾。\n对应五亲达到 3 点可获得该类技能增强资格。");
            return string.Join("\n", lines);
        }
        public static string Statuses(IEnumerable<BattleTimedStatus> statuses, bool compact = false)
        {
            if (statuses == null) return "";
            var entries = statuses.Where(s => s != null && s.rounds > 0).Select(Status).ToArray();
            if (!compact) return string.Join("\n", entries);
            return string.Join("\n", entries.Take(3)) + (entries.Length > 3 ? "\n另 " + (entries.Length - 3) + " 项 · 悬停查看" : "");
        }
        private static string Status(BattleTimedStatus status)
        {
            string value;
            switch (status.kind)
            {
                case BattleStatusKind.DamageUp: value = "增伤 +" + status.power.ToString("P0"); break;
                case BattleStatusKind.DamageReduction: value = "减伤 " + status.power.ToString("P0"); break;
                case BattleStatusKind.DefenseBreak: value = "破防 " + status.power.ToString("P0"); break;
                case BattleStatusKind.IncomingUp: value = "易伤 +" + status.power.ToString("P0"); break;
                case BattleStatusKind.DamageDown: value = "削弱 " + status.power.ToString("P0"); break;
                case BattleStatusKind.Burn: value = "灼烧 " + status.power.ToString("0.#") + " HP / 轮"; break;
                case BattleStatusKind.ShadowCurse: value = "引晦 " + status.power.ToString("0.#") + " HP / 轮"; break;
                case BattleStatusKind.Thunder: value = "雷印 " + status.count + " 层"; break;
                case BattleStatusKind.Blind: value = "致盲 " + status.power.ToString("P0"); break;
                case BattleStatusKind.Reflect: value = "盾击反伤 " + status.power.ToString("0.#"); break;
                case BattleStatusKind.ManaOnHit: value = "护盾受击回 MP " + status.power.ToString("0.#"); break;
                case BattleStatusKind.KillMana: value = "击杀回 MP " + status.power.ToString("0.#"); break;
                case BattleStatusKind.ManaLock: value = "禁回 MP"; break;
                case BattleStatusKind.Taunt: value = "挑衅 · 改用普攻"; break;
                case BattleStatusKind.DefenseDown: value = "防御下降 " + status.power.ToString("P0"); break;
                default: value = "天势 +" + status.power.ToString("P0") + " · " + status.count + " 层"; break;
            }
            return value + "（" + status.rounds + " 轮）";
        }
        public static string Summons(BattleSession session)
        {
            var lines = new List<string>();
            if (session.summons != null)
                foreach (var group in session.summons.Where(s => s != null && s.remainingRounds > 0).GroupBy(s => s.kind))
                {
                    string name = group.Key == BattleSummonKind.Clone ? "玄水分身" : group.Key == BattleSummonKind.GuNest ? "蛊虫" : "盘旋风刃";
                    lines.Add(name + " ×" + group.Count() + " · 剩余 " + string.Join(" / ", group.Select(s => s.remainingRounds).Distinct().OrderByDescending(x => x)) + " 轮");
                }
            if (session.domainRounds > 0) lines.Add((session.domainEnhanced ? "增强" : "") + "三爻领域 · 剩余 " + session.domainRounds + " 轮");
            return string.Join("\n", lines);
        }
        public static bool HasDelayedEnemyDamage(BattleSkillDefinition skill, bool enhanced)
        {
            switch (skill.kind)
            {
                case BattleSkillKind.GuNursery:
                case BattleSkillKind.WaterClone:
                case BattleSkillKind.ShadowMark:
                case BattleSkillKind.FlameForge: return true;
                case BattleSkillKind.WindBlades:
                case BattleSkillKind.TripleChange:
                case BattleSkillKind.RevolvingGu: return enhanced;
                default: return false;
            }
        }
        public static string Skill(BattleSkillDefinition s, BattleRules r, bool buildRules = true, bool skillTableRules = false)
        {
            string target = s.target == BattleTarget.Enemy ? "单体敌人" : s.target == BattleTarget.AllEnemies ? "所有敌人" : "自身";
            if (skillTableRules)
            {
                string category = s.isPassive ? "被动技能" : s.isUltimate ? "终结技能" : s.alwaysAvailable ? "基础技能" : "五亲技能";
                string cost = s.isPassive ? "满足条件自动触发，无需点击施放。" : "消耗：" + s.mpCost + " MP";
                string classification = s.isUltimate || s.isPassive ? category : BattleRules.FamilyName(s.family) + " · " + category;
                return s.displayName + "\n" + classification + " · " + target + "\n" + cost + "\n" + s.description +
                    (s.isPassive ? "" : "\n结算后可继续行动。");
            }
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
        public static string SkillStatuses(BattleSkillDefinition s, BattleRules r, bool buildRules = true, bool skillTableRules = false)
        {
            if (skillTableRules)
            {
                var notes = new List<string>();
                if (s.enhancedAvailable)
                    notes.Add("增强\n对应五亲达到 3 点，并在本轮定卦抽中该技能时生效。\n每个技能每场仅可增强一次，消耗 MP 与普通版相同；金色按钮边框表示本次增强。");
                if (s.isUltimate)
                    notes.Add("终结\n从第 " + BattleSkillTableRules.UltimateFirstRound + " 轮开始可用，仍须足够 MP 与剩余次数；不参与增强。");
                if (s.kind == BattleSkillKind.SixLineFateGu)
                    notes.Add("吉凶\n普通吉兆概率 " + BattleSkillTableRules.FateGoodChance.ToString("P0") + "；增强吉兆概率 " + BattleSkillTableRules.EnhancedFateGoodChance.ToString("P0") + "。\n吉凶在施放时结算。");
                if (s.isPassive)
                    notes.Add("被动\n满足技能说明中的条件后由战斗自动结算，不占用主动技能按钮。");
                if (s.effect == BattleEffect.Cleanse)
                    notes.Add("净化\n立即移除可净化的负面状态；技能额外回 MP 在下轮开始结算，取较高值。");
                notes.Add("暴击\n直接伤害基础暴击率 " + BattleSkillTableRules.CritChance.ToString("P0") + "，暴击伤害 ×" + BattleSkillTableRules.CritMultiplier.ToString("0.0") + "。\n加点与状态的具体影响以实际效果为准。");
                return string.Join("\n\n", notes);
            }
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
        public static string Intent(BattleEnemyDefinition def, BattleEnemyState e, BattleRules rules, bool buildRules = true, bool skillTableRules = false)
        {
            if (e.hp <= 0) return def.displayName + "\n已倒下，不会行动。";
            var s = def.skills.First(x => x.id == e.intentSkillId);
            string effect = s.effect == EnemyEffect.Shield ? "获得护盾 " + s.power : s.effect == EnemyEffect.Heal ? "回复 " + s.power + " HP" :
                s.effect == EnemyEffect.Charge ? "蓄势；下一轮准备释放 " + def.skills.First(x => x.effect == EnemyEffect.ChargedDamage).power + " 点基础伤害" :
                "对主角造成 " + s.power + " 点基础伤害" + (s.effect == EnemyEffect.Weaken ? "，并削弱 " + s.weakness.ToString("P0") : "");
            if (s.effect == EnemyEffect.Burn) effect += "\n灼伤：玩家回合结束损失 " + rules.burnDamage + " HP，持续 " + rules.burnTicks + " 次";
            if (s.effect == EnemyEffect.Exposure) effect += "\n破绽：承受直接伤害 +" + rules.exposure.ToString("P0") + "，至下一敌方阶段结束";
            if (s.effect == EnemyEffect.ChargedDamage) effect += "\n释放后敌人易伤 +25%，持续 3 次命中";
            bool taunted = skillTableRules && BattleEngine.Status(e.statuses, BattleStatusKind.Taunt) > 0;
            string control = e.stunned ? "\n眩晕：本次行动跳过。" : taunted ? "\n挑衅：当前特殊意图将改为基础普攻（0 MP），不执行上方特殊效果。" : e.silenced && s.mpCost > 0 ? "\n封诀：将改用 0 MP 普攻。" : "";
            return def.displayName + " · 敌方意图\n" + s.displayName + "\n" + effect + "。\n消耗 " + s.mpCost + " MP" + control +
                (e.weakness > 0 ? "\n攻击伤害降低 " + e.weakness.ToString("P0") + "。" : "") + "\n最终伤害受主角的减伤与护盾影响。" +
                (buildRules && def.retaliation > 0 ? "\n潮棘：反震实际 HP 伤害的 " + def.retaliation.ToString("P0") + "，减伤与护盾有效。致命攻击也触发；同归于尽判失败。" + (skillTableRules ? "敌方输出削弱同样降低反震。" : "") : "") +
                (skillTableRules ? "\n基础防御：" + e.defense.ToString("P0") + (e.statuses.Count > 0 ? "\n当前状态\n" + Statuses(e.statuses) : "") : "");
        }
    }
}
