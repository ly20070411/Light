using System;
using UnityEngine;

namespace Emerge.Battle
{
    public enum BattleFamily { Officer, Parent, Wealth, Offspring, Sibling }
    public enum BattleTarget { Self, Enemy, AllEnemies }
    public enum BattleEffect { Damage, Shield, Reduction, Heal, Regeneration, NextMana, Cleanse, Silence, Bind }
    public enum EnemyEffect { Damage, Shield, Heal, Weaken, Burn, Exposure, Charge, ChargedDamage }
    public enum BattleItemEffect { Heal, Mana, Cleanse, DamageAll }
    public enum BattlePhase { Player, Casting, Enemy, Victory, Defeat, RoundCasting }

    [CreateAssetMenu(menuName = "Light/战斗/基础规则")]
    public sealed class BattleRules : ScriptableObject
    {
        [Min(1)] public int maxHP = 100;
        [Min(1)] public int maxMP = 30;
        [Min(1)] public int roundMana = 14;
        [Min(1)] public int nextManaCap = 12;
        [Min(1)] public int shieldCap = 40;
        [Range(0, 1)] public float reductionCap = .5f;
        public float[] multipliers = { .6f, .8f, 1, 1.2f, 1.4f };
        [Range(0, 1)] public float vulnerability = .2f;
        [Min(1)] public int vulnerabilityHits = 2;
        [Range(0, 1)] public float bossWeakness = .3f;
        public string balanceVersion = "v0.3-easy";
        [Range(1, 8)] public int advancedOptions = 3;
        [Min(1)] public int enemyIntentBudget = 44;
        [Min(1)] public int burnDamage = 5, burnTicks = 2;
        [Range(0, .5f)] public float exposure = .2f;

        public float Multiplier(int score) => multipliers[Mathf.Clamp(score, -2, 2) + 2];
        // Use the same positive-number rounding as the initial Excel configuration.
        public static int Round(float value) => Mathf.FloorToInt(value + .5f);
        public static string FamilyName(BattleFamily family)
        {
            switch (family)
            {
                case BattleFamily.Officer: return "官鬼";
                case BattleFamily.Parent: return "父母";
                case BattleFamily.Wealth: return "妻财";
                case BattleFamily.Offspring: return "子孙";
                default: return "兄弟";
            }
        }
        public bool Validate(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(balanceVersion) || advancedOptions < 1 || advancedOptions > 8 || enemyIntentBudget < 1 || burnDamage < 1 || burnTicks < 1 ||
                float.IsNaN(exposure) || exposure < 0 || exposure > .5f || maxHP < 1 || maxMP < 1 || roundMana < 1 || nextManaCap < 1 || shieldCap < 1 ||
                float.IsNaN(reductionCap) || float.IsNaN(vulnerability) || float.IsNaN(bossWeakness) ||
                reductionCap < 0 || reductionCap > 1 || vulnerability < 0 || vulnerability > 1 ||
                vulnerabilityHits < 1 || bossWeakness < 0 || bossWeakness > 1 ||
                multipliers == null || multipliers.Length != 5) error = "战斗基础规则无效。";
            else foreach (float multiplier in multipliers)
                if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0 || multiplier > 3)
                    error = "卦势倍率必须是 0～3 之间的正数。";
            return error == null;
        }
    }
}
