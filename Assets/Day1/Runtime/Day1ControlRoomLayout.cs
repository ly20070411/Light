using UnityEngine;
using Emerge.Props;

namespace Emerge.Day1
{
    /// <summary>Scene-local art binding; shared prop definitions and all other scenes stay independent.</summary>
    [ExecuteAlways, DefaultExecutionOrder(10010), DisallowMultipleComponent]
    public sealed class Day1ControlRoomLayout : MonoBehaviour
    {
        public Texture2D sourceArtwork;
        public float pixelsPerUnit = 320;
        public Vector2 artworkCenter = new Vector2(2, 0);
        public PropInstance[] paintedInteractions;
        public Vector2[] assemblyOffsets = { new Vector2(-1.5f, 1.2f), new Vector2(1.4f, 1.2f),
            new Vector2(-2.3f, -.1f), new Vector2(2.3f, -.1f), new Vector2(-2.5f, -1.7f), new Vector2(2.5f, -1.7f) };
        public Vector2 PixelToWorld(Vector2 pixel) => artworkCenter +
            new Vector2(pixel.x - 2500, 3000 - pixel.y) / pixelsPerUnit;

        private void LateUpdate()
        {
            // PropInstance may regenerate its visuals after a definition edit or a scene reload.
            // Hide only these scene instances; keep interaction, save IDs and definitions intact.
            if (paintedInteractions == null) return;
            foreach (var prop in paintedInteractions)
            {
                if (prop == null) continue;
                foreach (var renderer in prop.GetComponentsInChildren<SpriteRenderer>()) renderer.enabled = false;
                if (prop.SolidCollider != null) prop.SolidCollider.enabled = false;
            }
        }

        public Vector2 LegacyDoor(Vector2 point)
        {
            if (Mathf.Abs(point.x + 3) < .01f) return point.y > 3 ? new Vector2(-7,4.33f) : new Vector2(-7,-.7f);
            if (Mathf.Abs(point.x - 7) < .01f) return new Vector2(10,0);
            if (Mathf.Abs(point.y - 5) < .01f) return new Vector2(2,7);
            if (Mathf.Abs(point.y + 3) < .01f) return new Vector2(2,-9);
            return point + Vector2.left * 4;
        }

        public Vector2 LegacyDoorDirection(Vector2 point, Vector2 direction)
        {
            if (Mathf.Abs(point.x + 3) < .01f)
                return point.y > 3 ? new Vector2(5.52f,-1.72f).normalized : new Vector2(-3.43f,1.25f).normalized;
            if (Mathf.Abs(point.x - 7) < .01f) return new Vector2(2.425f,2.25f).normalized;
            if (Mathf.Abs(point.y - 5) < .01f) return Vector2.up;
            if (Mathf.Abs(point.y + 3) < .01f) return Vector2.down;
            return direction;
        }

        public string Area(Vector2 p)
        {
            if (p.x < -13 && p.y < -10) return "码头";
            if (p.x < -13) return "科考站附近";
            if (p.x > 9.7f) return "仓储区";
            if (p.y < -8.6f) return "科研区 · 化学分析室";
            if (p.x < -6.8f) return p.y > 3 ? "认知缓冲间" : "机房";
            return p.y > 6.8f ? "生活区 · 餐厨走廊" : "总控室";
        }
    }
}
