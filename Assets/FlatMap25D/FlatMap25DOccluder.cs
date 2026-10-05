using UnityEngine;

namespace Emerge.FlatMap25D
{
    /// <summary>Moves a transparent foreground cutout above or below the single actor.</summary>
    [DefaultExecutionOrder(200), DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class FlatMap25DOccluder : MonoBehaviour
    {
        public Rect worldFootprint;
        public float baseHeight, topHeight;
        public bool platform;
        [Range(0, 100)] public int stableOrder;
        public FlatMap25DDemo actor;
        private SpriteRenderer spriteRenderer;

        public bool ShouldCover => EvaluateCoverage();
        public bool IsCovering { get; private set; }
        public int CurrentSortingOrder => spriteRenderer != null ? spriteRenderer.sortingOrder : stableOrder;
        public Vector2 ProjectedFootPoint
        {
            get
            {
                return ProjectFootPoint(baseHeight);
            }
        }
        public Vector2 ProjectedGroundFootPoint => ProjectFootPoint(0);

        private Vector2 ProjectFootPoint(float height)
        {
            if (actor == null) return Vector2.zero;
            Vector2 world = actor.WorldPosition;
            float x = Mathf.Clamp(world.x, worldFootprint.xMin, worldFootprint.xMax);
            float z = Mathf.Clamp(world.y, worldFootprint.yMin, worldFootprint.yMax);
            return FlatMap25DDemo.Project(new Vector3(x, height, z));
        }

        private void Awake() { spriteRenderer = GetComponent<SpriteRenderer>(); }
        private void LateUpdate()
        {
            IsCovering = ShouldCover;
            spriteRenderer.sortingOrder = IsCovering ? 1001 + stableOrder : stableOrder;
        }

        private bool EvaluateCoverage()
        {
            if (actor == null) return false;
            float height = actor.Height;
            // The platform cutout contains its top and front together; raised actors stand on it.
            if (platform && height > .001f) return false;
            if (height >= topHeight - .001f) return false;
            // Ground depth keeps a raised actor in front of the wall when its ground point is closer.
            // Height only determines whether the actor is above the object; alpha limits overlap.
            return actor.Body.position.y > ProjectedGroundFootPoint.y + .001f;
        }
    }
}
