using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.PixelMap;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Day1.Editor
{
    /// <summary>
    /// Day1 的简单二维地图。只使用默认地图方块与 PixelRoom 已有道具。
    /// 每个地面/墙格保留 MapBlockInstance，供默认像素地图编辑器继续修改。
    /// Build 不保存场景，也不安装玩家、NPC 或剧情脚本。
    /// </summary>
    public static class Day1MapBuilder
    {
        private const string DefinitionFolder = "Assets/Day1/Maps/Definitions";
        private const string DefaultDefinitionFolder = "Assets/PixelMap/Library/Definitions";
        private const string ArtFolder = "Assets/Art/Generated";
        private const int FloorOrder = -10000;
        private const int WallOrder = -8000;

        private struct Room
        {
            public string Name;
            public int Left, Bottom, Right, Top;
            public Color Tint;

            public Room(string name, int left, int bottom, int right, int top, Color tint)
            {
                Name = name;
                Left = left;
                Bottom = bottom;
                Right = right;
                Top = top;
                Tint = tint;
            }
        }

        private static readonly Room[] Rooms =
        {
            new Room("A01 认知缓冲间", -9, 3, -3, 7, new Color(0.92f, 0.97f, 1f)),
            new Room("A02 总控室", -3, -3, 7, 5, new Color(0.86f, 0.92f, 1f)),
            new Room("A03 生活区餐厨走廊", -3, 5, 7, 12, new Color(1f, 0.94f, 0.85f)),
            new Room("A04 机房", -9, -3, -3, 3, new Color(0.78f, 0.85f, 0.91f)),
            new Room("A05 化学分析室", -3, -10, 7, -3, new Color(0.83f, 1f, 0.96f)),
            new Room("A06 仓储区", 7, -3, 15, 5, new Color(1f, 0.9f, 0.77f))
        };

        /// <summary>
        /// 在传入场景建立地图。剧情坐标位于返回根节点的 Markers 子节点。
        /// 地板衍生定义独立保存，原 Stone/Brick/Grass/Water 定义保持不变。
        /// </summary>
        public static GameObject Build(Scene scene, MapBlockLibrary library)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("Day1MapBuilder requires a loaded scene.", nameof(scene));
            if (library == null) throw new ArgumentNullException(nameof(library));

            EnsureFolder(DefinitionFolder);
            var stone = LoadDefault("Stone");
            var brick = LoadDefault("Brick");
            var grass = LoadDefault("Grass");
            var water = LoadDefault("Water");

            var wall = Derived(library, brick, "Day1Wall", "Day1 站内墙", BlockColliderShape2D.Box,
                WallOrder, new Color(0.65f, 0.7f, 0.79f));
            var boundary = Derived(library, stone, "Day1Boundary", "Day1 外围边界", BlockColliderShape2D.Box,
                WallOrder, new Color(0.59f, 0.67f, 0.69f));
            var outdoor = Derived(library, grass, "Day1GrassFloor", "Day1 草地（可行走）",
                BlockColliderShape2D.None, FloorOrder, new Color(0.75f, 0.87f, 0.76f));
            var dock = Derived(library, brick, "Day1DockFloor", "Day1 码头（可行走）",
                BlockColliderShape2D.None, FloorOrder, new Color(0.78f, 0.78f, 0.76f));
            var sea = Derived(library, water, "Day1Sea", "Day1 蚀海远景（边界外）",
                BlockColliderShape2D.None, FloorOrder - 100, new Color(0.58f, 0.75f, 0.87f));

            var root = new GameObject("Day1 科考站与周边（二维占位地图）");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<PixelMapRoot>().Configure(library);
            var floors = Child(root.transform, "Floors 地面（无碰撞）");
            var walls = Child(root.transform, "Walls 墙体与边界");
            var props = Child(root.transform, "Props 现有 PixelRoom 道具");
            var markers = Child(root.transform, "Markers");

            // 地板统一整数格中心，墙体占边缘格。所有正常门洞固定两格净宽。
            var floorTiles = new Dictionary<Vector2Int, GameObject>();
            Fill(floors, sea, -24, -17, -8, -10, floorTiles);
            Fill(floors, sea, -21, 12, -9, 15, floorTiles);
            Fill(floors, outdoor, -21, -10, -9, 12, floorTiles, true);
            Fill(floors, dock, -21, -15, -10, -10, floorTiles, true);
            for (int i = 0; i < Rooms.Length; i++)
            {
                var room = Rooms[i];
                var floor = Derived(library, stone, "Day1RoomFloor" + i,
                    "Day1 " + room.Name.Substring(4) + "地板（可行走）", BlockColliderShape2D.None,
                    FloorOrder, room.Tint);
                var areaRoot = Child(floors, room.Name);
                Fill(areaRoot, floor, room.Left, room.Bottom, room.Right, room.Top, floorTiles, true);
            }

            var wallCells = new HashSet<Vector2Int>();
            // 总控室北门 x=1,2；南门 x=1,2；东门 y=0,1；西机房门 y=-1,0。
            // 左端额外让出转角两格，避免缓冲间东门被北墙角压缩成一格。
            Horizontal(walls, wall, -3, 7, 5, wallCells, -3, -2, 1, 2);
            Horizontal(walls, wall, -3, 7, -3, wallCells, 1, 2);
            Vertical(walls, wall, 7, -3, 5, wallCells, 0, 1);
            Vertical(walls, wall, -3, -3, 3, wallCells, -1, 0);
            // 缓冲间东西门 y=4,5；机房与缓冲间之间不开侧门。
            Vertical(walls, wall, -3, 3, 7, wallCells, 4, 5);
            Horizontal(walls, wall, -9, -3, 3, wallCells);
            Horizontal(walls, wall, -9, -3, 7, wallCells);
            Vertical(walls, wall, -9, -3, 7, wallCells, 4, 5);
            Horizontal(walls, wall, -9, -3, -3, wallCells);
            // 生活区、科研与仓储外围。
            Vertical(walls, wall, -3, 7, 12, wallCells);
            Vertical(walls, wall, 7, 5, 12, wallCells);
            Horizontal(walls, wall, -3, 7, 12, wallCells);
            Vertical(walls, wall, -3, -10, -3, wallCells);
            Vertical(walls, wall, 7, -10, -3, wallCells);
            Horizontal(walls, wall, -3, 7, -10, wallCells);
            Horizontal(walls, wall, 7, 15, -3, wallCells);
            Horizontal(walls, wall, 7, 15, 5, wallCells);
            Vertical(walls, wall, 15, -3, 5, wallCells);
            // 室外边界及码头。顶边是第一日不开放的蚀海侧，没有可穿透缺口。
            Vertical(walls, boundary, -21, -15, 12, wallCells);
            Vertical(walls, boundary, -9, -10, 12, wallCells, 4, 5);
            Horizontal(walls, boundary, -21, -9, 12, wallCells);
            Horizontal(walls, boundary, -21, -9, -10, wallCells, -17, -16);
            Vertical(walls, boundary, -10, -15, -10, wallCells);
            Horizontal(walls, boundary, -21, -10, -15, wallCells);

            BuildProps(props);
            BuildMarkers(markers);
            var floorCells = new HashSet<Vector2Int>(floorTiles.Keys);
            ValidateConnectivity(root, floorCells, wallCells);
            AssetDatabase.SaveAssets();
            Debug.Log("DAY1_MAP_BUILT: " + floorCells.Count + " floor coordinates; " +
                      wallCells.Count + " wall coordinates; all interaction markers reachable.");
            return root;
        }

        private static void BuildMarkers(Transform root)
        {
            Marker(root, "Spawn", -6, 5);
            Marker(root, "BufferExit", -3, 4.5f);
            Marker(root, "ControlAssembly", 1, 1);
            Marker(root, "Rules", -0.5f, 3);
            Marker(root, "Environment", 5, 3);
            Marker(root, "Diagnosis", 5, -1);
            Marker(root, "Supplies", 4, 8);
            Marker(root, "LinXi", -6, 0);
            Marker(root, "Hydrologist", -0.5f, -6.5f);
            Marker(root, "Geologist", -15, 2);
            Marker(root, "Plant", -18, 1);
            Marker(root, "YangYinglong", -17, 8);
            Marker(root, "Threat", -13, 8);
            Marker(root, "SeaGate", -18, 10);
            Marker(root, "ContainmentResearcher", 11, 2);
            Marker(root, "Repair", 12, -0.5f);
            Marker(root, "Mechanic", -14, -12);
            Marker(root, "Tools", -19, -12);
            Marker(root, "Parts", -18, -13.5f);
            Marker(root, "Power", -12, -13.5f);
            Marker(root, "ShadowStart", -1, 9.5f);
            Marker(root, "ShadowEnd", 6, 9.5f);
        }

        private static void BuildProps(Transform root)
        {
            // 道具贴图与 PixelRoom 相同，只有脚部小碰撞，不阻塞房间门洞。
            Prop(root, "缓冲间检测终端（桌面占位）", "Table", -6, 6.25f);
            Prop(root, "总控规则台", "Table", -0.5f, 3.85f);
            Prop(root, "环境监测台", "Table", 5, 3.85f);
            Prop(root, "设备诊断台", "Table", 5, -0.15f);
            Prop(root, "中央立柱（箱体占位）", "Crate", 2, 0);
            Prop(root, "餐桌与生活物资", "Table", 4, 8.85f);
            Prop(root, "厨房工作台", "Table", -0.5f, 10.5f);
            Prop(root, "机房设备柜", "Crate", -7.5f, 1.5f);
            Prop(root, "机房检修台", "Table", -5.5f, 1.5f);
            Prop(root, "化学分析台", "Table", -0.5f, -5.55f);
            Prop(root, "样本柜", "Crate", 5, -5.5f);
            Prop(root, "仓储装备箱一", "Crate", 9, 3.5f);
            Prop(root, "仓储装备箱二", "Crate", 13.5f, 3.5f);
            Prop(root, "装备维修工作台", "Table", 12, 0.35f);
            Prop(root, "码头工具箱", "Crate", -19, -11.15f);
            Prop(root, "码头零件箱", "Crate", -18, -12.65f);
            Prop(root, "码头供电箱", "Crate", -12, -12.65f);
        }

        private static void Prop(Transform root, string name, string key, float x, float y)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + key + ".png");
            if (sprite == null) throw new InvalidOperationException("Missing existing PixelRoom sprite: " + key);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, y, 0);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = Mathf.RoundToInt(-y * 32f);
            go.AddComponent<PixelPrototype.FeetYSort>();
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = key == "Table" ? new Vector2(1.75f, 0.55f) : new Vector2(0.8f, 0.55f);
            collider.offset = new Vector2(0, 0.2f);
            collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
        }

        private static void Fill(Transform parent, MapBlockDefinition definition, int left, int bottom,
            int right, int top, Dictionary<Vector2Int, GameObject> cells, bool replace = false)
        {
            for (int y = bottom; y <= top; y++)
            for (int x = left; x <= right; x++)
            {
                var coordinate = new Vector2Int(x, y);
                if (cells.TryGetValue(coordinate, out var previous))
                {
                    if (!replace) continue;
                    UnityEngine.Object.DestroyImmediate(previous);
                }
                // 每格只有一个地板，避免编辑器擦除后露出重叠地板。
                cells[coordinate] = Tile(parent, definition, coordinate);
            }
        }

        private static void Horizontal(Transform parent, MapBlockDefinition definition, int left, int right,
            int y, HashSet<Vector2Int> cells, params int[] gaps)
        {
            for (int x = left; x <= right; x++)
                if (!gaps.Contains(x)) Wall(parent, definition, new Vector2Int(x, y), cells);
        }

        private static void Vertical(Transform parent, MapBlockDefinition definition, int x, int bottom,
            int top, HashSet<Vector2Int> cells, params int[] gaps)
        {
            for (int y = bottom; y <= top; y++)
                if (!gaps.Contains(y)) Wall(parent, definition, new Vector2Int(x, y), cells);
        }

        private static void Wall(Transform root, MapBlockDefinition definition, Vector2Int coordinate,
            HashSet<Vector2Int> cells)
        {
            if (cells.Add(coordinate)) Tile(root, definition, coordinate);
        }

        private static GameObject Tile(Transform parent, MapBlockDefinition definition, Vector2Int coordinate)
        {
            var go = new GameObject(definition.DisplayName + " [" + coordinate.x + "," + coordinate.y + "]");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(coordinate.x, coordinate.y, 0);
            go.AddComponent<MapBlockInstance>().Configure(definition, MapPlacementMode.Grid, coordinate);
            return go;
        }

        private static void Marker(Transform root, string name, float x, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(x, y, 0);
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static MapBlockDefinition LoadDefault(string key)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MapBlockDefinition>(DefaultDefinitionFolder + "/" + key + ".asset");
            if (asset == null || asset.Sprite == null)
                throw new InvalidOperationException("Missing default map block: " + key);
            return asset;
        }

        private static MapBlockDefinition Derived(MapBlockLibrary library, MapBlockDefinition source,
            string key, string name, BlockColliderShape2D shape, int order, Color tint)
        {
            string path = DefinitionFolder + "/" + key + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<MapBlockDefinition>(path);
            if (definition == null)
            {
                definition = UnityEngine.Object.Instantiate(source);
                definition.name = key;
                var initial = new SerializedObject(definition);
                initial.FindProperty("id").stringValue = Guid.NewGuid().ToString("N");
                initial.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, path);
            }
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("category").stringValue = "Day1 二维占位";
            serialized.FindProperty("sprite").objectReferenceValue = source.Sprite;
            serialized.FindProperty("colliderShape").enumValueIndex = (int)shape;
            serialized.FindProperty("sortingOrder").intValue = order;
            serialized.FindProperty("tint").colorValue = tint;
            serialized.FindProperty("size").vector2Value = Vector2.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            if (library.Add(definition)) EditorUtility.SetDirty(library);
            return definition;
        }

        private static void ValidateConnectivity(GameObject root, HashSet<Vector2Int> floors,
            HashSet<Vector2Int> walls)
        {
            var reached = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(-6, 5));
            var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!floors.Contains(current) || walls.Contains(current) || !reached.Add(current)) continue;
                foreach (var direction in directions) queue.Enqueue(current + direction);
            }
            foreach (Transform marker in root.transform.Find("Markers"))
            {
                Vector2 position = marker.position;
                // 半格坐标允许落在相邻两块可行走地板之间。
                var candidate = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y));
                if (!reached.Contains(candidate))
                    throw new InvalidOperationException("Day1 marker is not reachable: " + marker.name + " " + position);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
