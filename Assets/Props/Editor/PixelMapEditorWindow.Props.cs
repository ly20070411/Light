using Emerge.Props;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.PixelMap.Editor
{
    internal sealed partial class PixelMapEditorWindow
    {
        [SerializeField] private bool propsMode;
        [SerializeField] private PropLibrary propLibrary;
        [SerializeField] private PropDefinition selectedProp;
        private UnityEditor.Editor propInspector;
        private string propSearch = "";
        public static void OpenPropsTab()
        { OpenPropsTab(null); }
        public static void OpenPropsTab(PropDefinition definition)
        {
            var window = GetWindow<PixelMapEditorWindow>("像素地图编辑器");
            window.propsMode = true; window.RefreshPropLibrary();
            if (definition != null) { window.selectedProp = definition; window.propSearch = ""; window.inspectorScroll = Vector2.zero; }
            window.Show();
        }
        private void RefreshPropLibrary()
        {
            propLibrary = library != null ? library.PropLibrary : null;
            if (propLibrary == null)
            {
                propLibrary = PropAssetFactory.EnsureDefaultLibrary();
                if (library != null) { library.SetPropLibrary(propLibrary); EditorUtility.SetDirty(library); }
            }
            if (selectedProp == null && propLibrary.Props.Count > 0) selectedProp = propLibrary.Props[0];
        }
        private void DrawPropModeSelector()
        {
            propsMode = GUILayout.Toolbar(propsMode ? 1 : 0, new[] { "地图方块", "交互道具" }) == 1;
            if (propLibrary == null || (library != null && library.PropLibrary != propLibrary)) RefreshPropLibrary();
            if (!propsMode) return;
            EditorGUI.BeginChangeCheck();
            var changedLibrary = (PropLibrary)EditorGUILayout.ObjectField("关联道具库", propLibrary, typeof(PropLibrary), false);
            if (EditorGUI.EndChangeCheck())
            {
                propLibrary = changedLibrary; selectedProp = null;
                if (library != null) { Undo.RecordObject(library, "关联地图道具库"); library.SetPropLibrary(propLibrary); EditorUtility.SetDirty(library); }
            }
        }
        private void DrawPropPalette()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(190f, position.width * .38f))))
            {
                EditorGUILayout.LabelField("交互道具", EditorStyles.boldLabel);
                propSearch = EditorGUILayout.TextField("搜索", propSearch);
                paletteScroll = EditorGUILayout.BeginScrollView(paletteScroll, "box");
                if (propLibrary != null)
                    foreach (var definition in propLibrary.Props)
                    {
                        if (definition == null || (!string.IsNullOrWhiteSpace(propSearch) &&
                            (definition.DisplayName + definition.category).IndexOf(propSearch, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var rect = GUILayoutUtility.GetRect(32f, 32f, GUILayout.Width(32f));
                            Texture preview = AssetPreview.GetAssetPreview(definition.sprite);
                            if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                            else EditorGUI.DrawRect(rect, definition.tint);
                            if (GUILayout.Toggle(selectedProp == definition, definition.DisplayName + "\n" + definition.category, "Button", GUILayout.Height(36f))) selectedProp = definition;
                        }
                    }
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("新建道具") && propLibrary != null) selectedProp = PropAssetFactory.Create(propLibrary);
                if (GUILayout.Button("导入 Project 选中素材") && propLibrary != null)
                {
                    var imported = PropAssetFactory.Import(propLibrary, Selection.objects);
                    if (imported.Count > 0) selectedProp = imported[imported.Count - 1];
                    else ShowNotification(new GUIContent("请选择 Sprite、已转换贴图、Prefab 或道具定义"));
                }
                if (GUILayout.Button("独立道具编辑器")) PropEditorWindow.Open(selectedProp);
                if (GUILayout.Button("保存资产")) AssetDatabase.SaveAssets();
            }
        }
        private void DrawPropInspector()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("道具属性（实时同步场景）", EditorStyles.boldLabel);
                inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, "box");
                if (selectedProp == null) EditorGUILayout.HelpBox("选择一个道具，在 Scene 视图中左键放置。", MessageType.Info);
                else
                {
                    UnityEditor.Editor.CreateCachedEditor(selectedProp, typeof(PropDefinitionEditor), ref propInspector);
                    propInspector.OnInspectorGUI();
                }
                EditorGUILayout.EndScrollView();
            }
        }
        private void DrawPropPreview(Vector3 position)
        {
            if (selectedProp == null) return;
            using (new Handles.DrawingScope(brushTool == BrushTool.Erase ? Color.red : new Color(.2f, 1f, .7f, .8f)))
            using (new Handles.DrawingScope(Matrix4x4.TRS(position, Quaternion.Euler(0, 0, selectedProp.allowRotation ? rotationZ : 0), Vector3.one)))
            {
                Handles.DrawWireCube(Vector3.zero, selectedProp.worldSize);
                if (selectedProp.isSolid) Handles.DrawWireCube(selectedProp.colliderOffset, selectedProp.colliderSize);
            }
        }
        private void PlacePropAt(Vector3 position)
        {
            if (selectedProp == null) return;
            EnsureMapRoot(true);
            if (mapRoot == null) return;
            float spacing = placementMode == MapPlacementMode.Grid ? Mathf.Min(library.GridSize.x, library.GridSize.y) * .25f : freeBrushSpacing;
            if (Vector3.Distance(position, lastPlacedPosition) < spacing) return;
            if (placementMode == MapPlacementMode.Grid)
                foreach (var prop in mapRoot.GetComponentsInChildren<PropInstance>(true))
                    if (Mathf.Abs(prop.transform.position.z - position.z) < .001f && Vector2.Distance(prop.transform.position, position) < .01f) return;
            var instance = PropPlacementService.Place(selectedProp, mapRoot.transform, position, rotationZ, placementMode, library.GridSize);
            if (instance == null) return;
            Selection.activeGameObject = instance.gameObject; lastPlacedPosition = position;
        }
        private void ErasePropAt(Vector3 world)
        {
            EnsureMapRoot(false);
            if (mapRoot != null) PropPlacementService.Erase(mapRoot.transform, world);
        }
    }
}
