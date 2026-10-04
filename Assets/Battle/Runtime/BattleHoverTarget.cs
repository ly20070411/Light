using UnityEngine;
using UnityEngine.EventSystems;

namespace Emerge.Battle
{
    public sealed class BattleHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public BattleView view;
        public string skillId, itemId;
        public int enemyIndex = -1;
        public void OnPointerEnter(PointerEventData e)
        {
            if (view == null) return;
            if (!string.IsNullOrEmpty(skillId)) view.ShowSkillTooltip(skillId, e.position);
            else if (!string.IsNullOrEmpty(itemId)) view.ShowItemTooltip(itemId, e.position);
            else view.ShowEnemyTooltip(enemyIndex, e.position);
        }
        public void OnPointerExit(PointerEventData e) { if (view != null) view.HideTooltip(); }
    }
}
