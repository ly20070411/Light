using UnityEngine;

namespace PixelPrototype
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(1f)] private float sprintMultiplier = 2f;
        private Rigidbody2D body;
        private Vector2 input;
        private bool useScriptedInput;
        private Vector2 scriptedInput;
        private bool scriptedSprint;

        public float MoveSpeed => moveSpeed;
        public float SprintMultiplier => sprintMultiplier;
        public float CurrentSpeed => moveSpeed * (IsSprinting ? sprintMultiplier : 1f);
        public bool IsSprinting { get; private set; }
        public Vector2 MoveInput => input;
        public Rigidbody2D Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed)
            { input = Vector2.zero; IsSprinting = false; if (body != null) body.velocity = Vector2.zero; return; }
            Vector2 requested = useScriptedInput ? scriptedInput : new Vector2(
                Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(requested, 1f);
            IsSprinting = useScriptedInput ? scriptedSprint :
                Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private void FixedUpdate()
        {
            // Leave collision resolution to Physics2D; never move the Transform here.
            body.velocity = Emerge.GameFlow.GameSessionController.GameplayInputAllowed ? input * CurrentSpeed : Vector2.zero;
        }

        private void OnDisable()
        {
            input = Vector2.zero;
            IsSprinting = false;
            if (body != null) body.velocity = Vector2.zero;
        }

        public void SetScriptedInput(Vector2 value, bool sprint = false)
        {
            useScriptedInput = true;
            scriptedInput = value;
            scriptedSprint = sprint;
            input = Vector2.ClampMagnitude(value, 1f);
            IsSprinting = sprint;
        }

        public void ResumeKeyboardInput()
        {
            useScriptedInput = false;
            scriptedInput = Vector2.zero;
            scriptedSprint = false;
            input = Vector2.zero;
            IsSprinting = false;
        }
    }
}
