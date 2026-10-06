using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks;
using Emerge.Checks.Divination;

namespace Emerge.Battle
{
    // Versioned constants keep previews, execution and save validation on the same rules.
    public static class BattleBuildRules
    {
        public const int Version = 4;
        public const float BonusPerPoint = .10f;
        public static CheckBehavior Attribute(BattleFamily family)
        {
            switch (family)
            {
                case BattleFamily.Parent: return CheckBehavior.Parent;
                case BattleFamily.Offspring: return CheckBehavior.Offspring;
                case BattleFamily.Officer: return CheckBehavior.Officer;
                case BattleFamily.Wealth: return CheckBehavior.Wealth;
                case BattleFamily.Sibling: return CheckBehavior.Sibling;
                default: throw new ArgumentOutOfRangeException(nameof(family));
            }
        }
        public static int Points(ActorCheckAttributes attributes, BattleFamily family) => attributes.Get(Attribute(family));
        public static float Multiplier(int points) => 1 + BonusPerPoint * points;
        // p * (1 + n / 4), scaled by 4 to allow an exact integer lottery.
        public static int Weight(ActorCheckAttributes attributes, DivinationRecord record, BattleFamily family)
            => Points(attributes, family) * (4 + record.chart.yaos.Count(y => y.benLiuqin == BattleRules.FamilyName(family)));

        public static List<string> SelectOffers(BattleCatalog catalog, DivinationRecord record,
            List<EnemySkillUses> baseline, ActorCheckAttributes attributes, Action<string> trace = null, int round = 1)
        {
            if (!SixKinAttributes.IsValidBuild(attributes)) throw new ArgumentException("战斗需要有效的五亲 8 点快照。");
            var pool = catalog.skills.Where(s => !s.alwaysAvailable && !s.isPassive && !s.isUltimate && Points(attributes, s.family) > 0 &&
                (baseline.Find(u => u.skillId == s.id)?.count ?? 0) < s.maximumUses).ToList();
            var random = new Random(unchecked(record.casting.seed ^ 0x347D));
            var selected = new List<string>();
            while (selected.Count < Math.Min(BattleSkillTableRules.NormalOfferCount, catalog.rules.advancedOptions) && pool.Count > 0)
            {
                var families = pool.Select(s => s.family).Distinct().OrderBy(f => (int)f).ToArray();
                int total = families.Sum(f => Weight(attributes, record, f));
                int roll = random.Next(total), remainder = roll;
                var chosenFamily = families[0];
                foreach (var family in families)
                {
                    int weight = Weight(attributes, record, family);
                    if (remainder < weight) { chosenFamily = family; break; }
                    remainder -= weight;
                }
                var skills = pool.Where(s => s.family == chosenFamily).ToArray();
                int skillRoll = random.Next(skills.Length);
                var chosen = skills[skillRoll];
                trace?.Invoke("第 " + (selected.Count + 1) + " 格；权重 " + string.Join("、", families.Select(f =>
                    BattleRules.FamilyName(f) + "=" + Weight(attributes, record, f))) + "; 抽签 " + roll + "/" + total +
                    "；类内 " + skillRoll + "/" + skills.Length + " → " + chosen.displayName);
                selected.Add(chosen.id); pool.Remove(chosen);
            }
            if (round >= BattleSkillTableRules.UltimateFirstRound)
            {
                var ultimates = catalog.skills.Where(s => s.isUltimate && !s.isPassive &&
                    (baseline.Find(u => u.skillId == s.id)?.count ?? 0) < s.maximumUses).ToArray();
                if (ultimates.Length > 0)
                {
                    // A separate lottery keeps the terminal slot independent of family weights/draw count.
                    var ultimateRandom = new Random(unchecked(record.casting.seed ^ 0x6E79));
                    int roll = ultimateRandom.Next(ultimates.Length);
                    selected.Add(ultimates[roll].id);
                    trace?.Invoke("独立终结格；第 " + round + " 轮；抽签 " + roll + "/" + ultimates.Length + " → " + ultimates[roll].displayName);
                }
            }
            return selected;
        }
    }
}
