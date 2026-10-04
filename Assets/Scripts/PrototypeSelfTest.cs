using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PixelPrototype
{
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
        private bool sampleCamera;
        private int walkRenderSamples;
        private int sprintRenderSamples;
        private float maxViewportError;
        private float maxFollowError;

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
            Add("Physics interpolation and continuous collision", movement.Body.interpolation == RigidbodyInterpolation2D.Interpolate &&
                movement.Body.collisionDetectionMode == CollisionDetectionMode2D.Continuous, "Interpolated rendering and continuous collision detection");
            CompositeCollider2D walls = FindObjectOfType<CompositeCollider2D>();
            Add("Adjacent wall tiles share composite geometry", walls != null && walls.pathCount > 0 && walls.pathCount <= 4,
                walls != null ? "Merged paths=" + walls.pathCount : "Missing composite collider");
            PhysicsMaterial2D material = movement.GetComponent<Collider2D>().sharedMaterial;
            Add("Feet collision has no friction or bounce", material != null && Mathf.Approximately(material.friction, 0f) &&
                Mathf.Approximately(material.bounciness, 0f), "Feet physics material");

            yield return ResetPlayer(new Vector2(-6f, -5f));
            Vector2 start = movement.Body.position;
            movement.SetScriptedInput(Vector2.right);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 cardinalEnd = movement.Body.position;
            float cardinalDistance = Vector2.Distance(start, cardinalEnd);
            float expected = movement.MoveSpeed * Time.fixedDeltaTime * 30f;
            Add("Horizontal movement at configured speed", Mathf.Abs(cardinalDistance - expected) < 0.2f &&
                Mathf.Abs(cardinalEnd.y - start.y) < 0.05f, "Distance=" + cardinalDistance.ToString("F3") + "; expected=" + expected.ToString("F3"));

            yield return ResetPlayer(new Vector2(-6f, -5f));
            start = movement.Body.position;
            movement.SetScriptedInput(Vector2.one);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 diagonalDelta = movement.Body.position - start;
            Add("Walking diagonal speed is normalized", Mathf.Abs(diagonalDelta.magnitude - cardinalDistance) < 0.2f &&
                Mathf.Abs(diagonalDelta.x - diagonalDelta.y) < 0.05f, "Distance=" + diagonalDelta.magnitude.ToString("F3"));

            yield return ResetPlayer(new Vector2(-6f, -5f));
            start = movement.Body.position;
            movement.SetScriptedInput(Vector2.right, true);
            yield return FixedFrames(30);
            float sprintDistance = Vector2.Distance(start, movement.Body.position);
            Add("Holding sprint multiplies movement speed", movement.IsSprinting &&
                Mathf.Abs(sprintDistance - expected * movement.SprintMultiplier) < 0.2f,
                "Distance=" + sprintDistance.ToString("F3") + "; multiplier=" + movement.SprintMultiplier);
            movement.SetScriptedInput(Vector2.right, false);
            yield return FixedFrames(5);
            Add("Releasing sprint immediately restores walking speed", !movement.IsSprinting &&
                Mathf.Abs(movement.Body.velocity.magnitude - movement.MoveSpeed) < 0.05f, "Speed=" + movement.Body.velocity.magnitude.ToString("F3"));

            yield return ResetPlayer(new Vector2(-6f, -5f));
            start = movement.Body.position;
            movement.SetScriptedInput(Vector2.one, true);
            yield return FixedFrames(30);
            diagonalDelta = movement.Body.position - start;
            Add("Sprinting diagonal speed is normalized", Mathf.Abs(diagonalDelta.magnitude - sprintDistance) < 0.2f &&
                Mathf.Abs(diagonalDelta.x - diagonalDelta.y) < 0.05f, "Distance=" + diagonalDelta.magnitude.ToString("F3"));
            movement.SetScriptedInput(Vector2.zero, true);
            yield return FixedFrames(3);
            Add("Holding sprint without input does not move", movement.Body.velocity.sqrMagnitude < 0.001f, "Velocity=" + movement.Body.velocity);
            movement.SetScriptedInput(Vector2.zero);
            yield return FixedFrames(3);
            Add("Releasing movement stops the character", movement.Body.velocity.sqrMagnitude < 0.001f, "Velocity=" + movement.Body.velocity);

            yield return ResetPlayer(new Vector2(0f, -8f));
            movement.SetScriptedInput(Vector2.down, true);
            yield return FixedFrames(45);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 collisionPosition = movement.Body.position;
            Add("Sprinting into the wall does not tunnel", collisionPosition.y > -9.15f && collisionPosition.y < -8.6f &&
                Mathf.Abs(collisionPosition.x) < 0.1f, "Stopped at=" + collisionPosition);

            yield return ResetPlayer(new Vector2(-4f, -8.7f));
            start = movement.Body.position;
            movement.SetScriptedInput(new Vector2(1f, -1f), true);
            int stalledFrames = 0;
            float previousX = start.x;
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
                float x = movement.Body.position.x;
                if (i > 3 && x - previousX < 0.04f) stalledFrames++;
                previousX = x;
            }
            movement.SetScriptedInput(Vector2.zero);
            collisionPosition = movement.Body.position;
            Add("Diagonal sprint slides across wall tile seams", collisionPosition.x - start.x > 6f && stalledFrames == 0 &&
                collisionPosition.y > -9.15f && collisionPosition.y < -8.6f,
                "Travel=" + (collisionPosition.x - start.x).ToString("F3") + "; stalled physics frames=" + stalledFrames);

            yield return ResetPlayer(new Vector2(13f, -8f));
            movement.SetScriptedInput(new Vector2(1f, -1f), true);
            yield return FixedFrames(60);
            movement.SetScriptedInput(Vector2.zero);
            collisionPosition = movement.Body.position;
            Add("Sprint remains inside the room corner", collisionPosition.x < 13.8f && collisionPosition.x > 13f &&
                collisionPosition.y > -9.15f && collisionPosition.y < -8.6f, "Stopped at=" + collisionPosition);

            yield return ResetPlayer(new Vector2(0f, -1f));
            movement.SetScriptedInput(Vector2.up, true);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.zero);
            Vector2 tablePosition = movement.Body.position;
            Add("Sprinting into table feet stays blocked", tablePosition.y > 0.5f && tablePosition.y < 1.15f, "Stopped at=" + tablePosition);

            yield return ResetPlayer(new Vector2(-6f, -5f));
            Camera.onPostRender += ObserveCamera;
            sampleCamera = true;
            movement.SetScriptedInput(Vector2.right);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.right, true);
            yield return FixedFrames(30);
            movement.SetScriptedInput(Vector2.zero);
            yield return FixedFrames(3);
            sampleCamera = false;
            Camera.onPostRender -= ObserveCamera;
            Add("Camera has no follow lag while walking and sprinting", walkRenderSamples > 0 && sprintRenderSamples > 0 && maxFollowError < 0.0001f,
                "Max error=" + maxFollowError.ToString("F6") + "; rendered samples=" + walkRenderSamples + "/" + sprintRenderSamples);
            Add("Rendered character stays centred without pixel jitter", walkRenderSamples > 0 && sprintRenderSamples > 0 && maxViewportError < 0.0001f,
                "Max viewport error=" + maxViewportError.ToString("F6"));

            yield return ResetPlayer(new Vector2(13f, 8f));
            follow.SnapToTarget();
            yield return FixedFrames(3);
            Add("Camera follows the character at room edges", Vector3.Distance(follow.transform.position, follow.DesiredPosition) < 0.0001f &&
                Mathf.Abs(follow.transform.position.x - 13f) < 0.05f && Mathf.Abs(follow.transform.position.y - 8f) < 0.05f,
                "Camera=" + follow.transform.position);
            Camera camera = follow.GetComponent<Camera>();
            Add("Orthographic camera retains pixel texture baseline", camera.orthographic && !camera.allowMSAA && !camera.allowHDR, "Projection and anti-alias settings");
            bool pixelGridActive = false;
            foreach (MonoBehaviour component in camera.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().Name == "PixelPerfectCamera" && component.enabled) pixelGridActive = true;
            Add("Camera pixel-grid quantization is disabled", !pixelGridActive, "Smooth follow uses the character's interpolated position");

            movement.ResumeKeyboardInput();
            Finish();
        }

        private void ObserveCamera(Camera camera)
        {
            if (!sampleCamera || follow == null || camera != follow.GetComponent<Camera>()) return;
            Vector3 viewport = camera.WorldToViewportPoint(movement.transform.position);
            maxViewportError = Mathf.Max(maxViewportError, Vector2.Distance(new Vector2(viewport.x, viewport.y), Vector2.one * 0.5f));
            maxFollowError = Mathf.Max(maxFollowError, Vector3.Distance(camera.transform.position, follow.DesiredPosition));
            if (movement.IsSprinting) sprintRenderSamples++; else walkRenderSamples++;
        }

        private void OnDestroy() { Camera.onPostRender -= ObserveCamera; }

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
            if (string.IsNullOrEmpty(OutputPath)) OutputPath = Path.Combine(Application.dataPath, "../Validation/playmode-results.json");
            string directory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(OutputPath, JsonUtility.ToJson(report, true));
            Debug.Log("PIXEL_PROTOTYPE_PLAYMODE_" + (report.passed ? "PASS" : "FAIL") + " output=" + OutputPath);
            Completed?.Invoke(report.passed);
        }
    }
}
