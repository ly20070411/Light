using UnityEngine;

namespace PixelPrototype
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;
        [SerializeField] private bool limitToRoom = true;
        [SerializeField] private Vector2 roomMin = new Vector2(-15f, -10f);
        [SerializeField] private Vector2 roomMax = new Vector2(15f, 10f);

        private Camera viewCamera;
        private Vector3 smoothVelocity;
        public Transform Target => target;
        public Vector2 RoomMin => roomMin;
        public Vector2 RoomMax => roomMax;
        public Vector3 DesiredPosition => GetDesiredPosition();

        private void Awake() { viewCamera = GetComponent<Camera>(); }
        private void Start() { SnapToTarget(); }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = GetDesiredPosition();
            transform.position = smoothTime <= 0f ? desired : Vector3.SmoothDamp(
                transform.position, desired, ref smoothVelocity, smoothTime);
        }

        public void Configure(Transform followTarget, Vector2 min, Vector2 max)
        {
            target = followTarget;
            roomMin = min;
            roomMax = max;
            viewCamera = GetComponent<Camera>();
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = GetDesiredPosition();
            smoothVelocity = Vector3.zero;
        }

        private Vector3 GetDesiredPosition()
        {
            if (target == null) return transform.position;
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            Vector3 result = target.position + offset;
            if (!limitToRoom) return result;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            result.x = ClampInside(result.x, roomMin.x, roomMax.x, halfWidth);
            result.y = ClampInside(result.y, roomMin.y, roomMax.y, halfHeight);
            return result;
        }

        private static float ClampInside(float value, float min, float max, float extent)
        {
            // Center the view if its dimensions exceed the room dimensions.
            return max - min <= extent * 2f ? (min + max) * 0.5f :
                Mathf.Clamp(value, min + extent, max - extent);
        }
    }
}
