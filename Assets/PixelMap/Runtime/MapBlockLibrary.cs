using System.Collections.Generic;
using UnityEngine;

namespace Emerge.PixelMap
{
    [CreateAssetMenu(fileName = "BlockLibrary", menuName = "Emerge/Pixel Map/Block Library")]
    public sealed class MapBlockLibrary : ScriptableObject
    {
        [SerializeField] private Vector2 gridSize = Vector2.one;
        [SerializeField] private float defaultZ;
        [SerializeField] private string mapRootName = "Pixel Map";
        [SerializeField] private List<MapBlockDefinition> blocks = new List<MapBlockDefinition>();

        public Vector2 GridSize => gridSize;
        public float DefaultZ => defaultZ;
        public string MapRootName => string.IsNullOrWhiteSpace(mapRootName) ? "Pixel Map" : mapRootName;
        public IReadOnlyList<MapBlockDefinition> Blocks => blocks;

        public bool Add(MapBlockDefinition definition)
        {
            if (definition == null || blocks.Contains(definition)) return false;
            blocks.Add(definition);
            return true;
        }

        public bool Remove(MapBlockDefinition definition)
        {
            return definition != null && blocks.Remove(definition);
        }

        private void OnValidate()
        {
            gridSize.x = Mathf.Max(0.01f, gridSize.x);
            gridSize.y = Mathf.Max(0.01f, gridSize.y);
            blocks.RemoveAll(item => item == null);
        }
    }
}
