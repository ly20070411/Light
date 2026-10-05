using Emerge.Checks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Emerge.GameFlow
{
    public sealed class CharacterAttributeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private GameMenuUI menu;
        private CheckBehavior attribute;
        private bool hovering;
        public void Bind(GameMenuUI owner, CheckBehavior value) { menu = owner; attribute = value; }
        public void OnPointerEnter(PointerEventData data)
        { hovering = true; menu.ShowAttributeTooltip(attribute, data.position); }
        public void OnPointerExit(PointerEventData data)
        { hovering = false; if (menu != null) menu.HideAttributeTooltip(attribute); }
        private void Update()
        { if (hovering && menu != null) menu.MoveAttributeTooltip(Input.mousePosition); }
        private void OnDisable()
        { hovering = false; if (menu != null) menu.HideAttributeTooltip(attribute); }
    }
}
