using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelPrototype
{
    /// <summary>Added only by the validation runner. Exercises the actual scene physics.</summary>
    public sealed class PrototypeSelfTest : MonoBehaviour
    {
        [Serializable] public sealed class Check
        {
            public string name;
            public bool passed;
            public string observed;
        }
        [Serializable] public sealed class Report
        {
            public string unityVersion;
            public string scene;
            public bool passed;
            public string completedUtc;
            public List<Check> checks = new List<Check>();
        }

        public static event Action<bool> Completed;
        public static string OutputPath;
        private readonly Report report = new Report();
        private PlayerMovement movement;
        private CameraFollow follow;

        private IEnumerator Start()
        {
            report.unityVersion = Application.unityVersion;
            report.scene = gameObject.scene.name;
            movement = FindObjectOfType<PlayerMovement>();
            follow = FindObjectOfType<CameraFollow>();
            Add("Required player and camera components", movement != null && follow != null, "Scene component lookup");
            if (movement == null || follow == null) { Finish(); yield break; }
            Add("Feet collider and zero gravity", movement.GetComponent<CapsuleCollider2D>() != null &&
                Mathf.Approximately(movement.Body.gravityScale, 0f) &&
                (movement.Body.constraints & RigidbodyConstraints2D.FreezeRotation) != 0, "Rigidbody2D and CapsuleCollider2D");

            // Fixed-frame stepping makes these tests use real FixedUpdate and Physics2D.
            yield return ResetPlayer(new Vector2(-6f, -5f));
            Vector2 start = movement.Body.position;
            movement.SetScriptedInput(Vector2.right);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 cardinalEnd = movement.Body.position;
            float cardinalDistance = Vector2.Distance(start, cardinalEnd);
            float expected = movement.MoveSpeed * Time.fixedDeltaTime * 30f;
            Add("Horizontal movement at configured speed", Mathf.Abs(cardinalDistance - expected) < 0.2f &&
                Mathf.Abs(cardinalEnd.y - start.y) < 0.05f,
                "Distance=" + cardinalDistance.ToString("F3") + "; expected=" + expected.ToString("F3"));

            yield return ResetPlayer(new Vector2(-6f, -5f));
            start = movement.Body.position;
            movement.SetScriptedInput(Vector2.one);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 diagonalDelta = movement.Body.position - start;
            Add("Diagonal speed is normalized", Mathf.Abs(diagonalDelta.magnitude - cardinalDistance) < 0.2f &&
                Mathf.Abs(diagonalDelta.x - diagonalDelta.y) < 0.05f,
                "Diagonal=" + diagonalDelta.magnitude.ToString("F3") + "; horizontal=" + cardinalDistance.ToString("F3"));

            yield return FixedFrames(3);
            Add("Releasing input stops movement", movement.Body.velocity.sqrMagnitude < 0.001f,
                "Velocity=" + movement.Body.velocity);

            yield return ResetPlayer(new Vector2(0f, -8f));
            movement.SetScriptedInput(Vector2.down);
            yield return FixedFrames(90);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 collisionPosition = movement.Body.position;
            Add("Outer wall blocks character", collisionPosition.y > -9.15f && collisionPosition.y < -8.6f &&
                Mathf.Abs(collisionPosition.x) < 0.1f, "Stopped at=" + collisionPosition);

            // Approach the actual table from its lower side.
            yield return ResetPlayer(new Vector2(0f, -1f));
            movement.SetScriptedInput(Vector2.up);
            yield return FixedFrames(60);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 tablePosition = movement.Body.position;
            Add("Table feet collider blocks character", tablePosition.y > 0.5f && tablePosition.y < 1.15f,
                "Stopped at=" + tablePosition);

            yield return ResetPlayer(new Vector2(-6f, -5f));
            follow.SnapToTarget();
            Vector3 initialCamera = follow.transform.position;
            movement.SetScriptedInput(Vector2.right);
            yield return FixedFrames(45);
            movement.SetScriptedInput(Vector2.zero);
            yield return FixedFrames(35);
            float cameraTravel = follow.transform.position.x - initialCamera.x;
            float cameraError = Vector3.Distance(follow.transform.position, follow.DesiredPosition);
            Add("Camera follows and settles", cameraTravel > 1.5f && cameraError < 0.15f,
                "Travel=" + cameraTravel.ToString("F3") + "; target error=" + cameraError.ToString("F3"));

            yield return ResetPlayer(new Vector2(13f, 8f));
            follow.SnapToTarget();
            yield return null;
            Camera camera = follow.GetComponent<Camera>();
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 cameraPosition = follow.transform.position;
            bool withinX = halfWidth * 2f > follow.RoomMax.x - follow.RoomMin.x ||
                (cameraPosition.x - halfWidth >= follow.RoomMin.x - 0.02f && cameraPosition.x + halfWidth <= follow.RoomMax.x + 0.02f);
            bool withinY = halfHeight * 2f > follow.RoomMax.y - follow.RoomMin.y ||
                (cameraPosition.y - halfHeight >= follow.RoomMin.y - 0.02f && cameraPosition.y + halfHeight <= follow.RoomMax.y + 0.02f);
            Add("Camera viewport stays within room", withinX && withinY, "Camera=" + cameraPosition + "; half extents=" + halfWidth + ", " + halfHeight);
            Add("Orthographic pixel camera baseline", camera.orthographic && !camera.allowMSAA && !camera.allowHDR,
                "Projection and anti-alias settings");
            Component pixelPerfect = null;
            foreach (Component component in camera.GetComponents<Component>())
                if (component != null && component.GetType().Name == "PixelPerfectCamera") pixelPerfect = component;
            Add("Pixel Perfect Camera installed", pixelPerfect != null,
                pixelPerfect == null ? "Missing pixel camera package/component" : pixelPerfect.GetType().FullName);

            movement.ResumeKeyboardInput();
            Finish();
        }

        private IEnumerator ResetPlayer(Vector2 position)
        {
            movement.SetScriptedInput(Vector2.zero);
            movement.Body.velocity = Vector2.zero;
            movement.Body.position = position;
            movement.transform.position = new Vector3(position.x, position.y, 0f);
            Physics2D.SyncTransforms();
            yield return FixedFrames(3);
        }

        private static IEnumerator FixedFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return new WaitForFixedUpdate();
        }

        private void Add(string name, bool passed, string observed)
        {
            report.checks.Add(new Check { name = name, passed = passed, observed = observed });
            Debug.Log("VALIDATION " + (passed ? "PASS" : "FAIL") + ": " + name + " / " + observed);
        }

        private void Finish()
        {
            report.passed = report.checks.TrueForAll(check => check.passed);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            if (string.IsNullOrEmpty(OutputPath))
                OutputPath = Path.Combine(Application.dataPath, "../Validation/playmode-results.json");
            string directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(OutputPath, JsonUtility.ToJson(report, true));
            Debug.Log("PIXEL_PROTOTYPE_PLAYMODE_" + (report.passed ? "PASS" : "FAIL") + " output=" + OutputPath);
            Completed?.Invoke(report.passed);
        }
    }
}
