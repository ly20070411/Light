using UnityEngine;
namespace Emerge.Checks
{
    [DisallowMultipleComponent]
    public sealed class PointConfigurationZone : MonoBehaviour
    {
        public bool warehouse;
        public Vector2 size = new Vector2(6, 5);
        public bool Contains(Vector3 position)
        { var p = transform.InverseTransformPoint(position); return Mathf.Abs(p.x) <= size.x / 2 && Mathf.Abs(p.y) <= size.y / 2; }
        private void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.matrix = transform.localToWorldMatrix; Gizmos.DrawWireCube(Vector3.zero, size); }
    }
}
