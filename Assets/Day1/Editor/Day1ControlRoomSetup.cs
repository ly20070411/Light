using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.PixelMap;
using Emerge.Props;
using PixelPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Day1.Editor
{
    /// <summary>Installs the painted control room into Day1, preserving scene-local story identities.</summary>
    [InitializeOnLoad]
    public static class Day1ControlRoomSetup
    {
        public const string ArtPath = "Assets/美术资产/总控室（初稿）.png";
        public const string GeneratedPath = "Assets/美术资产/总控室场景";
        public const string RootName = "A02 总控室 · 正式美术与遮挡";
        private const string CommandPath = "Validation/controlroom-command.json";
        private static double nextPoll;
        private static Material artwork, backdrop, geometry;
        private static Day1ControlRoomLayout layout;
        private static int meshIndex;
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result { public string id, state, message; public bool success; }
        static Day1ControlRoomSetup() { EditorApplication.update += Poll; }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + .8;
            if (!File.Exists(CommandPath)) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(CommandPath));
            if (command == null || string.IsNullOrEmpty(command.id) ||
                SessionState.GetString("Light.Day1.ControlRoom.Command", "") == command.id) return;
            SessionState.SetString("Light.Day1.ControlRoom.Command", command.id);
            var result = new Result { id = command.id, state = "complete" };
            try
            {
                if (command.verb == "apply") Apply();
                else if (command.verb == "preview") Preview();
                else if (command.verb == "validate") { Day1ControlRoomValidation.Run(); }
                else throw new InvalidOperationException("Supported commands: apply / preview / validate");
                result.success = true; result.message = "总控室操作完成。";
            }
            catch (Exception e) { result.success = false; result.state = "failed"; result.message = e.ToString(); Debug.LogException(e); }
            File.WriteAllText("Validation/controlroom-command-result.json", JsonUtility.ToJson(result, true));
        }

        [MenuItem("Tools/剧情/Day1/应用总控室美术与遮挡")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play。");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("当前场景有未保存改动，已保留；请先保存。");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool alreadyOpen = SceneManager.GetActiveScene().path == Day1SceneSetup.ScenePath;
            var scene = alreadyOpen ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(Day1SceneSetup.ScenePath);
            try
            {
                ApplyToScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Cannot save Day1 scene.");
                AssetDatabase.SaveAssets();
            }
            finally { if (!alreadyOpen) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void ApplyToScene(Scene scene)
        {
            var flow = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Day1FlowController>(true)).Single();
            var map = scene.GetRootGameObjects().First(g => g.GetComponent<PixelMapRoot>() != null);
            var previous = map.transform.Find(RootName);
            bool firstInstall = previous == null;
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            EnsureFolder(GeneratedPath); EnsureFolder(GeneratedPath + "/Meshes");
            var importer = AssetImporter.GetAtPath(ArtPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing control room artwork: " + ArtPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 320; importer.maxTextureSize = 8192;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear; importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(textureSettings); importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath);
            var shader = Shader.Find("Emerge/Day1/ControlRoomArtwork");
            if (shader == null) throw new InvalidOperationException("Control room shader is unavailable.");
            artwork = Material("RoomArtwork", shader, texture); artwork.SetFloat("_OpenNorthDoor", 1);
            backdrop = Material("RoomBackdrop", shader, texture); backdrop.SetFloat("_Backdrop", 1); backdrop.SetFloat("_BlurRadius", .018f);
            geometry = Material("RoomWalls", shader, Texture2D.whiteTexture); geometry.SetFloat("_RemoveWhite", 0);
            meshIndex = 0;
            var root = Child(map.transform, RootName);
            layout = root.gameObject.AddComponent<Day1ControlRoomLayout>(); layout.sourceArtwork = texture;
            flow.controlRoom = layout;
            if (firstInstall) Reflow(map, flow);
            BuildArtwork(root);
            BuildRoomBoundaries(root, flow.actor.transform);
            BuildConnections(root);
            BuildColumn(root, flow.actor.transform);
            BindInteractions(flow);
            flow.assembly.position = new Vector3(2, 1.3f);
            map.name = "Day1 科考站与周边（总控室美术已接入）";
            Physics2D.SyncTransforms();
            Debug.Log("DAY1_CONTROL_ROOM_APPLIED: original 5:6 artwork ratio, five physical connecting corridors, scene-local occlusion fade.");
        }

        private static void Reflow(GameObject map, Day1FlowController flow)
        {
            var floors = map.transform.Find("Floors 地面（无碰撞）");
            foreach (Transform child in floors)
            {
                if (child.name.StartsWith("A02")) { child.gameObject.SetActive(false); continue; }
                if (child.name.StartsWith("A01") || child.name.StartsWith("A04")) child.position += Vector3.left * 4;
                else if (child.name.StartsWith("A03")) child.position += Vector3.up * 2;
                else if (child.name.StartsWith("A05")) child.position += Vector3.down * 6;
                else if (child.name.StartsWith("A06")) child.position += Vector3.right * 3;
                else child.position += Vector3.left * 4;
            }
            var walls = map.transform.Find("Walls 墙体与边界");
            foreach (Transform child in walls)
            {
                var block = child.GetComponent<MapBlockInstance>(); if (block == null) continue;
                Vector2Int p = block.GridCoordinate;
                Vector3 offset;
                if (p.y == 5 && p.x >= -3 && p.x <= 7) offset = Vector3.up * 2;
                else if (p.y == -3 && p.x >= -3 && p.x <= 7) offset = Vector3.down * 6;
                else if (p.x == -3 && p.y >= -3 && p.y <= 5) offset = Vector3.left * 4;
                else if (p.x == 7 && p.y >= -3 && p.y <= 5) offset = Vector3.right * 3;
                else if (p.y > 5 && p.x >= -3 && p.x <= 7) offset = Vector3.up * 2;
                else if (p.y < -3 && p.x >= -3 && p.x <= 7) offset = Vector3.down * 6;
                else offset = Offset((Vector2)p);
                child.position += offset;
            }
            var props = map.transform.Find("Props 现有 PixelRoom 道具");
            foreach (Transform child in props)
            {
                if (child.name.StartsWith("总控") || child.name.StartsWith("环境监测") || child.name.StartsWith("设备诊断") || child.name.StartsWith("中央立柱"))
                    child.gameObject.SetActive(false);
                else child.position += Offset(child.position);
            }
            foreach (Transform marker in map.transform.Find("Markers")) marker.position += Offset(marker.position);
            foreach (var point in flow.interactions) { point.transform.position += Offset(point.transform.position); point.homePosition = point.transform.position; }
            flow.actor.transform.position += Vector3.left * 4;
            if (flow.admissionGate != null) flow.admissionGate.transform.position += Vector3.left * 4;
            if (flow.outdoorGate != null) flow.outdoorGate.transform.position += Vector3.left * 4;
            var body = flow.actor.GetComponent<Rigidbody2D>(); if (body != null) body.position = flow.actor.transform.position;
            var camera = sceneCamera(flow.gameObject.scene); camera.GetComponent<CameraFollow>()?.SnapToTarget();
        }

        private static Vector3 Offset(Vector3 p)
        {
            if (p.x <= -3) return Vector3.left * 4;
            if (p.x >= 7) return Vector3.right * 3;
            if (p.y >= 5) return Vector3.up * 2;
            if (p.y <= -3) return Vector3.down * 6;
            return Vector3.zero;
        }

        private static void BuildArtwork(Transform root)
        {
            var corners = new[] { P(0, 6000), P(5000, 6000), P(5000, 0), P(0, 0) };
            var uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            var main = Surface(root, "总控室原图（白底去除 / 保持原比例）", corners, uv, null, artwork, -7000);
            var soft = Surface(root, "外围虚化过渡（室内不模糊）", corners, uv, null, backdrop, -7100);
            soft.transform.position = new Vector3(0, 0, .01f);
        }

        private static Vector2[] Floor => new[] { P(1690,1634), P(3310,1634), P(4460,2774), P(4460,4408),
            P(3310,5548), P(1690,5548), P(542,4408), P(542,2774) };

        private static void BuildRoomBoundaries(Transform root, Transform actor)
        {
            Vector2[] floor = Floor;
            var edges = Child(root, "房间占地碰撞（淡出不影响碰撞）");
            var foreground = Child(root, "前墙 Occlusion Fade");
            for (int edge = 0; edge < floor.Length; edge++)
            {
                Vector2 a = floor[edge], b = floor[(edge + 1) % floor.Length];
                float gapA = -1, gapB = -1;
                if (edge == 0 || edge == 4)
                { gapA = Mathf.InverseLerp(a.x, b.x, edge == 0 ? 1.22f : 2.78f); gapB = Mathf.InverseLerp(a.x, b.x, edge == 0 ? 2.78f : 1.22f); }
                else if (edge == 2 || edge == 6)
                { gapA = Mathf.InverseLerp(a.y, b.y, edge == 2 ? -.85f : -2.65f); gapB = Mathf.InverseLerp(a.y, b.y, edge == 2 ? -2.65f : -.85f); }
                else if (edge == 7) { gapA = .40f; gapB = .76f; }
                if (gapA >= 0)
                {
                    Edge(edges, foreground, actor, a, Vector2.Lerp(a, b, gapA), edge);
                    Edge(edges, foreground, actor, Vector2.Lerp(a, b, gapB), b, edge);
                }
                else Edge(edges, foreground, actor, a, b, edge);
            }
        }

        private static void Edge(Transform collisions, Transform foreground, Transform actor, Vector2 a, Vector2 b, int edge)
        {
            if (Vector2.Distance(a, b) < .01f) return;
            Boundary(collisions, "墙体占地 " + edge, a, b, .12f);
            if (edge < 3 || edge > 5) return;
            // Short independently fading pieces preserve the room silhouette away from the player.
            int count = Mathf.CeilToInt(Vector2.Distance(a,b) / 2.25f);
            for (int i = 0; i < count; i++)
                Wall(foreground, "前墙 " + edge + " · " + (i + 1),
                    Vector2.Lerp(a,b,(float)i/count), Vector2.Lerp(a,b,(float)(i+1)/count), 1.3f, actor);
        }

        private static void BuildConnections(Transform root)
        {
            var corridors = Child(root, "与 Day1 相邻区域衔接");
            Corridor(corridors, "缓冲间 ↔ 总控室", new Vector2(-7.55f,4.5f), P(1210,2110), 1.65f);
            Corridor(corridors, "机房 ↔ 总控室", new Vector2(-7.55f,-.5f), new Vector2(Floor[6].x,-1.75f), 1.65f);
            Corridor(corridors, "生活区 ↔ 总控室", new Vector2(2,7.55f), new Vector2(2,Floor[0].y-.15f), 1.5f);
            Corridor(corridors, "化学室 ↔ 总控室", new Vector2(2,-9.55f), new Vector2(2,Floor[4].y+.15f), 1.5f);
            Corridor(corridors, "仓储区 ↔ 总控室", new Vector2(10.55f,.5f), new Vector2(Floor[2].x,-1.75f), 1.65f);
        }

        private static void Corridor(Transform parent, string name, Vector2 a, Vector2 b, float width)
        {
            var root = Child(parent,name);
            Vector2 n = new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Vector2[] points = { a-n,b-n,b+n,a+n };
            Color roomFloor = new Color(.72f,.72f,.77f).linear;
            Color outerFloor = new Color(.42f,.45f,.51f).linear;
            Surface(root,"过渡地面", points, null, new[]{outerFloor,roomFloor,roomFloor,outerFloor}, geometry,-7050);
            // Stop rails slightly inside the neighboring doorway so existing grid jambs remain clear.
            Vector2 start = Vector2.Lerp(a,b,.08f), end = Vector2.Lerp(a,b,.93f);
            Boundary(root,"走廊左边界",start-n,end-n,.09f);
            Boundary(root,"走廊右边界",start+n,end+n,.09f);
            Stripe(root,"走廊边缘",start-n,end-n,.025f,new Color(.33f,.35f,.42f).linear,-7040);
            Stripe(root,"走廊边缘",start+n,end+n,.025f,new Color(.33f,.35f,.42f).linear,-7040);
        }

        private static void BuildColumn(Transform root, Transform actor)
        {
            var pixels = new[] { new Vector2(1802,2931), new Vector2(2177,2559), new Vector2(2705,2559),
                new Vector2(3070,2931), new Vector2(3070,3641), new Vector2(2705,4010), new Vector2(2177,4010), new Vector2(1802,3641) };
            var points = pixels.Select(p=>layout.PixelToWorld(p)).ToArray();
            var uv = pixels.Select(p=>new Vector2(p.x/5000,1-p.y/6000)).ToArray();
            var renderer = Surface(root,"中央设备前景（原图提取 / 遮挡淡出）",points,uv,null,artwork,95);
            var fade = renderer.gameObject.AddComponent<Day1OcclusionFade>();
            fade.actor=actor; fade.surface=renderer; fade.silhouette=points;
            fade.groundStart=P(1800,3910); fade.groundEnd=P(3100,3910); fade.occludedAlpha=.38f;
            var collision = Child(root,"中央设备脚部碰撞").gameObject.AddComponent<PolygonCollider2D>();
            collision.points=Enumerable.Range(0,12).Select(i=>P(2440+620*Mathf.Cos(i*Mathf.PI/6),3790+240*Mathf.Sin(i*Mathf.PI/6))).ToArray();
        }

        private static void BindInteractions(Day1FlowController flow)
        {
            var positions = new Dictionary<string,Vector2> { {"Rules",P(2585,4300)}, {"Environment",P(3200,4200)}, {"Diagnosis",P(1710,3730)} };
            foreach (var pair in positions)
            {
                var point=flow.Find(pair.Key); point.transform.position=pair.Value; point.homePosition=pair.Value;
                // Synchronize the editor source marker without replacing persistent interaction identities.
                var map=flow.gameObject.scene.GetRootGameObjects().First(g=>g.GetComponent<PixelMapRoot>()!=null);
                var marker=map.transform.Find("Markers/"+pair.Key); if(marker!=null) marker.position=pair.Value;
            }
            layout.paintedInteractions=positions.Keys.Select(k=>flow.Find(k).Prop).ToArray();
        }

        private static void Wall(Transform root,string name,Vector2 a,Vector2 b,float height,Transform actor)
        {
            Vector2 lift=Vector2.up*height, cap=Vector2.up*.11f;
            var vertices=new List<Vector2>(); var colors=new List<Color>(); var triangles=new List<int>();
            Color face=new Color(113/255f,119/255f,142/255f).linear;
            Color top=new Color(137/255f,144/255f,165/255f).linear;
            Color ink=new Color(.09f,.1f,.14f).linear;
            AddQuad(vertices,colors,triangles,a,b,b+lift,a+lift,face);
            AddQuad(vertices,colors,triangles,a+lift,b+lift,b+lift+cap,a+lift+cap,top);
            AddStrip(vertices,colors,triangles,a+lift+cap,b+lift+cap,.012f,ink);
            AddStrip(vertices,colors,triangles,a,b,.014f,ink);
            // No vertical line between pieces: independently fading segments read as one continuous wall.
            var renderer=MeshSurface(root,name,vertices.ToArray(),null,colors.ToArray(),triangles.ToArray(),geometry,250);
            var fade=renderer.gameObject.AddComponent<Day1OcclusionFade>();
            fade.actor=actor; fade.surface=renderer; fade.silhouette=new[]{a,b,b+lift+cap,a+lift+cap};
            fade.groundStart=a; fade.groundEnd=b;
        }

        private static void Boundary(Transform root,string name,Vector2 a,Vector2 b,float thickness)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness*.5f;
            var collider=Child(root,name).gameObject.AddComponent<PolygonCollider2D>(); collider.points=new[]{a-n,b-n,b+n,a+n};
            collider.sharedMaterial=AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
        }
        private static void Stripe(Transform root,string name,Vector2 a,Vector2 b,float thickness,Color color,int order)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness*.5f;
            Surface(root,name,new[]{a-n,b-n,b+n,a+n},null,Enumerable.Repeat(color,4).ToArray(),geometry,order);
        }
        private static void AddQuad(List<Vector2> v,List<Color> c,List<int> t,Vector2 a,Vector2 b,Vector2 d,Vector2 e,Color color)
        {
            int index=v.Count; v.AddRange(new[]{a,b,d,e}); c.AddRange(Enumerable.Repeat(color,4));
            t.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
        }
        private static void AddStrip(List<Vector2> v,List<Color> c,List<int> t,Vector2 a,Vector2 b,float thickness,Color color)
        {
            Vector2 n=new Vector2(-(b-a).y,(b-a).x).normalized*thickness*.5f;
            AddQuad(v,c,t,a-n,b-n,b+n,a+n,color);
        }
        private static MeshRenderer Surface(Transform root,string name,Vector2[] vertices,Vector2[] uv,Color[] colors,Material material,int order)
        {
            var indices=new List<int>(); for(int i=1;i<vertices.Length-1;i++) indices.AddRange(new[]{0,i,i+1});
            return MeshSurface(root,name,vertices,uv,colors,indices.ToArray(),material,order);
        }
        private static MeshRenderer MeshSurface(Transform root,string name,Vector2[] vertices,Vector2[] uv,Color[] colors,int[] triangles,Material material,int order)
        {
            var go=Child(root,name).gameObject;
            string path=GeneratedPath+"/Meshes/Part"+(meshIndex++).ToString("D2")+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
            mesh.name=Path.GetFileNameWithoutExtension(path); mesh.vertices=vertices.Select(p=>new Vector3(p.x,p.y)).ToArray();
            mesh.uv=uv??Enumerable.Repeat(Vector2.zero,vertices.Length).ToArray();
            mesh.colors=colors??Enumerable.Repeat(Color.white,vertices.Length).ToArray();
            mesh.triangles=triangles; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.sortingOrder=order;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            return renderer;
        }
        private static Material Material(string name,Shader shader,Texture texture)
        {
            string path=GeneratedPath+"/"+name+".mat";
            var result=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(result==null){result=new Material(shader);AssetDatabase.CreateAsset(result,path);}
            result.shader=shader; result.mainTexture=texture; result.SetColor("_Color",Color.white); result.SetFloat("_OcclusionAlpha",1);
            EditorUtility.SetDirty(result); return result;
        }
        private static Vector2 P(float x,float y)=>layout.PixelToWorld(new Vector2(x,y));
        private static Transform Child(Transform parent,string name)
        { var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform; }
        private static Camera sceneCamera(Scene scene)=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.CompareTag("MainCamera"));
        private static void EnsureFolder(string path)
        { if(AssetDatabase.IsValidFolder(path))return; string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }

        [MenuItem("Tools/剧情/Day1/导出总控室预览")]
        public static void Preview()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("请停止 Play 后导出。");
            var scene=SceneManager.GetActiveScene(); if(scene.path!=Day1SceneSetup.ScenePath)throw new InvalidOperationException("请先打开 Day1。");
            var camera=sceneCamera(scene); var follow=camera.GetComponent<CameraFollow>();
            Vector3 oldPosition=camera.transform.position; float oldSize=camera.orthographicSize;
            bool oldEnabled=follow!=null&&follow.enabled;
            try
            {
                if(follow!=null)follow.enabled=false;
                camera.transform.position=new Vector3(2,-.5f,-10); camera.orthographicSize=9.6f;
                Capture(camera,"Validation/day1-controlroom-preview.png",1440,1440);
            }
            finally {camera.transform.position=oldPosition;camera.orthographicSize=oldSize;if(follow!=null)follow.enabled=oldEnabled;}
        }
        public static void Capture(Camera camera,string path,int width=1440,int height=1080)
        {
            var previous=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally {camera.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
