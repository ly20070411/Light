using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.FlatMap25D.Editor
{
    [InitializeOnLoad]
    public static class FlatMap25DSetup
    {
        public const string ScenePath = "Assets/Scenes/FlatMap25DDemo.unity";
        private const string ArtPath = "Assets/FlatMap25D/Art/";
        private const string Prefix = "Light.FlatMap25D.";
        private const int Width = 1920, Height = 1080;
        private static double nextPoll;
        private static bool completing;
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result { public string id, state, message; public bool success; }
        private sealed class Piece
        {
            public string name, file;
            public Rect footprint;
            public float floor, top;
            public bool platform;
            public List<Renderer> renderers = new List<Renderer>();
        }

        static FlatMap25DSetup() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += PlayChanged; }

        [MenuItem("Tools/2.5D视角/打开2D地图小样")]
        public static void Open()
        {
            CheckReady();
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath);
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            if (camera != null && SceneView.lastActiveSceneView != null)
            { SceneView.lastActiveSceneView.in2DMode = true; SceneView.lastActiveSceneView.AlignViewToObject(camera.transform); }
        }
        private static void CheckReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请先停止运行并等待导入完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存当前场景。");
        }

        [MenuItem("Tools/2.5D视角/创建2D地图小样")]
        public static void Build() => Build(false);
        [MenuItem("Tools/2.5D视角/重新烘焙2D小样图片")]
        public static void Rebake() => Build(true);
        private static void Build(bool rebake)
        {
            CheckReady();
            bool existing = File.Exists(ScenePath);
            if (existing && !rebake) return;
            if (!File.Exists(Orthographic25D.Editor.Orthographic25DSetup.ScenePath)) Orthographic25D.Editor.Orthographic25DSetup.Build();
            Directory.CreateDirectory(ArtPath);
            Scene previous = SceneManager.GetActiveScene();
            Scene source = EditorSceneManager.OpenScene(Orthographic25D.Editor.Orthographic25DSetup.ScenePath, OpenSceneMode.Additive);
            var disabledLights = new List<Light>();
            var pieces = new List<Piece>();
            try
            {
                foreach (var light in UnityEngine.Object.FindObjectsOfType<Light>())
                    if (light.gameObject.scene != source && light.enabled) { light.enabled = false; disabledLights.Add(light); }
                var roots = source.GetRootGameObjects();
                Camera camera = null; Orthographic25D.Orthographic25DDemo original = null;
                foreach (var root in roots)
                {
                    if (root.GetComponent<Camera>() != null) camera = root.GetComponent<Camera>();
                    if (root.GetComponent<Orthographic25D.Orthographic25DDemo>() != null) original = root.GetComponent<Orthographic25D.Orthographic25DDemo>();
                }
                if (camera == null || original == null) throw new InvalidOperationException("3D 参考小样缺少相机或角色。");
                original.gameObject.SetActive(false); original.groundShadow.gameObject.SetActive(false);
                var environment = roots[0].transform;
                foreach (var root in roots) if (root.name == "2.5D 占位地图") environment = root.transform;
                foreach (var renderer in environment.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer = 30;
                foreach (var light in source.GetRootGameObjects())
                    foreach (var component in light.GetComponentsInChildren<Light>()) component.cullingMask = (1 << 30) | (1 << 31);
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.aspect = (float)Width / Height;
                Color32[] basePixels = Capture(camera); WriteSprite(basePixels, ArtPath + "Map.png");

                pieces.Add(PieceFor(environment, "高台前景", "Platform", new Rect(-4, 1, 6, 4.5f), 0, 1.62f, true, "高台", "高台铺面"));
                pieces.Add(PieceFor(environment, "后墙前景", "BackWall", new Rect(-6.5f, 5.575f, 11, .45f), 0, 4.15f, false, "后墙", "后墙压顶", "墙柱", "柱帽"));
                pieces.Add(PieceFor(environment, "矮墙前景", "LowWall", new Rect(-6.925f, -4, .45f, 7), 0, 1.4f, false, "矮墙", "矮墙压顶"));
                int barrelIndex = 0;
                foreach (Transform child in environment)
                {
                    if (child.name != "木桶") continue;
                    Vector3 p = child.position; float floor = p.y - .42f;
                    var piece = new Piece { name = "木桶前景 " + ++barrelIndex, file = "Barrel" + barrelIndex,
                        footprint = new Rect(p.x - .375f, p.z - .375f, .75f, .75f), floor = floor, top = floor + .84f };
                    piece.renderers.Add(child.GetComponent<Renderer>());
                    foreach (Transform ring in environment)
                        if (ring.name == "桶箍" && Mathf.Abs(ring.position.x - p.x) < .01f && Mathf.Abs(ring.position.z - p.z) < .01f)
                            piece.renderers.Add(ring.GetComponent<Renderer>());
                    pieces.Add(piece);
                }
                pieces.Add(PieceFor(environment, "木箱前景", "Crates", new Rect(5.3f, -1.4f, 1, 1), 0, 1.6f, false, "木箱", "叠放木箱"));
                var frontRail = new Piece { name = "高台前栏杆", file = "FrontRail", footprint = new Rect(-4, 1.02f, 3.6f, .08f), floor = 1.6f, top = 2.5f };
                var sideRail = new Piece { name = "高台侧栏杆", file = "SideRail", footprint = new Rect(1.71f, 1.1f, .08f, 4), floor = 1.6f, top = 2.5f };
                foreach (Transform child in environment)
                    if (child.name == "栏杆柱" || child.name == "栏杆扶手")
                        (Mathf.Abs(child.position.z - 1.06f) < .035f ? frontRail : sideRail).renderers.Add(child.GetComponent<Renderer>());
                pieces.Add(frontRail); pieces.Add(sideRail);
                foreach (var position in new[] { new Vector3(4.5f, 0, -3.2f), new Vector3(-1.6f, 1.6f, 2.2f) })
                {
                    var piece = new Piece { name = "灯柱前景", file = position.y > 0 ? "UpperLamp" : "GroundLamp",
                        footprint = new Rect(position.x - .06f, position.z - .06f, .12f, .12f), floor = position.y, top = position.y + 1.575f };
                    foreach (Transform child in environment)
                        if ((child.name == "灯柱" || child.name == "灯笼") && Mathf.Abs(child.position.x - position.x) < .01f && Mathf.Abs(child.position.z - position.z) < .01f)
                            piece.renderers.Add(child.GetComponent<Renderer>());
                    pieces.Add(piece);
                }
                var maskShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/FlatMap25D/Editor/VisibleMask.shader");
                if (maskShader == null) throw new InvalidOperationException("缺少前景可见性遮罩 Shader。");
                camera.SetReplacementShader(maskShader, "");
                foreach (var piece in pieces)
                {
                    var properties = new MaterialPropertyBlock(); properties.SetFloat("_FlatMapSelected", 1);
                    foreach (var renderer in piece.renderers) renderer.SetPropertyBlock(properties);
                    Color32[] mask = Capture(camera); var pixels = new Color32[basePixels.Length];
                    // Copy only pixels actually visible in the original map, retaining its exact lighting.
                    int visible = 0;
                    for (int i = 0; i < pixels.Length; i++) if (mask[i].r > 127) { pixels[i] = basePixels[i]; visible++; }
                    if (visible < 10) throw new InvalidOperationException("前景遮罩为空：" + piece.name);
                    WriteSprite(pixels, ArtPath + piece.file + ".png");
                    foreach (var renderer in piece.renderers) renderer.SetPropertyBlock(null);
                }
                camera.ResetReplacementShader();
            }
            finally
            {
                foreach (var light in disabledLights) if (light != null) light.enabled = true;
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(source, true);
            }
            pieces.Sort((a, b) =>
                FlatMap25DDemo.Project(new Vector3(b.footprint.center.x, b.floor, b.footprint.center.y)).y.CompareTo(
                FlatMap25DDemo.Project(new Vector3(a.footprint.center.x, a.floor, a.footprint.center.y)).y));
            if (!existing) BuildScene(previous, pieces);
        }

        private static Piece PieceFor(Transform environment, string name, string file, Rect footprint, float floor, float top, bool platform, params string[] names)
        {
            var result = new Piece { name = name, file = file, footprint = footprint, floor = floor, top = top, platform = platform };
            foreach (Transform child in environment)
                if (Array.IndexOf(names, child.name) >= 0) result.renderers.Add(child.GetComponent<Renderer>());
            return result;
        }
        private static Color32[] Capture(Camera camera)
        {
            var target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture active = RenderTexture.active; RenderTexture previous = camera.targetTexture;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(); return texture.GetPixels32();
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static void WriteSprite(Color32[] pixels, string path)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Height / 16.6f; importer.spritePivot = new Vector2(.5f, .5f);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
        }

        private static SpriteRenderer Sprite(Transform parent, string name, string path, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path); renderer.sortingOrder = order; return renderer;
        }
        private static void BuildScene(Scene previous, List<Piece> pieces)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); var root = new GameObject("2D 地图（背景与前景图层）").transform;
                Sprite(root, "整张地图背景", ArtPath + "Map.png", -1000);
                var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.orthographic = true; camera.orthographicSize = 8.3f; camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .11f, .14f); camera.gameObject.AddComponent<AudioListener>();
                var player = new GameObject("主角（Rigidbody2D / 虚拟高度）"); player.transform.position = FlatMap25DDemo.Project(new Vector3(3, 0, -4));
                var body = player.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var collider = player.AddComponent<CircleCollider2D>(); collider.radius = .15f;
                collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
                var movement = player.AddComponent<PixelPrototype.PlayerMovement>(); var serialized = new SerializedObject(movement);
                serialized.FindProperty("moveSpeed").floatValue = 3.5f; serialized.FindProperty("sprintMultiplier").floatValue = 1.6f; serialized.ApplyModifiedPropertiesWithoutUndo();
                var demo = player.AddComponent<FlatMap25DDemo>(); demo.viewCamera = camera;
                demo.idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/HeroIdle.png"); demo.stepA = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/HeroStepA.png"); demo.stepB = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/HeroStepB.png");
                var visual = Sprite(player.transform, "角色立绘", "Assets/Art/Generated/HeroIdle.png", 1000);
                visual.transform.localScale = Vector3.one * (1.9f / visual.sprite.bounds.size.y); demo.visual = visual.transform;
                var shadow = new GameObject("2D 脚底影").AddComponent<SpriteRenderer>(); shadow.transform.SetParent(player.transform, false);
                shadow.sprite = MakeShadow(); shadow.color = new Color(.14f, .17f, .17f, .65f); shadow.sortingOrder = 999;
                shadow.transform.localScale = new Vector3(.65f, .3f, 1); demo.groundShadow = shadow.transform;
                int index = 0;
                foreach (var piece in pieces)
                {
                    var renderer = Sprite(root, piece.name, ArtPath + piece.file + ".png", ++index);
                    var occluder = renderer.gameObject.AddComponent<FlatMap25DOccluder>(); occluder.actor = demo;
                    occluder.worldFootprint = piece.footprint; occluder.baseHeight = piece.floor; occluder.topHeight = piece.top;
                    occluder.platform = piece.platform; occluder.stableOrder = index;
                }
                var collision = new GameObject("空气实体（只有 2D Collider）").transform;
                var edge = collision.gameObject.AddComponent<EdgeCollider2D>(); var corners = Corners(new Rect(-9, -7.5f, 18, 15));
                edge.points = new[] { corners[0], corners[1], corners[2], corners[3], corners[0] };
                Wall(collision, "后墙占地", new Rect(-6.5f, 5.575f, 11, .45f)); Wall(collision, "矮墙占地", new Rect(-6.925f, -4, .45f, 7));
                Wall(collision, "高台左边缘", new Rect(-4.08f, 1, .16f, 4.5f)); Wall(collision, "高台右边缘", new Rect(1.92f, 1, .16f, 4.5f));
                Wall(collision, "高台后边缘", new Rect(-4, 5.42f, 6, .16f));
                Wall(collision, "高台前左边缘", new Rect(-4, .92f, 3.6f, .16f)); Wall(collision, "高台前右边缘", new Rect(1.4f, .92f, .6f, .16f));
                Wall(collision, "楼梯左边缘", new Rect(-.48f, -3, .16f, 4)); Wall(collision, "楼梯右边缘", new Rect(1.32f, -3, .16f, 4));
                foreach (var piece in pieces)
                    if (piece.file.StartsWith("Barrel") || piece.file == "Crates" || piece.file.Contains("Lamp")) Wall(collision, piece.name + "占地", piece.footprint);
                EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
        private static Vector2[] Corners(Rect rect) => new[] {
            FlatMap25DDemo.Project(new Vector3(rect.xMin, 0, rect.yMin)), FlatMap25DDemo.Project(new Vector3(rect.xMax, 0, rect.yMin)),
            FlatMap25DDemo.Project(new Vector3(rect.xMax, 0, rect.yMax)), FlatMap25DDemo.Project(new Vector3(rect.xMin, 0, rect.yMax)) };
        private static void Wall(Transform parent, string name, Rect rect)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); var collider = go.AddComponent<PolygonCollider2D>(); collider.points = Corners(rect); }
        private static Sprite MakeShadow()
        {
            const string path = ArtPath + "FootShadow.png";
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32));
                pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(32 - distance) * 255));
            }
            texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 64; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode && SceneManager.GetActiveScene().path == ScenePath)
            {
                SessionState.SetBool(Prefix + "Isolated", true); SessionState.SetBool(Prefix + "PreviousSuppression", SessionState.GetBool("Light.GameFlow.SuppressForValidation", false));
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            }
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Prefix + "Capture", false))
            {
                FlatMap25DPreviewChecks.Completed -= CaptureCompleted; FlatMap25DPreviewChecks.Completed += CaptureCompleted;
                UnityEngine.Object.FindObjectOfType<FlatMap25DDemo>().gameObject.AddComponent<FlatMap25DPreviewChecks>();
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "Isolated", false))
            {
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(Prefix + "PreviousSuppression", false)); SessionState.SetBool(Prefix + "Isolated", false);
                if (SessionState.GetBool(Prefix + "Capture", false))
                {
                    FlatMap25DPreviewChecks.Completed -= CaptureCompleted; SessionState.SetBool(Prefix + "Capture", false);
                    WriteResult(SessionState.GetString(Prefix + "Id", ""), "complete", SessionState.GetBool(Prefix + "Passed", false), "2D 地图小样与预览已生成。");
                }
            }
        }
        private static void CaptureCompleted(bool passed)
        { SessionState.SetBool(Prefix + "Passed", passed); completing = true; EditorApplication.ExitPlaymode(); }
        private static void Poll()
        {
            if (SessionState.GetBool(Prefix + "Capture", false))
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (!completing && EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0) > 75) CaptureCompleted(false);
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7; const string path = "Validation/flatmap25d-command.json"; if (!File.Exists(path)) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
            if (command == null || string.IsNullOrEmpty(command.id) || command.id == SessionState.GetString(Prefix + "LastId", "")) return;
            SessionState.SetString(Prefix + "LastId", command.id);
            try
            {
                if (command.verb != "preview" && command.verb != "rebake") throw new InvalidOperationException("只支持 preview / rebake。");
                if (command.verb == "rebake") Rebake();
                Open(); SessionState.SetString(Prefix + "Id", command.id); SessionState.SetBool(Prefix + "Passed", false);
                SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup); SessionState.SetBool(Prefix + "Capture", true); completing = false;
                WriteResult(command.id, "running", true, "正在验证 2D 空气墙、楼梯高度和遮挡。"); EditorApplication.isPaused = false; EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { WriteResult(command.id, "rejected", false, e.Message); Debug.LogException(e); }
        }
        private static void WriteResult(string id, string state, bool success, string message)
        { Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/flatmap25d-command-result.json", JsonUtility.ToJson(new Result { id = id, state = state, success = success, message = message }, true)); }
    }
}
