using UnityEngine;

namespace Emerge.PixelMap
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MapBlockInstance : MonoBehaviour
    {
        [SerializeField] private MapBlockDefinition definition;
        [SerializeField] private MapPlacementMode placementMode;
        [SerializeField] private Vector2Int gridCoordinate;
        private bool pendingRefresh;

        public MapBlockDefinition Definition => definition;
        public MapPlacementMode PlacementMode => placementMode;
        public Vector2Int GridCoordinate => gridCoordinate;

        public void Configure(MapBlockDefinition newDefinition, MapPlacementMode newMode, Vector2Int newCoordinate)
        {
            definition = newDefinition;
            placementMode = newMode;
            gridCoordinate = newCoordinate;
            ApplyDefinition();
        }

        public void ApplyDefinition()
        {
            pendingRefresh = false;
            if (definition == null) return;

            gameObject.layer = definition.UnityLayer;
            try { gameObject.tag = definition.UnityTag; }
            catch (UnityException) { gameObject.tag = "Untagged"; }

            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<SpriteRenderer>();
            if (definition.Sprite != null) renderer.sprite = definition.Sprite;
            renderer.color = definition.Tint;
            renderer.sortingLayerName = definition.SortingLayerName;
            renderer.sortingOrder = definition.SortingOrder;
            if (definition.Material != null) renderer.sharedMaterial = definition.Material;

            Vector2 sourceSize = renderer.sprite != null ? renderer.sprite.bounds.size : Vector2.one;
            if (sourceSize.x <= 0f) sourceSize.x = 1f;
            if (sourceSize.y <= 0f) sourceSize.y = 1f;
            transform.localScale = new Vector3(
                definition.Size.x / sourceSize.x,
                definition.Size.y / sourceSize.y,
                1f);

            ApplyCollider(renderer.sprite);
        }

        private void ApplyCollider(Sprite currentSprite)
        {
            var colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            if (definition.ColliderShape == BlockColliderShape2D.None) return;

            Collider2D activeCollider;
            if (definition.ColliderShape == BlockColliderShape2D.Polygon)
            {
                activeCollider = GetComponent<PolygonCollider2D>();
                if (activeCollider == null) activeCollider = gameObject.AddComponent<PolygonCollider2D>();
            }
            else
            {
                var box = GetComponent<BoxCollider2D>();
                if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
                box.size = currentSprite != null ? currentSprite.bounds.size : Vector2.one;
                box.offset = currentSprite != null ? (Vector2)currentSprite.bounds.center : Vector2.zero;
                activeCollider = box;
            }

            activeCollider.enabled = true;
            activeCollider.isTrigger = definition.IsTrigger;
            activeCollider.sharedMaterial = definition.PhysicsMaterial;
        }

        private void OnValidate()
        {
            // Unity also invokes OnValidate while restoring serialized scenes. Layer,
            // collider and renderer changes must wait until its consistency check ends.
            pendingRefresh = true;
        }
        private void OnEnable() { pendingRefresh = true; }
        private void Update() { if (pendingRefresh) ApplyDefinition(); }
    }
}
