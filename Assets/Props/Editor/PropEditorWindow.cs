using UnityEditor;
using UnityEngine;

namespace Emerge.Props.Editor
{
    public sealed class PropEditorWindow : EditorWindow
    {
        [SerializeField] private PropLibrary library;
        [SerializeField] private PropDefinition selected;
        private UnityEditor.Editor inspector;
        private Vector2 listScroll, detailsScroll;
        [SerializeField] private string search = "";
        [MenuItem("Tools/道具编辑器")]
        public static void Open() { Open(null); }
        public static void Open(PropDefinition definition)
        {
            var window = GetWindow<PropEditorWindow>("道具编辑器");
            window.minSize = new Vector2(650f, 480f);
            if (definition != null) { window.selected = definition; window.search = ""; window.detailsScroll = Vector2.zero; }
            window.Show();
        }
        private void OnEnable()
        {
            if (library == null) library = PropAssetFactory.EnsureDefaultLibrary();
            if (selected == null && library.Props.Count > 0) selected = library.Props[0];
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        private void OnGUI()
        {
            library = (PropLibrary)EditorGUILayout.ObjectField("道具库", library, typeof(PropLibrary), false);
            EditorGUILayout.HelpBox("定义在不同地图中复用；每个场景实例独立记录拾取和一次性交互状态。", MessageType.Info);
            if (library == null) { if (GUILayout.Button("使用默认道具库")) library = PropAssetFactory.EnsureDefaultLibrary(); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(200f)))
                {
                    search = EditorGUILayout.TextField("搜索", search);
                    listScroll = EditorGUILayout.BeginScrollView(listScroll, "box");
                    foreach (var definition in library.Props)
                    {
                        if (definition == null || (!string.IsNullOrWhiteSpace(search) &&
                            (definition.DisplayName + definition.category).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) < 0)) continue;
                        if (GUILayout.Toggle(selected == definition, definition.DisplayName + "\n" + definition.category, "Button", GUILayout.Height(38f))) selected = definition;
                    }
                    EditorGUILayout.EndScrollView();
                    if (GUILayout.Button("新建道具")) selected = PropAssetFactory.Create(library);
                    using (new EditorGUI.DisabledScope(selected == null))
                    {
                        if (GUILayout.Button("复制所选")) selected = PropAssetFactory.Duplicate(library, selected);
                        if (GUILayout.Button("从库中移除"))
                        { Undo.RecordObject(library, "移除道具"); library.Remove(selected); EditorUtility.SetDirty(library); selected = null; AssetDatabase.SaveAssets(); }
                    }
                    if (GUILayout.Button("导入 Project 选中素材"))
                    {
                        var imported = PropAssetFactory.Import(library, Selection.objects);
                        if (imported.Count > 0) selected = imported[imported.Count - 1];
                        else ShowNotification(new GUIContent("请选择 Sprite、已设为 Sprite 的贴图、Prefab 或道具定义"));
                    }
                    if (GUILayout.Button("打开地图道具笔刷")) Emerge.PixelMap.Editor.PixelMapEditorWindow.OpenPropsTab(selected);
                    if (GUILayout.Button("保存资产")) AssetDatabase.SaveAssets();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                    if (selected == null) EditorGUILayout.HelpBox("选择一个道具进行编辑。", MessageType.Info);
                    else
                    {
                        UnityEditor.Editor.CreateCachedEditor(selected, typeof(PropDefinitionEditor), ref inspector);
                        inspector.OnInspectorGUI();
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }
    }
}
