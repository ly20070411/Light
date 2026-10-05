using PixelPrototype;
using UnityEngine;

namespace Emerge.FlatMap25D
{
    /// <summary>A flat-map view with 2D feet physics and a separate visual height.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D))]
    public sealed class FlatMap25DDemo : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform visual, groundShadow;
        public Sprite idle, stepA, stepB;
        public Vector2 spawnWorld = new Vector2(3, -4);
        public bool follow;
        public bool ShowCollision;

        private static readonly Quaternion ProjectionRotation = Quaternion.Euler(35, 45, 0);
        private static readonly Vector3 ProjectionRight = ProjectionRotation * Vector3.right;
        private static readonly Vector3 ProjectionUp = ProjectionRotation * Vector3.up;
        private static readonly Vector3 Framing = new Vector3(0, .8f, 0);
        private Rigidbody2D body;
        private PlayerMovement movement;
        private SpriteRenderer actorRenderer, shadowRenderer;
        private Collider2D[] collisionOutlines;
        private float animationPhase;
        private float overviewZoom = 8.3f;
        private GUIStyle titleStyle, textStyle;

        public Rigidbody2D Body => body != null ? body : body = GetComponent<Rigidbody2D>();
        public PlayerMovement Movement => movement != null ? movement : movement = GetComponent<PlayerMovement>();
        public Vector2 WorldPosition => GroundToWorld(Body.position);
        public float Height => HeightAt(WorldPosition);
        public Vector2 VisualFootPosition => Body.position + Vector2.up * (Height * ProjectionUp.y);
        public bool PreviewRunning { get; set; }

        /// <summary>Projects an original scene point into the flat map's XY coordinates.</summary>
        public static Vector2 Project(Vector3 point)
        {
            Vector3 relative = point - Framing;
            return new Vector2(Vector3.Dot(relative, ProjectionRight), Vector3.Dot(relative, ProjectionUp));
        }

        /// <summary>Inverts the zero-height ground projection and returns original (x, z).</summary>
        public static Vector2 GroundToWorld(Vector2 point)
        {
            Vector2 origin = Project(Vector3.zero);
            Vector2 relative = point - origin;
            float determinant = ProjectionRight.x * ProjectionUp.z - ProjectionRight.z * ProjectionUp.x;
            return new Vector2(
                (relative.x * ProjectionUp.z - ProjectionRight.z * relative.y) / determinant,
                (ProjectionRight.x * relative.y - relative.x * ProjectionUp.x) / determinant);
        }

        public static float HeightAt(Vector2 worldXZ)
        {
            if (worldXZ.x >= -4 && worldXZ.x <= 2 && worldXZ.y >= 1 && worldXZ.y <= 5.5f) return 1.6f;
            if (worldXZ.x >= -.4f && worldXZ.x <= 1.4f && worldXZ.y >= -3 && worldXZ.y <= 1)
                return Mathf.Lerp(0, 1.6f, (worldXZ.y + 3) / 4);
            return 0;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            movement = GetComponent<PlayerMovement>();
            collisionOutlines = FindObjectsOfType<Collider2D>();
            if (visual != null) actorRenderer = visual.GetComponent<SpriteRenderer>();
            if (groundShadow != null) shadowRenderer = groundShadow.GetComponent<SpriteRenderer>();
            UpdatePresentation();
        }

        private void Update()
        {
            if (PreviewRunning) return;
            if (Input.GetKeyDown(KeyCode.R)) TeleportWorld(spawnWorld);
            if (Input.GetKeyDown(KeyCode.Alpha1)) follow = false;
            if (Input.GetKeyDown(KeyCode.Alpha2)) follow = true;
            if (Input.GetKeyDown(KeyCode.F3)) ShowCollision = !ShowCollision;
            if (viewCamera != null)
                overviewZoom = Mathf.Clamp(overviewZoom - Input.mouseScrollDelta.y * .35f, 4, 12);
        }

        private void LateUpdate()
        {
            UpdatePresentation();
            if (viewCamera != null)
            {
                Vector2 center = follow ? VisualFootPosition + Vector2.up * .8f : Vector2.zero;
                viewCamera.transform.position = new Vector3(center.x, center.y, -10);
                float widthFit = Mathf.Max(1, (16f / 9) / Mathf.Max(.01f, viewCamera.aspect));
                viewCamera.orthographicSize = overviewZoom * (follow ? 1 : widthFit);
            }
        }

        private void UpdatePresentation()
        {
            // Physics stays at the projected ground point; only the picture rises on the stairs.
            float heightOffset = Height * ProjectionUp.y;
            if (visual != null) visual.localPosition = new Vector3(0, heightOffset, 0);
            if (groundShadow != null)
            {
                Vector2 feet = VisualFootPosition;
                groundShadow.position = new Vector3(feet.x, feet.y, 0);
            }
            if (shadowRenderer != null) shadowRenderer.sortingOrder = 999;
            if (actorRenderer == null) return;
            actorRenderer.sortingOrder = 1000;
            Vector2 input = Movement.MoveInput;
            if (input.x != 0) actorRenderer.flipX = input.x < 0;
            float actualSpeed = Body.velocity.magnitude;
            if (actualSpeed < .05f)
            {
                animationPhase = 0;
                if (idle != null) actorRenderer.sprite = idle;
            }
            else
            {
                animationPhase += Time.deltaTime * 10 * actualSpeed / Mathf.Max(.01f, Movement.MoveSpeed);
                Sprite frame = ((int)animationPhase & 1) == 0 ? stepA : stepB;
                if (frame != null) actorRenderer.sprite = frame;
            }
        }

