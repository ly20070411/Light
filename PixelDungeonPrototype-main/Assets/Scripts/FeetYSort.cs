using UnityEngine;

namespace PixelPrototype
{
    /// <summary>Objects lower on screen draw in front, using their ground contact point.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FeetYSort : MonoBehaviour
    {
        [SerializeField] private Transform feet;
        [SerializeField] private int orderOffset;
        private SpriteRenderer spriteRenderer;
        private void Awake() { spriteRenderer = GetComponent<SpriteRenderer>(); }
        private void LateUpdate()
        {
            Transform reference = feet != null ? feet : transform;
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-reference.position.y * 32f) + orderOffset;
        }
    }
}
