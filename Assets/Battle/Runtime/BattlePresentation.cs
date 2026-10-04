using UnityEngine;

namespace Emerge.Battle
{
    [CreateAssetMenu(menuName = "Light/战斗/界面素材")]
    public sealed class BattlePresentation : ScriptableObject
    {
        public const string ResourcePath = "Battle/BattlePresentation";
        public Sprite background, heroPortrait, defaultEnemyPortrait;
        public Sprite skillIcon, bagIcon, talkIcon, fleeIcon, itemIcon;
        public Sprite coinFront, coinBack, attackEffect, healingEffect, shieldEffect;
    }
    public enum BattlePage { Skills, Backpack, Negotiation, Escape }
}