        public void TeleportWorld(Vector2 worldXZ)
        {
            Vector2 point = Project(new Vector3(worldXZ.x, 0, worldXZ.y));
            Body.position = point;
            transform.position = new Vector3(point.x, point.y, 0);
            Body.velocity = Vector2.zero;
            Body.angularVelocity = 0;
            animationPhase = 0;
            Physics2D.SyncTransforms();
            UpdatePresentation();
        }

        /// <summary>Input uses screen XY, just like the original 2D movement component.</summary>
        public void SetScriptedInput(Vector2 input) { Movement.SetScriptedInput(input); }
        public void ResumeInput() { Movement.ResumeKeyboardInput(); }

        private void OnGUI()
        {
            if (ShowCollision) DrawCollisionOutlines();
            if (titleStyle == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 23, normal = { textColor = new Color(1, .86f, .57f) } };
                textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true, normal = { textColor = new Color(.85f, .9f, .93f) } };
            }
            float ratio = Mathf.Max(.8f, Mathf.Min(Screen.width / 1600f, Screen.height / 900f));
            float panelWidth = Mathf.Min(840, Screen.width / ratio - 48);
            float textWidth = Mathf.Max(1, panelWidth - 40);
            string controls = "WASD / 方向键移动 · Shift 加速 · 滚轮缩放 · R 回到起点";
            string viewControls = "1 全景 / 2 跟随 · F3 空气墙轮廓：" + (ShowCollision ? "显示" : "隐藏");
            string explanation = "单张背景 + 空气墙 + 2D 遮挡 · 楼梯改变虚拟高度 · 运行时没有 3D 几何";
            float controlsHeight = Mathf.Max(24, textStyle.CalcHeight(new GUIContent(controls), textWidth));
            float viewHeight = Mathf.Max(24, textStyle.CalcHeight(new GUIContent(viewControls), textWidth));
            float explanationHeight = Mathf.Max(24, textStyle.CalcHeight(new GUIContent(explanation), textWidth));
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(ratio, ratio, 1));
            GUI.color = new Color(.04f, .07f, .1f, .88f);
            GUI.DrawTexture(new Rect(24, 22, panelWidth, 59 + controlsHeight + viewHeight + explanationHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(44, 32, textWidth, 38), "2D 平面地图 · 2.5D 对照小样", titleStyle);
            GUI.Label(new Rect(44, 75, textWidth, controlsHeight), controls, textStyle);
            GUI.Label(new Rect(44, 75 + controlsHeight, textWidth, viewHeight), viewControls, textStyle);
            GUI.Label(new Rect(44, 75 + controlsHeight + viewHeight, textWidth, explanationHeight), explanation, textStyle);
            Vector2 world = WorldPosition;
            GUI.Label(new Rect(26, Screen.height / ratio - 49, Mathf.Max(1, Screen.width / ratio - 52), 38),
                "纯 2D 碰撞  |  " + (follow ? "角色跟随" : "全景展示") + "  |  当前高度 " + Height.ToString("0.0") +
                " m  |  地面位置 " + world.x.ToString("0.0") + ", " + world.y.ToString("0.0"), textStyle);
            GUI.matrix = previous;
        }

        private void DrawCollisionOutlines()
        {
            if (viewCamera == null || collisionOutlines == null || Event.current.type != EventType.Repaint) return;
            foreach (Collider2D collider in collisionOutlines)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    collider.gameObject.scene != gameObject.scene) continue;
                if (collider is PolygonCollider2D polygon)
                {
                    for (int path = 0; path < polygon.pathCount; path++)
                        DrawColliderPath(polygon, polygon.GetPath(path), true);
                }
                else if (collider is EdgeCollider2D edge) DrawColliderPath(edge, edge.points, false);
            }
        }

        private void DrawColliderPath(Collider2D collider, Vector2[] points, bool closed)
        {
            if (points == null || points.Length < 2) return;
            int segments = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < segments; i++)
            {
                Vector3 startWorld = collider.transform.TransformPoint(points[i] + collider.offset);
                Vector3 endWorld = collider.transform.TransformPoint(points[(i + 1) % points.Length] + collider.offset);
                Vector3 start = viewCamera.WorldToScreenPoint(startWorld);
                Vector3 end = viewCamera.WorldToScreenPoint(endWorld);
                if (start.z <= 0 || end.z <= 0) continue;
                DrawScreenLine(new Vector2(start.x, Screen.height - start.y), new Vector2(end.x, Screen.height - end.y));
            }
        }

        private static void DrawScreenLine(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            if (delta.sqrMagnitude < .01f) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(.2f, 1, .45f, .85f);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - .75f, delta.magnitude, 1.5f), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
    }
}
