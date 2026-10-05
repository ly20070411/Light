using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Orthographic25D.Editor
{
    [InitializeOnLoad]
    public static class Orthographic25DSetup
    {
        public const string ScenePath = "Assets/Scenes/Orthographic25DDemo.unity";
        private const string Prefix = "Light.Orthographic25D.";
        private static double nextPoll;
        private static bool completing;
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result { public string id, state, message; public bool success; }
        static Orthographic25DSetup() { EditorApplication.update += Poll; EditorApplication.playModeStateChanged += PlayChanged; }

        [MenuItem("Tools/2.5D视角/打开占位小样")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("请先停止运行并等待导入完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存当前场景，再打开小样。");
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath);
            var camera = UnityEngine.Object.FindObjectOfType<Camera>();
            if (SceneView.lastActiveSceneView != null && camera != null) SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);
        }

        private static Material Material(string name, Color color)
        {
            const string folder = "Assets/Orthographic25D/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Orthographic25D", "Materials");
            string path = folder + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", .12f); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        [MenuItem("Tools/2.5D视角/创建占位小样")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式。");
            if (File.Exists(ScenePath)) return;
            var stone = Material("Stone", new Color(.42f, .46f, .44f)); var light = Material("StoneLight", new Color(.54f, .57f, .52f));
            var dark = Material("StoneDark", new Color(.33f, .38f, .39f)); var sand = Material("Path", new Color(.66f, .61f, .49f));
            var moss = Material("Moss", new Color(.3f, .42f, .32f)); var wood = Material("Wood", new Color(.42f, .27f, .17f));
            var metal = Material("Rail", new Color(.2f, .23f, .23f)); var shadow = Material("FootShadow", new Color(.21f, .24f, .24f));
            var flame = Material("WarmLight", new Color(1, .64f, .19f)); flame.EnableKeyword("_EMISSION"); flame.SetColor("_EmissionColor", new Color(1, .45f, .08f)); EditorUtility.SetDirty(flame);
            Scene previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); var root = new GameObject("2.5D 占位地图").transform;
                var floor = Shape(root, "地面碰撞", PrimitiveType.Cube, new Vector3(0, -.22f, 0), new Vector3(18, .4f, 15), dark);
                // Keep the collision surface at zero, with the foundation mesh below the tiles.
                floor.GetComponent<BoxCollider>().center = new Vector3(0, .05f, 0);
                for (int x = -9; x < 9; x++) for (int z = -7; z < 8; z++)
                {
                    bool path = x > 1 && x < 5 || z < -2 && z > -5;
                    Shape(root, "地砖 " + x + "," + z, PrimitiveType.Cube, new Vector3(x + .5f, -.01f, z), new Vector3(.98f, .02f, .98f), path ? sand : (x + z) % 3 == 0 ? light : stone, false);
                }
                Shape(root, "高台", PrimitiveType.Cube, new Vector3(-1, .8f, 3.25f), new Vector3(6, 1.6f, 4.5f), dark);
                Shape(root, "高台铺面", PrimitiveType.Cube, new Vector3(-1, 1.61f, 3.25f), new Vector3(6, .02f, 4.5f), light, false);
                for (int i = 0; i < 8; i++)
                    Shape(root, "台阶 " + (i + 1), PrimitiveType.Cube, new Vector3(.5f, (i + 1) * .1f, -2.75f + i * .5f), new Vector3(1.8f, (i + 1) * .2f, .5f), i % 2 == 0 ? light : stone);
                Shape(root, "后墙", PrimitiveType.Cube, new Vector3(-1, 1.9f, 5.8f), new Vector3(11, 3.8f, .45f), stone);
                Shape(root, "后墙压顶", PrimitiveType.Cube, new Vector3(-1, 3.87f, 5.8f), new Vector3(11.2f, .16f, .58f), light);
                for (int i = 0; i < 4; i++)
                {
                    float x = -5.7f + i * 3.2f;
                    Shape(root, "墙柱", PrimitiveType.Cube, new Vector3(x, 2, 5.48f), new Vector3(.52f, 4, .72f), dark);
                    Shape(root, "柱帽", PrimitiveType.Cube, new Vector3(x, 4.05f, 5.48f), new Vector3(.7f, .16f, .85f), light);
                }
                // The stair opening stays clear; rails show depth and allow walking along the platform.
                Rail(root, new Vector3(-2.2f, 1.6f, 1.06f), 3.6f, metal);
                Rail(root, new Vector3(1.75f, 1.6f, 3.1f), 4, metal, true);
                Shape(root, "矮墙", PrimitiveType.Cube, new Vector3(-6.7f, .65f, -.5f), new Vector3(.45f, 1.3f, 7), stone);
                Shape(root, "矮墙压顶", PrimitiveType.Cube, new Vector3(-6.7f, 1.34f, -.5f), new Vector3(.6f, .12f, 7.1f), light);
                foreach (var position in new[] { new Vector3(-4.5f, 0, -3.8f), new Vector3(-4.5f, 0, -2.8f), new Vector3(5.5f, 0, 2.1f), new Vector3(-2.7f, 1.6f, 4.5f) })
                {
                    Shape(root, "木桶", PrimitiveType.Cylinder, position + Vector3.up * .42f, new Vector3(.75f, .42f, .75f), wood);
                    Shape(root, "桶箍", PrimitiveType.Cylinder, position + Vector3.up * .55f, new Vector3(.78f, .04f, .78f), metal, false);
                }
                Shape(root, "木箱", PrimitiveType.Cube, new Vector3(5.8f, .5f, -.9f), Vector3.one, wood);
                Shape(root, "叠放木箱", PrimitiveType.Cube, new Vector3(5.8f, 1.25f, -.9f), Vector3.one * .65f, wood);
                foreach (var position in new[] { new Vector3(-5.8f, .015f, 2), new Vector3(5.8f, .015f, 4), new Vector3(-3.5f, .015f, -5.2f) })
                    Shape(root, "苔藓地块", PrimitiveType.Cylinder, position, new Vector3(2, .014f, 1.4f), moss, false);
                Lamp(root, new Vector3(-1.6f, 1.6f, 2.2f), metal, flame); Lamp(root, new Vector3(4.5f, 0, -3.2f), metal, flame);
                var sun = new GameObject("柔和主光").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.1f;
                sun.color = new Color(1, .9f, .74f); sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -35, 0);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.43f, .48f, .55f);
                var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 8.3f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .11f, .14f); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.transform.rotation = Quaternion.Euler(35, 45, 0); camera.transform.position = new Vector3(0, .8f, 0) - camera.transform.forward * 24; camera.gameObject.AddComponent<AudioListener>();
                var player = new GameObject("主角（2D Sprite / 3D 碰撞）"); player.transform.position = new Vector3(3, .05f, -4);
                var body = player.AddComponent<CharacterController>(); body.height = 1.7f; body.radius = .27f; body.center = new Vector3(0, .87f, 0); body.stepOffset = .28f; body.slopeLimit = 50; body.skinWidth = .03f;
                var demo = player.AddComponent<Orthographic25DDemo>(); demo.viewCamera = camera;
                demo.idle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/HeroIdle.png"); demo.stepA = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/HeroStepA.png"); demo.stepB = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/HeroStepB.png");
                var visual = new GameObject("角色立绘"); visual.transform.SetParent(player.transform); var sr = visual.AddComponent<SpriteRenderer>(); sr.sprite = demo.idle;
                float scale = 1.9f / sr.sprite.bounds.size.y; visual.transform.localScale = Vector3.one * scale; visual.transform.rotation = camera.transform.rotation;
                visual.transform.position = player.transform.position + Vector3.up * .02f; demo.visual = visual.transform;
                demo.groundShadow = Shape(root, "角色脚底影", PrimitiveType.Cylinder, player.transform.position + Vector3.up * .025f, new Vector3(.65f, .012f, .48f), shadow, false).transform;
                Shape(root, "起点标记", PrimitiveType.Cylinder, new Vector3(3, .011f, -4), new Vector3(1.3f, .008f, 1.3f), light, false);
                EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
        private static void Rail(Transform root, Vector3 position, float length, Material material, bool alongZ = false)
        {
            for (int i = 0; i < 4; i++)
            {
                var offset = alongZ ? Vector3.forward * (i / 3f - .5f) * length : Vector3.right * (i / 3f - .5f) * length;
                Shape(root, "栏杆柱", PrimitiveType.Cube, position + offset + Vector3.up * .45f, new Vector3(.07f, .9f, .07f), material);
            }
            Shape(root, "栏杆扶手", PrimitiveType.Cube, position + Vector3.up * .87f, alongZ ? new Vector3(.07f, .06f, length) : new Vector3(length, .06f, .07f), material);
        }
        private static void Lamp(Transform root, Vector3 position, Material pole, Material flame)
        {
            Shape(root, "灯柱", PrimitiveType.Cube, position + Vector3.up * .65f, new Vector3(.12f, 1.3f, .12f), pole);
            Shape(root, "灯笼", PrimitiveType.Cube, position + Vector3.up * 1.4f, new Vector3(.32f, .35f, .32f), flame, false);
            var light = new GameObject("暖色灯光").AddComponent<Light>(); light.transform.SetParent(root); light.transform.position = position + Vector3.up * 1.5f;
            light.type = LightType.Point; light.color = new Color(1, .57f, .18f); light.range = 4; light.intensity = 2;
        }

        [MenuItem("Tools/2.5D视角/修整占位小样")]
        public static void RefreshLayout()
        {
            Open();
            var floor = GameObject.Find("地面碰撞");
            if (floor == null) throw new InvalidOperationException("小样缺少地面。");
            floor.transform.position = new Vector3(0, -.22f, 0);
            floor.GetComponent<BoxCollider>().center = new Vector3(0, .05f, 0);
            var demo = UnityEngine.Object.FindObjectOfType<Orthographic25DDemo>();
            demo.visual.position = demo.transform.position + Vector3.up * .02f;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
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
                Orthographic25DPreviewChecks.Completed -= CaptureCompleted; Orthographic25DPreviewChecks.Completed += CaptureCompleted;
                UnityEngine.Object.FindObjectOfType<Orthographic25DDemo>().gameObject.AddComponent<Orthographic25DPreviewChecks>();
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Prefix + "Isolated", false))
            {
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(Prefix + "PreviousSuppression", false)); SessionState.SetBool(Prefix + "Isolated", false);
                if (SessionState.GetBool(Prefix + "Capture", false))
                {
                    Orthographic25DPreviewChecks.Completed -= CaptureCompleted;
                    SessionState.SetBool(Prefix + "Capture", false);
                    WriteResult(SessionState.GetString(Prefix + "Id", ""), "complete", SessionState.GetBool(Prefix + "Passed", false), "占位场景与预览已生成。");
                }
            }
        }
        public static void CaptureCompleted(bool passed)
        { SessionState.SetBool(Prefix + "Passed", passed); completing = true; EditorApplication.ExitPlaymode(); }
        private static void Poll()
        {
            if (SessionState.GetBool(Prefix + "Capture", false))
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (!completing && EditorApplication.timeSinceStartup - SessionState.GetFloat(Prefix + "Started", 0) > 60) CaptureCompleted(false);
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7; string path = "Validation/orthographic25d-command.json"; if (!File.Exists(path)) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); if (command == null || string.IsNullOrEmpty(command.id) || command.id == SessionState.GetString(Prefix + "LastId", "")) return;
            SessionState.SetString(Prefix + "LastId", command.id);
            try
            {
                if (command.verb != "preview" && command.verb != "refresh") throw new InvalidOperationException("只支持 preview / refresh。");
                if (command.verb == "refresh") RefreshLayout(); else Open();
                SessionState.SetString(Prefix + "Id", command.id); SessionState.SetBool(Prefix + "Passed", false);
                SessionState.SetFloat(Prefix + "Started", (float)EditorApplication.timeSinceStartup); SessionState.SetBool(Prefix + "Capture", true); completing = false;
                WriteResult(command.id, "running", true, "正在检查移动、碰撞与台阶并捕获预览。"); EditorApplication.isPaused = false; EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { WriteResult(command.id, "rejected", false, e.Message); Debug.LogException(e); }
        }
        private static void WriteResult(string id, string state, bool success, string message)
        { Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/orthographic25d-command-result.json", JsonUtility.ToJson(new Result { id = id, state = state, success = success, message = message }, true)); }
    }
}
