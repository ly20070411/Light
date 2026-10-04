using System;
using System.IO;

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.SceneManagement;

namespace PixelPrototype.Editor
{
    public static class PrototypeBootstrap
    {
        public const string ScenePath = "Assets/Scenes/PixelRoom.unity";
        private const string ArtPath = "Assets/Art/Generated";
        private const int Ppu = 16;

        [MenuItem("Pixel Prototype/Create Demo Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(ArtPath);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            Sprite[] floors = new Sprite[4];
            for (int i = 0; i < floors.Length; i++)
                floors[i] = MakeSprite("Floor" + i, 16, 16, new Vector2(0.5f, 0.5f), t => DrawFloor(t, i));
            Sprite wall = MakeSprite("Wall", 16, 24, new Vector2(0.5f, 8f / 24f), DrawWall);
            Sprite idle = MakeSprite("HeroIdle", 24, 32, new Vector2(0.5f, 4f / 32f), t => DrawHero(t, 0));
            Sprite stepA = MakeSprite("HeroStepA", 24, 32, new Vector2(0.5f, 4f / 32f), t => DrawHero(t, 1));
            Sprite stepB = MakeSprite("HeroStepB", 24, 32, new Vector2(0.5f, 4f / 32f), t => DrawHero(t, 2));
            Sprite shadow = MakeSprite("Shadow", 24, 10, new Vector2(0.5f, 0.5f), DrawShadow);
            Sprite table = MakeSprite("Table", 32, 32, new Vector2(0.5f, 6f / 32f), DrawTable);
            Sprite crate = MakeSprite("Crate", 16, 24, new Vector2(0.5f, 4f / 24f), DrawCrate);
            Sprite rug = MakeSprite("Rug", 64, 48, new Vector2(0.5f, 0.5f), DrawRug);
            Sprite torch = MakeSprite("Torch", 16, 24, new Vector2(0.5f, 0.5f), DrawTorch);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject room = new GameObject("Room (30 x 20 units)");
            GameObject floorRoot = new GameObject("Floor");
            floorRoot.transform.parent = room.transform;
            for (int y = -10; y < 10; y++)
            for (int x = -15; x < 15; x++)
            {
                int variant = (Math.Abs(x * 37 + y * 17) % 4);
                MakeRenderer("Tile", floors[variant], new Vector2(x + 0.5f, y + 0.5f), floorRoot.transform, -10000);
            }

            MakeRenderer("Starting rug", rug, new Vector2(-5f, -3f), room.transform, -9000);
            GameObject wallRoot = new GameObject("Walls (static 2D colliders)");
            wallRoot.transform.parent = room.transform;
            for (int x = -15; x < 15; x++)
            {
                WallTile(wall, new Vector2(x + 0.5f, -9.5f), wallRoot.transform);
                WallTile(wall, new Vector2(x + 0.5f, 9.5f), wallRoot.transform);
            }
            for (int y = -9; y < 9; y++)
            {
                WallTile(wall, new Vector2(-14.5f, y + 0.5f), wallRoot.transform);
                WallTile(wall, new Vector2(14.5f, y + 0.5f), wallRoot.transform);
            }

            GameObject props = new GameObject("Props (feet collision and Y sorting)");
            props.transform.parent = room.transform;
            MakeProp("Worktable", table, new Vector2(0f, 1.2f), new Vector2(1.75f, 0.6f), props.transform);
            MakeProp("Worktable east", table, new Vector2(7f, 3f), new Vector2(1.75f, 0.6f), props.transform);
            MakeProp("Worktable west", table, new Vector2(-9f, 4.2f), new Vector2(1.75f, 0.6f), props.transform);
            MakeProp("Crate west", crate, new Vector2(-8f, -0.5f), new Vector2(0.8f, 0.7f), props.transform);
            MakeProp("Crate east", crate, new Vector2(6f, -4f), new Vector2(0.8f, 0.7f), props.transform);
            MakeProp("Crate southeast", crate, new Vector2(9f, -6f), new Vector2(0.8f, 0.7f), props.transform);
            MakeProp("Crate northwest", crate, new Vector2(-11f, 5f), new Vector2(0.8f, 0.7f), props.transform);
            for (int x = -10; x <= 10; x += 5)
                MakeRenderer("Wall torch", torch, new Vector2(x, 9.8f), room.transform, -260);

            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(-5f, -3f, 0f);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.drag = 0f;
            body.angularDrag = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CapsuleCollider2D feetCollider = player.AddComponent<CapsuleCollider2D>();
            feetCollider.direction = CapsuleDirection2D.Horizontal;
            feetCollider.size = new Vector2(0.65f, 0.38f);
            feetCollider.offset = new Vector2(0f, 0.16f);
            PhysicsMaterial2D noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            string materialPath = "Assets/Art/NoFriction.physicsMaterial2D";
            if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath) != null)
                AssetDatabase.DeleteAsset(materialPath);
            AssetDatabase.CreateAsset(noFriction, materialPath);
            feetCollider.sharedMaterial = noFriction;
            PlayerMovement movement = player.AddComponent<PlayerMovement>();
            SpriteRenderer visual = MakeRenderer("Visual", idle, new Vector2(-5f, -3f), player.transform, 0);
            SetReference(visual.gameObject.AddComponent<FeetYSort>(), "feet", player.transform);
            PlayerVisual animation = visual.gameObject.AddComponent<PlayerVisual>();
            SetReference(animation, "movement", movement);
            SetReference(animation, "idle", idle);
            SetReference(animation, "stepA", stepA);
            SetReference(animation, "stepB", stepB);
            SpriteRenderer groundShadow = MakeRenderer("Ground shadow", shadow, new Vector2(-5f, -2.87f), player.transform, 0);
            FeetYSort shadowSort = groundShadow.gameObject.AddComponent<FeetYSort>();
            SetReference(shadowSort, "feet", player.transform);
            SetInt(shadowSort, "orderOffset", -2);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera view = cameraObject.AddComponent<Camera>();
            view.orthographic = true;
            view.orthographicSize = 180f / (2f * Ppu);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Hex("121b29");
            view.nearClipPlane = 0.1f;
            view.farClipPlane = 100f;
            view.allowHDR = false;
            view.allowMSAA = false;
            view.allowDynamicResolution = false;
            cameraObject.AddComponent<AudioListener>();
            CameraFollow follow = cameraObject.AddComponent<CameraFollow>();
            follow.Configure(player.transform);
            PrototypeMovementSetup.ConfigureCamera(view);
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            ConfigureLegacyInput();
            new GameObject("Instructions").AddComponent<DemoInstructions>();

