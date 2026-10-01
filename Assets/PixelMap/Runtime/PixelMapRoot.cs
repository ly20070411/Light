using UnityEngine;

namespace Emerge.PixelMap
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class PixelMapRoot : MonoBehaviour
    {
        [SerializeField] private MapBlockLibrary library;
        [SerializeField] private Vector2 gridSize = Vector2.one;
        [SerializeField] private float placementZ;

        public MapBlockLibrary Library => library;
        public Vector2 GridSize => gridSize;
        public float PlacementZ => placementZ;

        public void Configure(MapBlockLibrary newLibrary)
        {
            library = newLibrary;
            if (newLibrary == null) return;
            gridSize = newLibrary.GridSize;
            placementZ = newLibrary.DefaultZ;
        }

        private void OnValidate()
        {
            gridSize.x = Mathf.Max(0.01f, gridSize.x);
            gridSize.y = Mathf.Max(0.01f, gridSize.y);
        }
    }
}
