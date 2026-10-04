using UnityEngine;

namespace PixelPrototype
{
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        public Transform Target => target;
        public Vector3 DesiredPosition => target != null ? target.position + offset : transform.position;

        private void Start() { SnapToTarget(); }

        private void LateUpdate()
        {
            // Follow the same interpolated Transform that the character renders with.
            SnapToTarget();
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target != null) transform.position = target.position + offset;
        }
    }
}