            PrototypeMovementSetup.Configure(scene);
            Emerge.Props.Editor.PropBootstrap.InstallPlayer(scene);
            Emerge.GameFlow.Editor.GameFlowInstaller.InstallPlayer(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (var existingScene in EditorBuildSettings.scenes)
                if (existingScene.path != ScenePath) buildScenes.Add(existingScene);
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("PIXEL_PROTOTYPE_BOOTSTRAP_OK scene=" + ScenePath);
        }

        private static void ConfigureLegacyInput()
        {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings.Length == 0) return;
            SerializedObject serialized = new SerializedObject(settings[0]);
            SerializedProperty handling = serialized.FindProperty("activeInputHandler");
            if (handling != null) { handling.intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetReference(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object owner, string field, int value)
        {
            SerializedObject serialized = new SerializedObject(owner);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SpriteRenderer MakeRenderer(string name, Sprite sprite, Vector2 position, Transform parent, int order)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.parent = parent;
            gameObject.transform.position = position;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void WallTile(Sprite sprite, Vector2 position, Transform parent)
        {
            SpriteRenderer wall = MakeRenderer("Wall", sprite, position, parent, Mathf.RoundToInt(-position.y * 32f));
            wall.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        private static void MakeProp(string name, Sprite sprite, Vector2 position, Vector2 size, Transform parent)
        {
            SpriteRenderer prop = MakeRenderer(name, sprite, position, parent, Mathf.RoundToInt(-position.y * 32f));
            BoxCollider2D collider = prop.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.offset = new Vector2(0f, 0.25f);
        }

        private static Sprite MakeSprite(string name, int width, int height, Vector2 pivot, Action<Texture2D> draw)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.SetPixels(new Color[width * height]);
            draw(texture);
            texture.Apply();
            string path = ArtPath + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            TextureImporterSettings importSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importSettings);
            importSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            importSettings.spritePivot = pivot;
            importSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(importSettings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }

        private static void Rect(Texture2D texture, int x, int y, int width, int height, string color)
        {
            Color c = Hex(color);
            for (int row = Mathf.Max(0, y); row < Mathf.Min(texture.height, y + height); row++)
            for (int column = Mathf.Max(0, x); column < Mathf.Min(texture.width, x + width); column++)
                texture.SetPixel(column, row, c);
        }

        private static void DrawFloor(Texture2D t, int variant)
        {
            Rect(t, 0, 0, 16, 16, variant % 2 == 0 ? "273b4b" : "2a3f4e");
            Rect(t, 0, 0, 16, 1, "20313f");
            Rect(t, 0, 0, 1, 16, "20313f");
            Rect(t, 1, 15, 15, 1, "354b5a");
            Rect(t, 15, 1, 1, 14, "314755");
            if (variant == 1) { Rect(t, 4, 9, 4, 1, "314755"); Rect(t, 7, 8, 1, 2, "20313f"); }
            if (variant == 2) { Rect(t, 10, 4, 3, 1, "354b5a"); Rect(t, 3, 12, 2, 1, "314755"); }
            if (variant == 3) { Rect(t, 3, 5, 1, 4, "20313f"); Rect(t, 4, 8, 3, 1, "20313f"); }
        }

        private static void DrawWall(Texture2D t)
        {
            Rect(t, 0, 0, 16, 23, "142230");
            Rect(t, 1, 2, 14, 14, "3b5061");
            Rect(t, 1, 3, 14, 2, "293b4d");
            Rect(t, 1, 9, 14, 1, "223446");
            Rect(t, 7, 10, 1, 6, "223446");
            Rect(t, 3, 3, 1, 6, "223446");
            Rect(t, 0, 16, 16, 7, "5a7182");
            Rect(t, 0, 22, 16, 1, "8194a0");
            Rect(t, 1, 17, 14, 1, "708695");
            Rect(t, 0, 15, 16, 1, "1d2c3a");
        }

        private static void DrawHero(Texture2D t, int frame)
        {
            int leftFoot = frame == 1 ? 5 : 4;
            int rightFoot = frame == 2 ? 5 : 4;
            Rect(t, 6, leftFoot, 5, 5, "182332");
            Rect(t, 13, rightFoot, 5, 5, "182332");
            Rect(t, 6, leftFoot, 5, 1, "79b8c9");
            Rect(t, 13, rightFoot, 5, 1, "79b8c9");
            Rect(t, 6, 8, 12, 12, "152837");
            Rect(t, 7, 9, 10, 11, "28aec1");
            Rect(t, 7, 16, 10, 3, "66d2d5");
            Rect(t, 7, 9, 10, 2, "187f95");
            Rect(t, 4, 12, 3, 7, "1f809a");
            Rect(t, 17, 12, 3, 7, "1f809a");
            Rect(t, 4, 11, 3, 3, "e8b280");
            Rect(t, 17, 11, 3, 3, "e8b280");
            Rect(t, 6, 19, 12, 11, "22283b");
            Rect(t, 7, 20, 10, 9, "f2c596");
            Rect(t, 7, 19, 10, 2, "d49a76");
            Rect(t, 5, 26, 14, 4, "3e3444");
            Rect(t, 7, 29, 10, 2, "6b4754");
            Rect(t, 5, 24, 4, 3, "3e3444");
            Rect(t, 15, 26, 4, 2, "3e3444");
            Rect(t, 9, 23, 2, 2, "192637");
            Rect(t, 14, 23, 2, 2, "192637");
            Rect(t, 9, 24, 1, 1, "ffffff");
            Rect(t, 14, 24, 1, 1, "ffffff");
            Rect(t, 11, 20, 3, 1, "b47467");
            Rect(t, 10, 15, 4, 1, "ffe5a6");
        }

        private static void DrawShadow(Texture2D t)
        {
            Color color = new Color(0.04f, 0.08f, 0.12f, 0.42f);
            for (int y = 0; y < 10; y++)
            for (int x = 0; x < 24; x++)
            {
                float dx = (x - 11.5f) / 10f;
                float dy = (y - 4.5f) / 3f;
                if (dx * dx + dy * dy <= 1f) t.SetPixel(x, y, color);
            }
        }

        private static void DrawTable(Texture2D t)
        {
            Rect(t, 3, 5, 5, 12, "442b32");
            Rect(t, 24, 5, 5, 12, "442b32");
            Rect(t, 4, 6, 3, 11, "875147");
            Rect(t, 25, 6, 3, 11, "875147");
            Rect(t, 0, 14, 32, 13, "412b34");
            Rect(t, 1, 16, 30, 10, "a7674d");
            Rect(t, 1, 24, 30, 2, "d09864");
            Rect(t, 1, 16, 30, 2, "754335");
            Rect(t, 3, 19, 26, 1, "b87852");
            Rect(t, 11, 18, 1, 6, "875147");
            Rect(t, 22, 18, 1, 6, "875147");
            Rect(t, 5, 22, 7, 6, "edf1cf");
            Rect(t, 6, 23, 5, 4, "bac6ba");
            Rect(t, 22, 22, 4, 5, "4589a8");
            Rect(t, 23, 25, 2, 2, "b9e3e4");
        }

        private static void DrawCrate(Texture2D t)
        {
            Rect(t, 0, 3, 16, 18, "3d2c35");
            Rect(t, 1, 4, 14, 16, "9b664e");
            Rect(t, 2, 5, 12, 9, "674635");
            Rect(t, 1, 18, 14, 2, "c49362");
            Rect(t, 2, 14, 12, 3, "b67e54");
            Rect(t, 2, 5, 2, 9, "c49362");
            Rect(t, 12, 5, 2, 9, "c49362");
            Rect(t, 2, 5, 12, 2, "a67451");
            for (int i = 0; i < 8; i++) Rect(t, 4 + i, 6 + i, 2, 2, "b38358");
            Rect(t, 2, 17, 1, 1, "edd4a0");
            Rect(t, 13, 17, 1, 1, "edd4a0");
        }

        private static void DrawRug(Texture2D t)
        {
            Rect(t, 0, 0, 64, 48, "1d293d");
            Rect(t, 1, 1, 62, 46, "536580");
            Rect(t, 3, 3, 58, 42, "324b65");
            Rect(t, 5, 5, 54, 38, "41617d");
            Rect(t, 8, 8, 48, 32, "304b67");
            for (int x = 12; x < 56; x += 8)
            for (int y = 12; y < 40; y += 8) Rect(t, x, y, 2, 2, "78949f");
        }

        private static void DrawTorch(Texture2D t)
        {
            Rect(t, 6, 2, 4, 10, "1a2533");
            Rect(t, 7, 3, 2, 9, "986b4c");
            Rect(t, 4, 10, 8, 3, "364350");
            Rect(t, 5, 13, 6, 6, "f07842");
            Rect(t, 6, 14, 4, 7, "ffd06b");
            Rect(t, 7, 14, 2, 4, "fff2bb");
            Rect(t, 7, 21, 1, 2, "ffd06b");
        }
    }
}
