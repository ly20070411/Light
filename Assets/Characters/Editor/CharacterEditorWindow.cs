using System;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.Characters.Editor
{
    public sealed class CharacterEditorWindow : EditorWindow
    {
        [SerializeField] private CharacterCatalog catalog;
        [SerializeField] private CharacterDefinition selected;
        [SerializeField] private string search = "";
        private Vector2 listScroll, detailsScroll;
        private UnityEditor.Editor inspector;

        [MenuItem("Tools/角色编辑器")]
        public static void Open()
        {
            var window = GetWindow<CharacterEditorWindow>("角色编辑器");
            window.minSize = new Vector2(760, 520); window.Show();
        }
        private void OnEnable()
        {
            if (catalog == null) catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CharacterAssetFactory.CatalogPath);
            if (selected == null && catalog != null && catalog.Characters.Count > 0) selected = catalog.Characters[0];
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        private void OnGUI()
        {
            catalog = (CharacterCatalog)EditorGUILayout.ObjectField("角色库", catalog, typeof(CharacterCatalog), false);
            if (catalog == null)
            {
                if (GUILayout.Button("创建初始角色库")) { CharacterAssetFactory.Install(); OnEnable(); }
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
                {
                    search = EditorGUILayout.TextField("搜索", search);
                    EditorGUILayout.LabelField("共 " + catalog.Characters.Count + " 名角色");
                    listScroll = EditorGUILayout.BeginScrollView(listScroll, "box");
                    foreach (var character in catalog.Characters)
                    {
                        if (character == null) continue;
                        string searchable = character.DisplayName + character.Id + character.faction + character.department + character.function + character.hexagram;
                        if (!string.IsNullOrWhiteSpace(search) && searchable.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var rect = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44));
                            if (character.portrait != null)
                            {
                                Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(character.portrait);
                                GUI.DrawTextureWithTexCoords(rect, character.portrait.texture, new Rect(uv.x, uv.y, uv.z - uv.x, uv.w - uv.y));
                            }
                            string label = character.DisplayName + (character.IsNamePending ? "（待命名）" : "") + "\n" + character.faction + " · " + character.hexagram;
                            if (GUILayout.Toggle(selected == character, label, "Button", GUILayout.Height(44))) selected = character;
                        }
                    }
                    EditorGUILayout.EndScrollView();
                    using (new EditorGUI.DisabledScope(selected == null))
                    {
                        if (GUILayout.Button("复制角色 ID")) EditorGUIUtility.systemCopyBuffer = selected.Id;
                        if (GUILayout.Button("定位角色资产")) { Selection.activeObject = selected; EditorGUIUtility.PingObject(selected); }
                        if (GUILayout.Button("定位角色预制体")) { Selection.activeObject = selected.prefab; EditorGUIUtility.PingObject(selected.prefab); }
                        if (GUILayout.Button("在地图中放置")) Emerge.PixelMap.Editor.PixelMapEditorWindow.OpenPropsTab(selected.mapProp);
                        if (GUILayout.Button("编辑交互与对话")) PropEditorWindow.Open(selected.mapProp);
                    }
                    if (GUILayout.Button("保存角色资料")) AssetDatabase.SaveAssets();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                    if (selected != null)
                    {
                        UnityEditor.Editor.CreateCachedEditor(selected, typeof(CharacterDefinitionEditor), ref inspector);
                        inspector.OnInspectorGUI();
                    }
                    else EditorGUILayout.HelpBox("选择角色查看资料、第一日安排和资源引用。", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                }
            }
        }
    }
}
