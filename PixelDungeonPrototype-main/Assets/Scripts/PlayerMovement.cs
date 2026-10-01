using UnityEngine;

namespace PixelPrototype
{
    /// <summary>XY-plane movement for Unity 2022.3 and the legacy Input Manager.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        private Rigidbody2D body;
        private Vector2 input;
        private bool useScriptedInput;
        private Vector2 scriptedInput;

        public float MoveSpeed => moveSpeed;
        public Vector2 MoveInput => input;
        public Rigidbody2D Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            Vector2 requested = useScriptedInput ? scriptedInput : new Vector2(
                Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(requested, 1f);
        }

        private void FixedUpdate()
        {
            // velocity is units per second; it must not be multiplied by deltaTime.
            body.velocity = input * moveSpeed;
        }

        private void OnDisable()
        {
            input = Vector2.zero;
            if (body != null) body.velocity = Vector2.zero;
        }

        /// <summary>Deterministic input hook used by the real PlayMode validation.</summary>
        public void SetScriptedInput(Vector2 value)
        {
            useScriptedInput = true;
            scriptedInput = value;
            input = Vector2.ClampMagnitude(value, 1f);
        }

        public void ResumeKeyboardInput()
        {
            useScriptedInput = false;
        }
    }
}
