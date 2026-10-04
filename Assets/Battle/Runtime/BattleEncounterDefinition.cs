using System;
using UnityEngine;

namespace Emerge.Battle
{
    [Serializable] public sealed class BattleEnemySlot
    {
        public BattleEnemyDefinition enemy;
        [Min(0)] public int healthOverride;
    }

    [CreateAssetMenu(menuName = "Light/战斗/遭遇")]
    public sealed class BattleEncounterDefinition : ScriptableObject
    {
        [Min(1)] public int swiftVictoryRounds = 5;
        public string id, displayName;
        [TextArea] public string description;
        public string month = "巳月", day = "戊子日";
        public BattleEnemySlot[] enemies = Array.Empty<BattleEnemySlot>();
        public bool repeatable = true;
        public string victoryFlag;
        public bool useFixedSeed;
        public int fixedSeed = 12637;
    }
}
