using UnityEngine;

namespace PixelPrototype
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private PlayerMovement movement;
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite stepA;
        [SerializeField] private Sprite stepB;
        private SpriteRenderer spriteRenderer;
        private float phase;

        private void Awake() { spriteRenderer = GetComponent<SpriteRenderer>(); }

        private void Update()
        {
            Vector2 input = movement.MoveInput;
            if (input.x != 0f) spriteRenderer.flipX = input.x < 0f;
            if (input.sqrMagnitude < 0.01f)
            {
                phase = 0f;
                spriteRenderer.sprite = idle;
            }
            else
            {
                phase += Time.deltaTime * 10f;
                spriteRenderer.sprite = ((int)phase & 1) == 0 ? stepA : stepB;
            }
        }
    }
}
