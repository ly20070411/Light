using UnityEngine;

namespace Emerge.Battle
{
    [CreateAssetMenu(menuName = "Light/战斗/玩家技能")]
    public sealed class BattleSkillDefinition : ScriptableObject
    {
        public string id, displayName;
        public BattleFamily family;
        public BattleTarget target;
        public BattleEffect effect;
        [Min(1)] public int mpCost = 3;
        [Min(0)] public float power = 7;
        [TextArea] public string description;
        public bool appliesVulnerability;
        public bool alwaysAvailable;
        [Min(0)] public int maximumUses;
        [Min(1)] public int regenerationTicks = 3;
    }
}
