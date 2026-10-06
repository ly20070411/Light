using UnityEngine;

namespace Emerge.Battle
{
    [CreateAssetMenu(menuName = "Light/战斗/玩家技能")]
    public sealed class BattleSkillDefinition : ScriptableObject
    {
        public string id, displayName;
        public BattleSkillKind kind;
        public BattleFamily family;
        public BattleTarget target;
        public BattleEffect effect;
        [Min(0)] public int mpCost = 3;
        [Min(0)] public float power = 7;
        [TextArea] public string description;
        public bool appliesVulnerability;
        public bool alwaysAvailable;
        public bool enhancedAvailable, isUltimate, isPassive;
        [Min(0)] public int maximumUses;
        [Min(1)] public int regenerationTicks = 3;
    }
}
