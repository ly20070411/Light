using UnityEngine;

namespace Emerge.Day1
{
    /// <summary>Fades only when the player's rendered silhouette overlaps a foreground face.</summary>
    [ExecuteAlways, DefaultExecutionOrder(10020), DisallowMultipleComponent]
    public sealed class Day1OcclusionFade : MonoBehaviour
    {
        public Transform actor;
        public Renderer surface;
        public Vector2[] silhouette;
        public Vector2 groundStart, groundEnd;
        [Range(.1f, .8f)] public float occludedAlpha = .28f;
        [Min(.01f)] public float fadeOutSeconds = .18f;
        [Min(.01f)] public float fadeInSeconds = .32f;
        [Min(0)] public float releaseDelay = .12f;
        [Min(0)] public float padding = .06f;
        public bool keepInForeground;
        public float CurrentAlpha { get; private set; } = 1;
        public bool IsOccluding { get; private set; }
        private SpriteRenderer actorRenderer;
        private MaterialPropertyBlock properties;
        private float clearFor;
        private static readonly int AlphaId = Shader.PropertyToID("_OcclusionAlpha");

        private void OnEnable() { CurrentAlpha = 1; Apply(); }
        private void OnDisable() { CurrentAlpha = 1; Apply(); }
        private void LateUpdate()
        {
            if (!Application.isPlaying) { CurrentAlpha = 1; Apply(); return; }
            if (actor == null || surface == null) return;
            if (actorRenderer == null) actorRenderer = actor.GetComponent<SpriteRenderer>();
            Vector2 feet = transform.InverseTransformPoint(actor.position);
            Vector2 edge = groundEnd - groundStart;
            float t = Mathf.Abs(edge.x) < .001f ? .5f : Mathf.Clamp01((feet.x - groundStart.x) / edge.x);
            Vector2 ground = Vector2.Lerp(groundStart, groundEnd, t);
            if (!keepInForeground)
                surface.sortingOrder = Mathf.RoundToInt(-transform.TransformPoint(ground).y * 32) + 2;
            IsOccluding = actorRenderer != null && actorRenderer.enabled && actor.gameObject.activeInHierarchy &&
                feet.y >= ground.y - .08f && Overlaps(actorRenderer.bounds);
            clearFor = IsOccluding ? 0 : clearFor + Time.unscaledDeltaTime;
            float target = IsOccluding || clearFor < releaseDelay ? occludedAlpha : 1;
            float duration = target < CurrentAlpha ? fadeOutSeconds : fadeInSeconds;
            CurrentAlpha = Mathf.MoveTowards(CurrentAlpha, target,
                Time.unscaledDeltaTime * (1 - occludedAlpha) / Mathf.Max(.01f, duration));
            Apply();
        }

        private void Apply()
        {
            if (surface == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(properties);
            properties.SetFloat(AlphaId, CurrentAlpha);
            if (actorRenderer != null)
            {
                Bounds bounds = actorRenderer.bounds;
                properties.SetFloat("_UseOcclusionFocus", 1);
                properties.SetVector("_OcclusionFocus", new Vector4(bounds.center.x, bounds.center.y,
                    Mathf.Max(.55f, bounds.extents.x + .22f), Mathf.Max(.8f, bounds.extents.y + .24f)));
            }
            surface.SetPropertyBlock(properties);
        }

        private bool Overlaps(Bounds bounds)
        {
            if (silhouette == null || silhouette.Length < 3) return false;
            Vector2 min = transform.InverseTransformPoint(bounds.min);
            Vector2 max = transform.InverseTransformPoint(bounds.max);
            var rect = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
            foreach (var p in silhouette) if (rect.Contains(p)) return true;
            var corners = new[] { rect.min, new Vector2(rect.xMax, rect.yMin), rect.max,
                new Vector2(rect.xMin, rect.yMax) };
            foreach (var corner in corners) if (Contains(corner)) return true;
            for (int i = 0; i < silhouette.Length; i++)
                for (int j = 0; j < 4; j++)
                    if (Intersects(silhouette[i], silhouette[(i + 1) % silhouette.Length], corners[j], corners[(j + 1) % 4])) return true;
            return false;
        }

        private bool Contains(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = silhouette.Length - 1; i < silhouette.Length; j = i++)
            {
                Vector2 a = silhouette[i], b = silhouette[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            Vector2 ab = b - a, cd = d - c;
            float denominator = Cross(ab, cd);
            if (Mathf.Abs(denominator) < .00001f) return false;
            float t = Cross(c - a, cd) / denominator, u = Cross(c - a, ab) / denominator;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
