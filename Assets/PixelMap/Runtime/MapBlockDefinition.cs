using System;
using UnityEngine;

namespace Emerge.PixelMap
{
    public enum MapPlacementMode
    {
        Free,
        Grid
    }

    public enum BlockColliderShape2D
    {
        None,
        Box,
        Polygon
    }

    [CreateAssetMenu(fileName = "BlockDefinition", menuName = "Emerge/Pixel Map/Block Definition")]
    public sealed class MapBlockDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private string displayName = "新方块";
        [SerializeField] private string category = "默认";
        [SerializeField] private Sprite sprite;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Material material;
        [SerializeField] private Color tint = Color.white;
        [SerializeField] private Vector2 size = Vector2.one;
        [SerializeField] private BlockColliderShape2D colliderShape = BlockColliderShape2D.Box;
        [SerializeField] private bool isTrigger;
        [SerializeField] private PhysicsMaterial2D physicsMaterial;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder;
        [SerializeField] private string unityTag = "Untagged";
        [SerializeField, Range(0, 31)] private int unityLayer;
        [SerializeField] private bool allowRotation = true;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Category => category;
        public Sprite Sprite => sprite;
        public GameObject Prefab => prefab;
        public Material Material => material;
        public Color Tint => tint;
        public Vector2 Size => size;
        public BlockColliderShape2D ColliderShape => colliderShape;
        public bool IsTrigger => isTrigger;
        public PhysicsMaterial2D PhysicsMaterial => physicsMaterial;
        public string SortingLayerName => sortingLayerName;
        public int SortingOrder => sortingOrder;
        public string UnityTag => unityTag;
        public int UnityLayer => unityLayer;
        public bool AllowRotation => allowRotation;

        public void ConfigureDefaults(string newDisplayName, Sprite newSprite, Color newTint,
            BlockColliderShape2D newColliderShape)
        {
            EnsureId();
            displayName = newDisplayName;
            sprite = newSprite;
            tint = newTint;
            colliderShape = newColliderShape;
            size = Vector2.one;
        }

        public void ConfigureImported(string newDisplayName, Sprite newSprite, GameObject newPrefab,
            Material newMaterial)
        {
            EnsureId();
            displayName = newDisplayName;
            sprite = newSprite;
            prefab = newPrefab;
            material = newMaterial;
            tint = Color.white;
            size = Vector2.one;
        }

        private void OnValidate()
        {
            EnsureId();
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
            unityLayer = Mathf.Clamp(unityLayer, 0, 31);
            if (string.IsNullOrWhiteSpace(sortingLayerName)) sortingLayerName = "Default";
            if (string.IsNullOrWhiteSpace(unityTag)) unityTag = "Untagged";
        }

        private void EnsureId()
        {
            if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
        }
    }
}
