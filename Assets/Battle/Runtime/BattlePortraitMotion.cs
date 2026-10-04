using UnityEngine;
using UnityEngine.UI;

namespace Emerge.Battle
{
    public sealed class BattlePortraitMotion : MonoBehaviour
    {
        public Image portrait, effect;
        private RectTransform rect;
        private Vector2 home;
        private float remaining, duration, direction;
        private Color tint;
        private bool damaged;
        private void Awake() { rect = (RectTransform)transform; home = rect.anchoredPosition; }
        public void ResetHome() { rect = (RectTransform)transform; home = rect.anchoredPosition; remaining = 0; }
        public void Play(float movement, Sprite sprite, Color color, bool hit = false)
        {
            if (rect == null) ResetHome();
            remaining = duration = .55f; direction = movement; tint = color; damaged = hit;
            if (effect != null) { effect.sprite = sprite; effect.color = color; effect.gameObject.SetActive(sprite != null); }
        }
        private void Update()
        {
            if (remaining <= 0) return;
            remaining = Mathf.Max(0, remaining - Time.deltaTime); float t = 1 - remaining / duration;
            rect.anchoredPosition = home + Vector2.right * (direction * Mathf.Sin(t * Mathf.PI));
            if (portrait != null) portrait.color = damaged ? Color.Lerp(Color.white, tint, Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3)) * .65f) : Color.white;
            if (effect != null)
            { var color = tint; color.a *= Mathf.Sin(t * Mathf.PI); effect.color = color; effect.transform.localScale = Vector3.one * Mathf.Lerp(.6f, 1.25f, t); }
            if (remaining == 0) { rect.anchoredPosition = home; if (portrait != null) portrait.color = Color.white; if (effect != null) effect.gameObject.SetActive(false); }
        }
    }
}
