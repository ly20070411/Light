using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelPrototype.Editor
{
    public static class PrototypeMovementSetup
    {
        [MenuItem("Pixel Prototype/Update Movement and Collisions")]
        public static void ApplyToCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating the scene.");
            Scene scene = SceneManager.GetActiveScene();
            Configure(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("PIXEL_MOVEMENT_SETUP_OK scene=" + scene.path);
        }

        public static void Configure(Scene scene)
        {
            PlayerMovement player = null;
            Transform walls = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (player == null) player = root.GetComponentInChildren<PlayerMovement>(true);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Walls (static 2D colliders)") walls = child;
            }
            if (player == null) throw new InvalidOperationException("Open PixelRoom or a scene containing PlayerMovement first.");

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
            if (material == null) throw new InvalidOperationException("NoFriction physics material is missing.");
            material.friction = 0f;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);

            if (walls != null)
            {
                Rigidbody2D wallBody = walls.GetComponent<Rigidbody2D>();
                if (wallBody == null) wallBody = walls.gameObject.AddComponent<Rigidbody2D>();
                wallBody.bodyType = RigidbodyType2D.Static;
                CompositeCollider2D composite = walls.GetComponent<CompositeCollider2D>();
                if (composite == null) composite = walls.gameObject.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.generationType = CompositeCollider2D.GenerationType.Manual;
                composite.sharedMaterial = material;
                foreach (BoxCollider2D tile in walls.GetComponentsInChildren<BoxCollider2D>())
                    tile.usedByComposite = true;
                composite.GenerateGeometry();
                composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (!collider.isTrigger && !collider.usedByComposite) collider.sharedMaterial = material;
                foreach (CameraFollow follow in root.GetComponentsInChildren<CameraFollow>(true))
                {
                    follow.Configure(follow.Target != null ? follow.Target : player.transform);
                    ConfigureCamera(follow.GetComponent<Camera>());
                }
            }
        }

        public static void ConfigureCamera(Camera camera)
        {
            float size = camera.orthographicSize;
            // Point-filtered textures retain the pixel style without rounding the camera independently.
            foreach (MonoBehaviour component in camera.GetComponents<MonoBehaviour>())
            {
                if (component == null || component.GetType().Name != "PixelPerfectCamera") continue;
                component.enabled = false;
                EditorUtility.SetDirty(component);
            }
            camera.orthographicSize = size;
            camera.ResetWorldToCameraMatrix();
        }
    }
}
