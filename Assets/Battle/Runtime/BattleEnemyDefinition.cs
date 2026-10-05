using System;
using UnityEngine;

namespace Emerge.Battle
{
    [Serializable] public sealed class EnemySkillDefinition
    {
        public string id, displayName;
        public EnemyEffect effect;
        [Min(0)] public int mpCost;
        [Min(0)] public int power = 10;
        [Min(1)] public int weight = 70;
        [Range(0, 1)] public float weakness;
        [Range(0, 1)] public float maximumHealthFraction = 1;
        public int maximumShield = int.MaxValue;
        // Zero means no per-battle use limit.
        [Min(0)] public int maximumUses;
        [Min(0)] public int cooldownRounds;
    }

    [CreateAssetMenu(menuName = "Light/战斗/敌人")]
    public sealed class BattleEnemyDefinition : ScriptableObject
    {
        public string id, displayName;
        [Min(1)] public int maxHP = 60, maxMP = 12;
        [Min(0)] public int roundMana = 4;
        public bool resistsStun;
        [Tooltip("受到实际 HP 伤害时反震的比例；主角减伤和护盾可抵消，致命命中也会触发。")]
        [Range(0, 1)] public float retaliation;
        public Color color = new Color(.76f, .35f, .45f);
        public Sprite battlePortrait;
        public EnemySkillDefinition[] skills = Array.Empty<EnemySkillDefinition>();
    }
}
