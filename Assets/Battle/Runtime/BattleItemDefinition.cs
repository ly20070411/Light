using UnityEngine;

namespace Emerge.Battle
{
    [CreateAssetMenu(menuName = "Light/战斗/一次性物品")]
    public sealed class BattleItemDefinition : ScriptableObject
    {
        public string id, displayName;
        [Tooltip("与现有背包物品键一致。")] public string inventoryKey;
        public BattleItemEffect effect;
        [Min(1)] public int power = 35;
        [Tooltip("点数战斗使用独立数值；回蓝道具不适用。")]
        [Min(1)] public int pointPower = 3;
        [TextArea] public string description;
    }
}
