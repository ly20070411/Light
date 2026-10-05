using UnityEngine;

namespace Emerge.Orthographic25D
{
    // Isolated view prototype: the existing 2D player, maps and game saves remain separate.
    [RequireComponent(typeof(CharacterController))]
    public sealed class Orthographic25DDemo : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform visual, groundShadow;
        public Sprite idle, stepA, stepB;
        public Vector3 spawn = new Vector3(3, .05f, -4);
        public float speed = 3.5f;
        public bool follow;
        public Vector3 framing = new Vector3(0, .8f, 0);
        private CharacterController body;
        private SpriteRenderer actor;
        private float verticalSpeed, animationPhase;
        private bool scripted;
        private Vector2 scriptedInput;
        private GUIStyle titleStyle, textStyle, badgeStyle;
        public Vector3 GroundForward => Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up).normalized;
        public Vector3 GroundRight => Vector3.ProjectOnPlane(viewCamera.transform.right, Vector3.up).normalized;
        public bool PreviewRunning { get; set; }

        private void Awake() { body = GetComponent<CharacterController>(); actor = visual.GetComponent<SpriteRenderer>(); }
        private void Update()
        {
            Vector2 input = scripted ? scriptedInput : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1);
            float rate = !scripted && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? speed * 1.6f : speed;
            Vector3 direction = GroundRight * input.x + GroundForward * input.y;
            if (body.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += -20 * Time.deltaTime;
            body.Move((direction * rate + Vector3.up * verticalSpeed) * Time.deltaTime);
            if (input.x != 0) actor.flipX = input.x < 0;
            if (input.sqrMagnitude > .01f) { animationPhase += Time.deltaTime * 10; actor.sprite = ((int)animationPhase & 1) == 0 ? stepA : stepB; }
            else { animationPhase = 0; actor.sprite = idle; }
            if (!PreviewRunning)
            {
                if (Input.GetKeyDown(KeyCode.R)) Teleport(spawn);
                if (Input.GetKeyDown(KeyCode.Alpha1)) follow = false;
                if (Input.GetKeyDown(KeyCode.Alpha2)) follow = true;
                viewCamera.orthographicSize = Mathf.Clamp(viewCamera.orthographicSize - Input.mouseScrollDelta.y * .35f, 4, 12);
            }
        }
        private void LateUpdate()
        {
            Vector3 center = follow ? transform.position + Vector3.up * .8f : framing;
            viewCamera.transform.position = center - viewCamera.transform.forward * 24;
            // Facing the fixed camera keeps the temporary sprite readable; its bottom stays at the feet.
            visual.rotation = viewCamera.transform.rotation;
            // The existing hero sprites have their pivot at the feet.
            visual.position = transform.position + Vector3.up * .02f;
            if (groundShadow != null) groundShadow.position = transform.position + Vector3.up * .025f;
        }
        public void Teleport(Vector3 position)
        { body.enabled = false; transform.position = position; body.enabled = true; verticalSpeed = 0; Physics.SyncTransforms(); }
        public void SetScriptedInput(Vector2 input) { scripted = true; scriptedInput = input; }
        public void ResumeInput() { scripted = false; scriptedInput = Vector2.zero; }

        private void OnGUI()
        {
            if (titleStyle == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 25, normal = { textColor = new Color(1, .86f, .57f) } };
                textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, normal = { textColor = new Color(.85f, .9f, .93f) } };
                badgeStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            }
            float ratio = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            var oldMatrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(ratio, ratio, 1));
            GUI.color = new Color(.04f, .07f, .1f, .88f); GUI.DrawTexture(new Rect(24, 22, 750, 108), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(44, 32, 700, 38), "2.5D 正交视角 · 占位小样", titleStyle);
            GUI.Label(new Rect(44, 75, 720, 25), "WASD / 方向键移动 · Shift 加速 · 滚轮缩放 · R 回到起点", textStyle);
            GUI.Label(new Rect(44, 100, 720, 25), "1 全景 / 2 跟随 · 楼梯可走上高台 · 角色使用 2D Sprite", textStyle);
            GUI.Label(new Rect(26, Screen.height / ratio - 49, 1100, 30),
                "正交投影  |  俯角 35° / 朝向 45°  |  " + (follow ? "角色跟随" : "全景展示") + "  |  当前高度 " + transform.position.y.ToString("0.0") + " m", textStyle);
            GUI.matrix = oldMatrix;
            Tag(new Vector3(-2.4f, 1.6f, 3.5f), "高台 · 1.6 m");
            Tag(new Vector3(.5f, .8f, -1), "楼梯");
        }
        private void Tag(Vector3 position, string text)
        {
            Vector3 point = viewCamera.WorldToScreenPoint(position);
            if (point.z <= 0) return;
            float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            var rect = new Rect(point.x - 85 * scale, Screen.height - point.y - 36 * scale, 170 * scale, 32 * scale);
            GUI.color = new Color(.04f, .07f, .1f, .66f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = Color.white;
            badgeStyle.fontSize = Mathf.RoundToInt(16 * scale); GUI.Label(rect, text, badgeStyle);
        }
    }
}
