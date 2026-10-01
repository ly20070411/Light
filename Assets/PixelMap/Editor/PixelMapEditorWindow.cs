using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.PixelMap.Editor
{
    internal sealed class PixelMapEditorWindow : EditorWindow
    {
        private enum BrushTool
        {
            Place,
            Erase
        }

        private MapBlockLibrary library;
        private PixelMapRoot mapRoot;
        private int selectedIndex;
        private Vector2 paletteScroll;
        private Vector2 inspectorScroll;
        private MapPlacementMode placementMode = MapPlacementMode.Grid;
        private BrushTool brushTool = BrushTool.Place;
        private bool brushEnabled = true;
        private bool showGrid = true;
        private float placementZ;
        private float rotationZ;
        private float freeBrushSpacing = 0.25f;
        private Vector3 lastPlacedPosition = new Vector3(float.PositiveInfinity, 0f, 0f);

        [MenuItem("Tools/像素地图编辑器 %#m")]
        private static void Open()
        {
            GetWindow<PixelMapEditorWindow>("像素地图编辑器");
        }

        [MenuItem("GameObject/像素地图/创建地图根节点", false, 10)]
        private static void CreateMapRootFromMenu()
        {
            var window = GetWindow<PixelMapEditorWindow>("像素地图编辑器");
            window.EnsureMapRoot(true);
        }

        private void OnEnable()
        {
            library = PixelMapAssetFactory.EnsureDefaultLibrary();
            if (library != null) placementZ = library.DefaultZ;
            FindMapRoot();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private MapBlockDefinition SelectedDefinition
        {
            get
            {
                if (library == null || library.Blocks.Count == 0) return null;
                selectedIndex = Mathf.Clamp(selectedIndex, 0, library.Blocks.Count - 1);
                return library.Blocks[selectedIndex];
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(4f);
            DrawBrushSettings();
            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPalette();
                DrawDefinitionInspector();
            }

            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("2D 像素地图编辑器", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在 Scene 视图中左键绘制。按住 Alt 可正常浏览场景；切换到擦除模式后点击方块即可删除。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            library = (MapBlockLibrary)EditorGUILayout.ObjectField("素材库", library, typeof(MapBlockLibrary), false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedIndex = 0;
                if (library != null) placementZ = library.DefaultZ;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                mapRoot = (PixelMapRoot)EditorGUILayout.ObjectField("地图根节点", mapRoot, typeof(PixelMapRoot), true);
                if (GUILayout.Button("查找/创建", GUILayout.Width(82f))) EnsureMapRoot(true);
            }

            if (library == null && GUILayout.Button("创建默认素材库"))
            {
                library = PixelMapAssetFactory.EnsureDefaultLibrary();
            }
        }

        private void DrawBrushSettings()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("绘制设置", EditorStyles.boldLabel);
                placementMode = (MapPlacementMode)GUILayout.Toolbar((int)placementMode, new[] { "自由放置", "网格吸附" });
                brushTool = (BrushTool)GUILayout.Toolbar((int)brushTool, new[] { "放置", "擦除" });

                brushEnabled = EditorGUILayout.Toggle("启用 Scene 笔刷", brushEnabled);
                showGrid = EditorGUILayout.Toggle("显示网格", showGrid);
                placementZ = EditorGUILayout.FloatField("Z 平面", placementZ);
                rotationZ = EditorGUILayout.Slider("旋转角度", rotationZ, -180f, 180f);
                if (placementMode == MapPlacementMode.Free)
                    freeBrushSpacing = Mathf.Max(0.01f, EditorGUILayout.FloatField("连续绘制间距", freeBrushSpacing));

                if (library != null)
                {
                    var serializedLibrary = new SerializedObject(library);
                    serializedLibrary.Update();
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(serializedLibrary.FindProperty("gridSize"), new GUIContent("网格尺寸"));
                    EditorGUILayout.PropertyField(serializedLibrary.FindProperty("defaultZ"), new GUIContent("默认 Z"));
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedLibrary.ApplyModifiedProperties();
                        EditorUtility.SetDirty(library);
                    }
                    else serializedLibrary.ApplyModifiedProperties();
                }
            }
        }

        private void DrawPalette()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(190f, position.width * 0.38f))))
            {
                EditorGUILayout.LabelField("地图素材", EditorStyles.boldLabel);
                paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, "box");
                if (library != null)
                {
                    for (int i = 0; i < library.Blocks.Count; i++)
                    {
                        var definition = library.Blocks[i];
                        if (definition == null) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            Rect swatch = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f));
                            EditorGUI.DrawRect(swatch, definition.Tint);
                            if (definition.Sprite != null)
                                GUI.DrawTexture(swatch, AssetPreview.GetAssetPreview(definition.Sprite) ?? definition.Sprite.texture,
                                    ScaleMode.ScaleToFit, true);
                            bool selected = GUILayout.Toggle(selectedIndex == i,
                                definition.DisplayName + "\n" + definition.Category, "Button", GUILayout.Height(32f));
                            if (selected) selectedIndex = i;
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                if (GUILayout.Button("导入 Project 中选中的素材")) ImportSelection();
                if (GUILayout.Button("新建空白素材")) CreateBlankDefinition();
                if (GUILayout.Button("补全默认素材"))
                {
                    library = PixelMapAssetFactory.EnsureDefaultLibrary();
                    Repaint();
                }
            }
        }

        private void DrawDefinitionInspector()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("方块属性（实时同步）", EditorStyles.boldLabel);
                inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, "box");
                var definition = SelectedDefinition;
                if (definition == null)
                {
                    EditorGUILayout.HelpBox("请选择或导入一个素材。", MessageType.Warning);
                }
                else
                {
                    var serializedDefinition = new SerializedObject(definition);
                    serializedDefinition.Update();
                    EditorGUI.BeginChangeCheck();
                    DrawProperty(serializedDefinition, "displayName", "名称");
                    DrawProperty(serializedDefinition, "category", "分类");
                    DrawProperty(serializedDefinition, "sprite", "Sprite");
                    DrawProperty(serializedDefinition, "prefab", "可选 Prefab");
                    DrawProperty(serializedDefinition, "material", "可选材质");
                    DrawProperty(serializedDefinition, "tint", "颜色");
                    DrawProperty(serializedDefinition, "size", "世界尺寸");
                    DrawProperty(serializedDefinition, "colliderShape", "碰撞形状");
                    DrawProperty(serializedDefinition, "isTrigger", "是否 Trigger");
                    DrawProperty(serializedDefinition, "physicsMaterial", "2D 物理材质");
                    DrawProperty(serializedDefinition, "sortingLayerName", "Sorting Layer");
                    DrawProperty(serializedDefinition, "sortingOrder", "Order in Layer");
                    DrawProperty(serializedDefinition, "unityTag", "Tag");
                    DrawProperty(serializedDefinition, "unityLayer", "Layer");
                    DrawProperty(serializedDefinition, "allowRotation", "允许旋转");
                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedDefinition.ApplyModifiedProperties();
                        EditorUtility.SetDirty(definition);
                        MapBlockDefinitionEditor.ApplyToScene(definition);
                    }
                    else serializedDefinition.ApplyModifiedProperties();

                    EditorGUILayout.Space(8f);
                    if (GUILayout.Button("从素材库移除")) RemoveSelectedDefinition(false);
                    if (GUILayout.Button("删除素材定义资产")) RemoveSelectedDefinition(true);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawProperty(SerializedObject serializedObject, string propertyName, string label)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space(4f);
            string mode = placementMode == MapPlacementMode.Grid ? "网格" : "自由";
            string tool = brushTool == BrushTool.Place ? "放置" : "擦除";
            EditorGUILayout.LabelField($"当前：{mode} / {tool} / Z={placementZ:0.###}", EditorStyles.miniLabel);
        }

        private void ImportSelection()
        {
            if (library == null) library = PixelMapAssetFactory.EnsureDefaultLibrary();
            var imported = PixelMapAssetFactory.ImportObjects(library, Selection.objects);
            if (imported.Count == 0)
                ShowNotification(new GUIContent("请选择 Sprite、Texture2D、2D Prefab 或 Material"));
            else
            {
                selectedIndex = library.Blocks.Count - 1;
                ShowNotification(new GUIContent($"已导入 {imported.Count} 个素材"));
            }
        }

        private void CreateBlankDefinition()
        {
            if (library == null) library = PixelMapAssetFactory.EnsureDefaultLibrary();
            PixelMapAssetFactory.CreateBlankDefinition(library);
            selectedIndex = library.Blocks.Count - 1;
        }

        private void RemoveSelectedDefinition(bool deleteAsset)
        {
            var definition = SelectedDefinition;
            if (library == null || definition == null) return;
            string message = deleteAsset
                ? $"将从素材库移除并删除“{definition.DisplayName}”定义资产。场景中已有方块会保留，但引用会丢失。"
                : $"仅从当前素材库移除“{definition.DisplayName}”，不会删除资产或已放置方块。";
            if (!EditorUtility.DisplayDialog("确认删除", message, "确认", "取消")) return;

            string path = AssetDatabase.GetAssetPath(definition);
            library.Remove(definition);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            if (deleteAsset && !string.IsNullOrWhiteSpace(path)) AssetDatabase.DeleteAsset(path);
            selectedIndex = Mathf.Max(0, selectedIndex - 1);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!brushEnabled || library == null) return;
            if (showGrid && placementMode == MapPlacementMode.Grid) DrawGrid(sceneView);

            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.alt) return;

            int controlId = GUIUtility.GetControlID("PixelMapBrush".GetHashCode(), FocusType.Passive);
            if (currentEvent.type == EventType.Layout) HandleUtility.AddDefaultControl(controlId);

            if (!TryGetMouseWorld(currentEvent.mousePosition, out Vector3 world)) return;
            Vector3 position = placementMode == MapPlacementMode.Grid ? SnapToGrid(world) : world;
            DrawPreview(position);

            if (currentEvent.type == EventType.MouseMove) sceneView.Repaint();
            bool paintEvent = currentEvent.button == 0 &&
                              (currentEvent.type == EventType.MouseDown || currentEvent.type == EventType.MouseDrag);
            if (paintEvent)
            {
                if (brushTool == BrushTool.Place) PlaceAt(position);
                else EraseAt(world);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                lastPlacedPosition = new Vector3(float.PositiveInfinity, 0f, 0f);
            }

            Handles.BeginGUI();
            GUI.Label(new Rect(12f, 12f, 300f, 22f),
                brushTool == BrushTool.Place ? "像素地图：左键绘制（Alt 浏览）" : "像素地图：左键擦除（Alt 浏览）",
                EditorStyles.helpBox);
            Handles.EndGUI();
        }

        private bool TryGetMouseWorld(Vector2 mousePosition, out Vector3 world)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, placementZ));
            if (plane.Raycast(ray, out float distance))
            {
                world = ray.GetPoint(distance);
                world.z = placementZ;
                return true;
            }
            world = default;
            return false;
        }

        private Vector3 SnapToGrid(Vector3 world)
        {
            Vector2 grid = library != null ? library.GridSize : Vector2.one;
            return new Vector3(
                Mathf.Round(world.x / grid.x) * grid.x,
                Mathf.Round(world.y / grid.y) * grid.y,
                placementZ);
        }

        private void DrawPreview(Vector3 position)
        {
            var definition = SelectedDefinition;
            Vector2 size = definition != null ? definition.Size : (library != null ? library.GridSize : Vector2.one);
            Color color = brushTool == BrushTool.Erase ? new Color(1f, 0.25f, 0.2f, 0.9f) :
                (definition != null ? definition.Tint : Color.white);
            color.a = 0.85f;
            using (new Handles.DrawingScope(color))
            {
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.Euler(0f, 0f, rotationZ), Vector3.one);
                using (new Handles.DrawingScope(matrix)) Handles.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));
            }
        }

        private void DrawGrid(SceneView sceneView)
        {
            Vector2 grid = library.GridSize;
            Vector3 center = sceneView.pivot;
            int columns = Mathf.Clamp(Mathf.CeilToInt(sceneView.size * 2f / grid.x) + 4, 8, 100);
            int rows = Mathf.Clamp(Mathf.CeilToInt(sceneView.size * 2f / grid.y) + 4, 8, 100);
            float startX = Mathf.Floor(center.x / grid.x) * grid.x - columns * grid.x * 0.5f;
            float startY = Mathf.Floor(center.y / grid.y) * grid.y - rows * grid.y * 0.5f;
            Color oldColor = Handles.color;
            Handles.color = new Color(0.35f, 0.75f, 1f, 0.2f);
            for (int x = 0; x <= columns; x++)
            {
                float px = startX + x * grid.x;
                Handles.DrawLine(new Vector3(px, startY, placementZ), new Vector3(px, startY + rows * grid.y, placementZ));
            }
            for (int y = 0; y <= rows; y++)
            {
                float py = startY + y * grid.y;
                Handles.DrawLine(new Vector3(startX, py, placementZ), new Vector3(startX + columns * grid.x, py, placementZ));
            }
            Handles.color = oldColor;
        }

        private void PlaceAt(Vector3 position)
        {
            var definition = SelectedDefinition;
            if (definition == null) return;
            EnsureMapRoot(true);
            if (mapRoot == null) return;

            float requiredSpacing = placementMode == MapPlacementMode.Grid
                ? Mathf.Min(library.GridSize.x, library.GridSize.y) * 0.25f
                : freeBrushSpacing;
            if (Vector3.Distance(position, lastPlacedPosition) < requiredSpacing) return;
            if (placementMode == MapPlacementMode.Grid && HasBlockAt(position)) return;

            GameObject instanceObject = null;
            if (definition.Prefab != null)
                instanceObject = PrefabUtility.InstantiatePrefab(definition.Prefab, mapRoot.gameObject.scene) as GameObject;
            if (instanceObject == null) instanceObject = new GameObject(definition.DisplayName);

            Undo.RegisterCreatedObjectUndo(instanceObject, "放置地图方块");
            Undo.SetTransformParent(instanceObject.transform, mapRoot.transform, "设置地图父节点");
            instanceObject.transform.position = position;
            instanceObject.transform.rotation = Quaternion.Euler(0f, 0f, definition.AllowRotation ? rotationZ : 0f);

            var blockInstance = instanceObject.GetComponent<MapBlockInstance>();
            if (blockInstance == null) blockInstance = Undo.AddComponent<MapBlockInstance>(instanceObject);
            Vector2 grid = library.GridSize;
            var coordinate = new Vector2Int(
                Mathf.RoundToInt(position.x / grid.x),
                Mathf.RoundToInt(position.y / grid.y));
            blockInstance.Configure(definition, placementMode, coordinate);

            Selection.activeGameObject = instanceObject;
            lastPlacedPosition = position;
            EditorSceneManager.MarkSceneDirty(instanceObject.scene);
        }

        private bool HasBlockAt(Vector3 position)
        {
            if (mapRoot == null) return false;
            float epsilon = Mathf.Min(library.GridSize.x, library.GridSize.y) * 0.1f;
            var instances = mapRoot.GetComponentsInChildren<MapBlockInstance>(true);
            foreach (var instance in instances)
            {
                if (Mathf.Abs(instance.transform.position.z - placementZ) < 0.001f &&
                    Vector2.Distance(instance.transform.position, position) <= epsilon) return true;
            }
            return false;
        }

        private void EraseAt(Vector3 world)
        {
            EnsureMapRoot(false);
            if (mapRoot == null) return;

            MapBlockInstance best = null;
            float bestDistance = float.PositiveInfinity;
            var colliders = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
            foreach (var collider in colliders)
            {
                var candidate = collider.GetComponentInParent<MapBlockInstance>();
                if (candidate == null || !candidate.transform.IsChildOf(mapRoot.transform)) continue;
                float distance = Mathf.Abs(candidate.transform.position.z - placementZ);
                if (distance < bestDistance) { best = candidate; bestDistance = distance; }
            }

            if (best == null)
            {
                foreach (var candidate in mapRoot.GetComponentsInChildren<MapBlockInstance>(true))
                {
                    var renderer = candidate.GetComponent<SpriteRenderer>();
                    if (renderer == null || !renderer.bounds.Contains(new Vector3(world.x, world.y, renderer.bounds.center.z))) continue;
                    float distance = Mathf.Abs(candidate.transform.position.z - placementZ);
                    if (distance < bestDistance) { best = candidate; bestDistance = distance; }
                }
            }

            if (best != null)
            {
                var scene = best.gameObject.scene;
                Undo.DestroyObjectImmediate(best.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private void FindMapRoot()
        {
            mapRoot = FindObjectOfType<PixelMapRoot>();
        }

        private void EnsureMapRoot(bool create)
        {
            if (mapRoot == null) FindMapRoot();
            if (mapRoot != null || !create) return;

            string rootName = library != null ? library.MapRootName : "Pixel Map";
            var rootObject = new GameObject(rootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "创建像素地图根节点");
            mapRoot = Undo.AddComponent<PixelMapRoot>(rootObject);
            mapRoot.Configure(library);
            Selection.activeGameObject = rootObject;
            EditorSceneManager.MarkSceneDirty(rootObject.scene);
        }
    }
}
