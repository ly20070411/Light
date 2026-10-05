#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.FlatMap25D
{
    /// <summary>Exercises the authored flat map in play mode and captures the actual Game view.</summary>
    public sealed class FlatMap25DPreviewChecks : MonoBehaviour
    {
        public static event Action<bool> Completed;

        [Serializable] private sealed class Check { public string name, observed; public bool passed; }
        [Serializable] private sealed class Report
        {
            public bool passed;
            public string completedUtc;
            public List<Check> checks = new List<Check>();
        }

        private readonly Report report = new Report();
        private readonly HashSet<string> contacts = new HashSet<string>();

        private IEnumerator Start()
        {
            Time.timeScale = 1;
            var demo = GetComponent<FlatMap25DDemo>();
            if (demo != null) demo.PreviewRunning = true;
            var checks = Run(demo);
            // Preserve an actionable report if an authored scene reference is missing.
            while (true)
            {
                bool more;
                object next = null;
                try { more = checks.MoveNext(); if (more) next = checks.Current; }
                catch (Exception exception)
                {
                    Add("Runtime checks complete without exceptions", false, exception.ToString());
                    break;
                }
                if (!more) break;
                yield return next;
            }

            if (demo != null)
            {
                demo.SetScriptedInput(Vector2.zero);
                demo.TeleportWorld(demo.spawnWorld);
                demo.follow = false;
                demo.ShowCollision = false;
                demo.PreviewRunning = false;
                demo.ResumeInput();
            }
            report.passed = report.checks.Count > 0 && report.checks.TrueForAll(check => check.passed);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/flatmap25d-results.json", JsonUtility.ToJson(report, true));
            Debug.Log("FLATMAP25D_PREVIEW_" + (report.passed ? "PASS" : "FAIL"));
            Completed?.Invoke(report.passed);
        }

        private IEnumerator Run(FlatMap25DDemo demo)
        {
            Add("Flat-map controller exists", demo != null);
            if (demo == null) yield break;
            yield return null;
            yield return null;

            Scene scene = gameObject.scene;
            int meshes = CountInScene<MeshRenderer>(scene);
            int colliders3D = CountInScene<Collider>(scene);
            int controllers = CountInScene<CharacterController>(scene);
            Add("Runtime map contains no 3D mesh renderers or collision", meshes == 0 && colliders3D == 0 && controllers == 0,
                "MeshRenderer=" + meshes + "; Collider=" + colliders3D + "; CharacterController=" + controllers);

            Camera camera = demo.viewCamera;
            Add("Camera views the XY sprite map orthographically", camera != null && camera.orthographic &&
                Quaternion.Angle(camera.transform.rotation, Quaternion.identity) < .1f && camera.transform.position.z < -1,
                camera != null ? "rotation=" + camera.transform.eulerAngles + "; orthographic=" + camera.orthographic : "Missing camera");

            var actorRenderer = demo.visual != null ? demo.visual.GetComponent<SpriteRenderer>() : null;
            SpriteRenderer background = FindInScene<SpriteRenderer>(scene, "整张地图背景");
            FlatMap25DOccluder[] foregrounds = ComponentsInScene<FlatMap25DOccluder>(scene);
            bool foregroundSprites = foregrounds.Length > 0;
            foreach (var foreground in foregrounds)
            {
                var renderer = foreground.GetComponent<SpriteRenderer>();
                foregroundSprites &= renderer != null && renderer.sprite != null && renderer.enabled && foreground.actor == demo;
            }
            Add("A real background sprite and foreground sprite cutouts are present", background != null && background.sprite != null &&
                background.enabled && foregroundSprites && actorRenderer != null && actorRenderer.sprite != null,
                "foregrounds=" + foregrounds.Length + "; background=" + (background != null ? background.name : "missing"));
            Add("Preview is isolated from the real game and user saves", Emerge.GameFlow.GameSessionController.Instance == null);
            Add("Player moves with active 2D physics", demo.Body != null && demo.Body.simulated &&
                demo.Body.bodyType == RigidbodyType2D.Dynamic && Mathf.Abs(demo.Body.gravityScale) < .001f &&
                GetComponent<Collider2D>() != null && CountInScene<Collider2D>(scene) > 5,
                "Collider2D=" + CountInScene<Collider2D>(scene) + "; speed=" + demo.Movement.MoveSpeed.ToString("F2"));

            demo.follow = false;
            demo.ShowCollision = false;
            demo.TeleportWorld(demo.spawnWorld);
            Vector2 start = demo.Body.position;
            demo.SetScriptedInput(Vector2.right);
            yield return new WaitForSeconds(.6f);
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Vector2 delta = demo.Body.position - start;
            Add("Right input physically moves right in screen XY", delta.x > 1.7f && delta.x < 2.6f && Mathf.Abs(delta.y) < .12f,
                "Rigidbody2D displacement=" + delta);

            contacts.Clear();
            demo.TeleportWorld(new Vector2(8, -5));
            demo.SetScriptedInput(WorldDirection(Vector2.right));
            yield return new WaitForSeconds(.8f);
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Vector2 edgePosition = demo.WorldPosition;
            Add("Invisible 2D map boundary stops outward movement", edgePosition.x > 8.1f && edgePosition.x < 9 &&
                contacts.Contains("空气实体（只有 2D Collider）"),
                PositionAndContacts(edgePosition, demo.Height));

            contacts.Clear();
            demo.TeleportWorld(new Vector2(3.5f, 4.5f));
            demo.SetScriptedInput(WorldDirection(Vector2.up));
            yield return new WaitForSeconds(.7f);
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Vector2 wallPosition = demo.WorldPosition;
            Add("Back-wall 2D collider physically blocks walking through it", wallPosition.y > 4.8f && wallPosition.y < 5.575f &&
                contacts.Contains("后墙占地"), PositionAndContacts(wallPosition, demo.Height));

            contacts.Clear();
            demo.TeleportWorld(new Vector2(3.3f, 3.2f));
            demo.SetScriptedInput(WorldDirection(Vector2.left));
            yield return new WaitForSeconds(.65f);
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Vector2 sidePosition = demo.WorldPosition;
            Add("Raised platform cannot be entered through its side", sidePosition.x > 2 && sidePosition.x < 3 &&
                demo.Height < .01f && contacts.Contains("高台右边缘"), PositionAndContacts(sidePosition, demo.Height));

            demo.TeleportWorld(new Vector2(.5f, -3.5f));
            float initialHeight = demo.Height;
            float intermediateHeight = 0;
            float climbStarted = Time.time;
            demo.SetScriptedInput(WorldDirection(Vector2.up));
            while (demo.WorldPosition.y < 1.3f && Time.time - climbStarted < 3)
            {
                yield return new WaitForFixedUpdate();
                if (demo.Height > .2f && demo.Height < 1.4f) intermediateHeight = demo.Height;
            }
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
            yield return null;
            Vector2 platformPosition = demo.WorldPosition;
            Add("Walking up the stairs changes height and reaches the platform", initialHeight < .01f && intermediateHeight > .2f &&
                demo.Height > 1.5f && platformPosition.y >= 1.3f && Mathf.Abs(platformPosition.x - .5f) < .2f,
                "start height=" + initialHeight.ToString("F2") + "; stair height=" + intermediateHeight.ToString("F2") +
                "; position=" + platformPosition + "; final height=" + demo.Height.ToString("F2"));
            Add("Stair elevation changes the sprite while physics stays in XY", demo.visual != null &&
                demo.visual.localPosition.y > 1 && demo.VisualFootPosition.y > demo.Body.position.y + 1 &&
                Mathf.Abs(transform.position.z) < .001f,
                "visual local position=" + (demo.visual != null ? demo.visual.localPosition.ToString() : "missing"));
            FlatMap25DOccluder platform = FindInScene<FlatMap25DOccluder>(scene, "高台前景");
            Add("Player standing on the platform renders above the platform cutout", platform != null &&
                !platform.ShouldCover && !platform.IsCovering && actorRenderer != null && platform.CurrentSortingOrder < actorRenderer.sortingOrder,
                platform != null ? "cover=" + platform.IsCovering + "; order=" + platform.CurrentSortingOrder : "Missing platform cutout");

            float descentStarted = Time.time;
            float descendingHeight = 0;
            float platformHeight = demo.Height;
            demo.SetScriptedInput(WorldDirection(Vector2.down));
            while (demo.WorldPosition.y > -3.3f && Time.time - descentStarted < 3)
            {
                yield return new WaitForFixedUpdate();
                if (demo.Height > .2f && demo.Height < 1.4f) descendingHeight = demo.Height;
            }
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForFixedUpdate();
            Add("Walking down the stairs returns to ground height", platformHeight > 1.5f && descendingHeight > .2f &&
                demo.WorldPosition.y <= -3.3f && demo.Height < .01f,
                "platform height=" + platformHeight.ToString("F2") + "; stair height=" + descendingHeight.ToString("F2") +
                "; position=" + demo.WorldPosition + "; final height=" + demo.Height.ToString("F2"));

            demo.TeleportWorld(new Vector2(-1, 3));
            yield return null;
            yield return new WaitForEndOfFrame();
            FlatMap25DOccluder backWall = FindInScene<FlatMap25DOccluder>(scene, "后墙前景");
            Add("Raised actor in front of the back wall is not hidden by height alone", demo.Height > 1.5f && backWall != null &&
                !backWall.ShouldCover && !backWall.IsCovering && actorRenderer != null && backWall.CurrentSortingOrder < actorRenderer.sortingOrder,
                "position=" + demo.WorldPosition + "; height=" + demo.Height.ToString("F2") +
                "; cover=" + (backWall != null && backWall.IsCovering) + "; order=" + (backWall != null ? backWall.CurrentSortingOrder : -1));

            FlatMap25DOccluder barrel = FindInScene<FlatMap25DOccluder>(scene, "木桶前景 1");
            Add("Ground barrel occlusion test uses the authored barrel near (-4.5, -3.8)", barrel != null &&
                Vector2.Distance(barrel.worldFootprint.center, new Vector2(-4.5f, -3.8f)) < .05f && barrel.baseHeight < .01f,
                barrel != null ? barrel.worldFootprint.ToString() : "Missing ground barrel");
            if (barrel != null && actorRenderer != null)
            {
                // These positions stay outside the barrel's collider, on the same screen column.
                demo.TeleportWorld(new Vector2(-5.15f, -4.45f));
                yield return null;
                yield return new WaitForEndOfFrame();
                bool front = !barrel.ShouldCover && !barrel.IsCovering && barrel.CurrentSortingOrder < actorRenderer.sortingOrder &&
                    barrel.GetComponent<SpriteRenderer>().sortingOrder == barrel.CurrentSortingOrder;
                int frontOrder = barrel.CurrentSortingOrder;
                demo.TeleportWorld(new Vector2(-3.85f, -3.15f));
                yield return null;
                yield return new WaitForEndOfFrame();
                bool behind = barrel.ShouldCover && barrel.IsCovering && barrel.CurrentSortingOrder > actorRenderer.sortingOrder &&
                    barrel.GetComponent<SpriteRenderer>().sortingOrder == barrel.CurrentSortingOrder;
                Add("Ground barrel switches actual render order between front and back positions", front && behind &&
                    frontOrder != barrel.CurrentSortingOrder,
                    "front=" + front + " order=" + frontOrder + "; behind=" + behind + " order=" + barrel.CurrentSortingOrder +
                    "; actor order=" + actorRenderer.sortingOrder);
            }

            Directory.CreateDirectory("Validation");
            const string occlusionPath = "Validation/flatmap25d-occlusion.png";
            if (File.Exists(occlusionPath)) File.Delete(occlusionPath);
            demo.TeleportWorld(new Vector2(-6.3f, 0));
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForSeconds(.15f);
            yield return new WaitForEndOfFrame();
            FlatMap25DOccluder lowWall = FindInScene<FlatMap25DOccluder>(scene, "矮墙前景");
            Add("Low-wall foreground renders above the actor from its back side", lowWall != null && lowWall.ShouldCover &&
                lowWall.IsCovering && actorRenderer != null && lowWall.CurrentSortingOrder > actorRenderer.sortingOrder,
                "position=" + demo.WorldPosition + "; cover=" + (lowWall != null && lowWall.IsCovering));
            ScreenCapture.CaptureScreenshot(occlusionPath);
            yield return null;
            float captureStarted = Time.realtimeSinceStartup;
            while (!File.Exists(occlusionPath) && Time.realtimeSinceStartup - captureStarted < 2) yield return null;
            Add("Actual Game-view occlusion screenshot is written", File.Exists(occlusionPath) && new FileInfo(occlusionPath).Length > 1024, occlusionPath);

            const string previewPath = "Validation/flatmap25d-preview.png";
            if (File.Exists(previewPath)) File.Delete(previewPath);
            demo.TeleportWorld(demo.spawnWorld);
            demo.follow = false;
            demo.SetScriptedInput(Vector2.zero);
            yield return new WaitForSeconds(.25f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(previewPath);
            yield return null;
            captureStarted = Time.realtimeSinceStartup;
            while (!File.Exists(previewPath) && Time.realtimeSinceStartup - captureStarted < 2) yield return null;
            Add("Actual Game-view panorama screenshot is written", File.Exists(previewPath) && new FileInfo(previewPath).Length > 1024, previewPath);

            const string collisionPath = "Validation/flatmap25d-collision.png";
            if (File.Exists(collisionPath)) File.Delete(collisionPath);
            demo.ShowCollision = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(collisionPath);
            yield return null;
            captureStarted = Time.realtimeSinceStartup;
            while (!File.Exists(collisionPath) && Time.realtimeSinceStartup - captureStarted < 2) yield return null;
            Add("Actual Game-view screenshot shows the optional 2D air-wall outlines", File.Exists(collisionPath) &&
                new FileInfo(collisionPath).Length > 1024, collisionPath);
            demo.ShowCollision = false;
        }

        private static Vector2 WorldDirection(Vector2 worldXZ)
        {
            return (FlatMap25DDemo.Project(new Vector3(worldXZ.x, 0, worldXZ.y)) - FlatMap25DDemo.Project(Vector3.zero)).normalized;
        }

        private void OnCollisionEnter2D(Collision2D collision) { RememberContact(collision); }
        private void OnCollisionStay2D(Collision2D collision) { RememberContact(collision); }
        private void RememberContact(Collision2D collision)
        {
            if (collision.collider != null) contacts.Add(collision.collider.gameObject.name);
        }

        private string PositionAndContacts(Vector2 position, float height)
        {
            return "position=" + position + "; height=" + height.ToString("F2") + "; contacts=" + string.Join(", ", contacts);
        }

        private static T[] ComponentsInScene<T>(Scene scene) where T : Component
        {
            var components = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) components.AddRange(root.GetComponentsInChildren<T>(true));
            return components.ToArray();
        }

        private static int CountInScene<T>(Scene scene) where T : Component { return ComponentsInScene<T>(scene).Length; }
        private static T FindInScene<T>(Scene scene, string objectName) where T : Component
        {
            foreach (T component in ComponentsInScene<T>(scene)) if (component.gameObject.name == objectName) return component;
            return null;
        }

        private void Add(string name, bool passed, string observed = "")
        {
            report.checks.Add(new Check { name = name, passed = passed, observed = observed });
        }
    }
}
#endif
