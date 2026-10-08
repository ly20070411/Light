using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.Story.Editor
{
    public sealed class StoryEditorWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "总览", "剧情节点", "道具", "场景", "角色", "设定", "待确认" };
        [SerializeField] private StoryProjectDefinition project;
        [SerializeField] private int tab, day;
        [SerializeField] private string selectedId = "", search = "", route = "";
        [SerializeField] private bool showOriginal;
        private SerializedObject serialized;
        private Vector2 listScroll, detailScroll;
        private string loadError = "";

        [MenuItem("Tools/剧情编辑器")]
        public static void Open()
        {
            var window = GetWindow<StoryEditorWindow>("剧情编辑器");
            window.minSize = new Vector2(1100, 680);
            window.LoadDefault(); window.Show();
        }
        private void OnEnable() { Undo.undoRedoPerformed += OnUndo; }
        private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; }
        private void OnUndo() { serialized?.Update(); Repaint(); }
        private void LoadDefault()
        {
            try
            {
                project = AssetDatabase.LoadAssetAtPath<StoryProjectDefinition>(StoryDraftImporter.ProjectPath);
                if (project == null) project = StoryDraftImporter.EnsureInstalled();
                serialized = new SerializedObject(project); loadError = "";
            }
            catch (Exception error) { loadError = error.Message; }
        }
        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var chosen = (StoryProjectDefinition)EditorGUILayout.ObjectField(project, typeof(StoryProjectDefinition), false, GUILayout.MinWidth(280));
                if (EditorGUI.EndChangeCheck()) { project = chosen; serialized = project == null ? null : new SerializedObject(project); selectedId = ""; }
                if (GUILayout.Button("打开初始稿", EditorStyles.toolbarButton, GUILayout.Width(100))) LoadDefault();
                using (new EditorGUI.DisabledScope(project == null))
                {
                    if (GUILayout.Button("保存项目", EditorStyles.toolbarButton, GUILayout.Width(90))) Save();
                    if (GUILayout.Button("校验资料", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    { Save(); StoryAuthoringValidation.ValidateAndWrite(); }
                    if (GUILayout.Button("定位资产", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    { Selection.activeObject = project; EditorGUIUtility.PingObject(project); }
                }
            }
            EditorGUILayout.HelpBox("编辑资料 · 当前稿独立保存。道具效果与剧情流程尚未接入运行场景。修改可用 Ctrl+Z 撤销。", MessageType.Info);
            if (project == null)
            {
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(loadError) ? "选择剧情项目，或打开初始稿。" : loadError, MessageType.Warning);
                return;
            }
            if (serialized == null || serialized.targetObject != project) serialized = new SerializedObject(project);
            serialized.Update();
            int nextTab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(28));
            if (nextTab != tab) { tab = nextTab; selectedId = ""; detailScroll = Vector2.zero; search = ""; }
            if (tab == 0) DrawOverview();
            else DrawRecords();
            serialized.ApplyModifiedProperties();
        }
        private void Save()
        {
            if (project == null) return;
            serialized?.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(project);
            ShowNotification(new GUIContent("剧情项目已保存"));
        }
        private void DrawOverview()
        {
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            Field(serialized, "title", "项目名称"); Field(serialized, "revision", "版本");
            Area(serialized, "scope", "当前制作范围", 58);
            EditorGUILayout.LabelField($"{project.nodes.Count} 个剧情节点    {project.items.Count} 项道具    {project.scenes.Count} 个场景    {project.characters.Count} 名角色", EditorStyles.boldLabel);
            Section("流程索引（点击跳转）");
            foreach (var group in project.nodes.Where(x => x != null).OrderBy(x => x.day).ThenBy(x => x.order).GroupBy(x => x.day))
            {
                EditorGUILayout.LabelField(group.Key == 0 ? "后续结局 / 占位" : "Day " + group.Key, EditorStyles.boldLabel);
                foreach (var node in group)
                    if (GUILayout.Button(NodeLabel(node) + (node.optional ? "  · 可选" : "") + (node.placeholder ? "  · 占位" : ""), EditorStyles.miniButton))
                        Jump(1, node.id);
            }
            Section("剧情规则");
            var rules = serialized.FindProperty("rules");
            for (int i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                { Field(rule, "title", "规则"); Area(rule, "text", "内容", 65); Area(rule, "notes", "编辑备注", 45); Source(rule); }
            }
            Section("来源档案");
            foreach (var source in project.sources)
                EditorGUILayout.SelectableLabel(source.fileName + "\nSHA256: " + source.sha256 + "\n" + source.notes, EditorStyles.textArea, GUILayout.Height(62));
            Section("地图布局参考");
            Field(serialized, "layoutReference", "原始结构图");
            if (project.layoutReference != null)
            {
                var rect = GUILayoutUtility.GetRect(400, 430, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, project.layoutReference, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndScrollView();
        }
        private string ListName => new[] { "", "nodes", "items", "scenes", "characters", "lore", "issues" }[tab];
        private IList Records => tab == 1 ? (IList)project.nodes : tab == 2 ? (IList)project.items : tab == 3 ? (IList)project.scenes : tab == 4 ? (IList)project.characters : tab == 5 ? (IList)project.lore : project.issues;
        private void DrawRecords()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(290)))
                {
                    search = EditorGUILayout.TextField("搜索", search);
                    if (tab == 1)
                    {
                        day = EditorGUILayout.Popup("日期", day, new[] { "全部", "Day 1", "Day 2", "Day 3", "Day 4", "后续 / Day 0" });
                        var routes = new[] { "全部" }.Concat(project.nodes.Select(x => x.route).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()).ToArray();
                        int currentRoute = Math.Max(0, Array.IndexOf(routes, route));
                        int selectedRoute = EditorGUILayout.Popup("线路", currentRoute, routes);
                        route = selectedRoute == 0 ? "" : routes[selectedRoute];
                    }
                    var records = serialized.FindProperty(ListName);
                    listScroll = EditorGUILayout.BeginScrollView(listScroll, "box");
                    int count = 0;
                    for (int i = 0; i < records.arraySize; i++)
                    {
                        var record = records.GetArrayElementAtIndex(i);
                        if (!Matches(record)) continue;
                        count++;
                        string id = record.FindPropertyRelative("id").stringValue;
                        bool active = selectedId == id;
                        var oldColor = GUI.backgroundColor;
                        if (active) GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
                        if (GUILayout.Button(RecordLabel(record), GUILayout.MinHeight(40))) { selectedId = id; detailScroll = Vector2.zero; }
                        GUI.backgroundColor = oldColor;
                    }
                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.LabelField($"显示 {count} / {records.arraySize}");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("新增")) Mutate("新增剧情记录", () => AddRecord(null));
                        using (new EditorGUI.DisabledScope(SelectedIndex() < 0))
                        {
                            if (GUILayout.Button("复制")) Mutate("复制剧情记录", () => AddRecord(Records[SelectedIndex()]));
                            if (GUILayout.Button("删除")) Mutate("删除剧情记录", () => { Records.RemoveAt(SelectedIndex()); selectedId = ""; });
                        }
                    }
                    EditorGUILayout.HelpBox("删除可撤销；引用该记录的节点需同步调整。稳定 ID 用于关联，改名保留 ID。", MessageType.None);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    int index = SelectedIndex();
                    if (index < 0) EditorGUILayout.HelpBox("选择左侧记录查看或编辑。", MessageType.Info);
                    else
                    {
                        var record = serialized.FindProperty(ListName).GetArrayElementAtIndex(index);
                        using (new EditorGUI.DisabledScope(true)) Field(record, "id", "稳定 ID");
                        switch (tab)
                        {
                            case 1: DrawNode(record); break;
                            case 2: DrawItem(record); break;
                            case 3: DrawScene(record); break;
                            case 4: DrawCharacter(record); break;
                            case 5: Field(record, "title", "标题"); Field(record, "secret", "剧情真相 / 剧透"); Area(record, "text", "设定内容", 160); Source(record); break;
                            case 6: Field(record, "title", "事项"); Field(record, "status", "状态"); Area(record, "detail", "问题详情", 120); Area(record, "resolution", "确认结果", 90); Source(record); break;
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        private int SelectedIndex()
        {
            var records = serialized.FindProperty(ListName);
            for (int i = 0; i < records.arraySize; i++)
                if (records.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == selectedId) return i;
            return -1;
        }
        private bool Matches(SerializedProperty record)
        {
            if (tab == 1)
            {
                int recordDay = record.FindPropertyRelative("day").intValue;
                if (day != 0 && recordDay != (day == 5 ? 0 : day)) return false;
                if (!string.IsNullOrEmpty(route) && record.FindPropertyRelative("route").stringValue != route) return false;
            }
            if (string.IsNullOrWhiteSpace(search)) return true;
            foreach (string field in new[] { "id", "title", "name", "summary", "description", "sourceNumber", "eventNumber", "role", "text", "detail" })
            {
                var value = record.FindPropertyRelative(field);
                if (value != null && value.propertyType == SerializedPropertyType.String && value.stringValue.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
        private string RecordLabel(SerializedProperty record)
        {
            string title = Value(record, "title"); if (string.IsNullOrEmpty(title)) title = Value(record, "name");
            string suffix = tab == 1 ? $"Day {record.FindPropertyRelative("day").intValue} · {Value(record, "route")} · {Value(record, "eventNumber")}" : Value(record, "id");
            var placeholder = record.FindPropertyRelative("placeholder");
            return title + (placeholder != null && placeholder.boolValue ? "（占位）" : "") + "\n" + suffix;
        }
        private void DrawNode(SerializedProperty node)
        {
            Field(node, "title", "事件名称");
            using (new EditorGUILayout.HorizontalScope()) { Field(node, "day", "日期"); Field(node, "order", "排序"); }
            Field(node, "eventNumber", "细纲编号"); Field(node, "route", "线路"); Field(node, "kind", "事件类型");
            using (new EditorGUILayout.HorizontalScope()) { Field(node, "optional", "可选事件"); Field(node, "placeholder", "尚待展开 / 占位"); }
            Area(node, "summary", "剧情概要", 70); Area(node, "trigger", "触发条件", 55);
            Area(node, "completion", "完成条件", 55); Area(node, "interaction", "交互与流程", 100);
            Area(node, "staging", "场面与演出", 75); Area(node, "designNotes", "制作 / 待定备注", 80);
            Section("关联资料");
            References(node.FindPropertyRelative("sceneIds"), "场景", 3);
            References(node.FindPropertyRelative("characterIds"), "角色", 4);
            References(node.FindPropertyRelative("itemIds"), "道具", 2);
            Section("对白（整理稿）");
            var lines = node.FindPropertyRelative("lines");
            for (int i = 0; i < lines.arraySize; i++)
            {
                var line = lines.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    RelationPopup(line.FindPropertyRelative("speakerId"), "关联说话人", 4);
                    Field(line, "speaker", "显示称呼（可覆盖）"); Area(line, "text", "对白", 65); Area(line, "notes", "备注", 40);
                    if (ArrayControls(lines, i, "此条对白")) break;
                }
            }
            if (GUILayout.Button("添加对白")) AppendRecord(lines, new StoryDialogueRecord());
            Section("分支与下一步");
            var branches = node.FindPropertyRelative("branches");
            for (int i = 0; i < branches.arraySize; i++)
            {
                var branch = branches.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUI.DisabledScope(true)) Field(branch, "id", "分支 ID");
                    Field(branch, "label", "选择 / 流转名称"); Area(branch, "condition", "条件（需求文案）", 60);
                    RelationPopup(branch.FindPropertyRelative("targetId"), "目标节点", 1);
                    Field(branch, "hiddenValueDelta", "隐藏值变化"); Field(branch, "hiddenValueConfirmed", "该分值已确认");
                    References(branch.FindPropertyRelative("grantItemIds"), "获得道具", 2);
                    References(branch.FindPropertyRelative("consumeItemIds"), "消耗道具", 2);
                    Area(branch, "notes", "分支备注", 55);
                    if (ArrayControls(branches, i, "此分支")) break;
                }
            }
            if (GUILayout.Button("添加分支")) AppendRecord(branches, new StoryBranchRecord { id = NewId("branch") });
            Source(node);
            Original(node, "sourceText");
            Section("指向本事件的节点");
            string id = node.FindPropertyRelative("id").stringValue;
            var incoming = project.nodes.Where(x => x.branches.Any(b => b.targetId == id)).ToArray();
            if (incoming.Length == 0) EditorGUILayout.LabelField("暂无前置连线（可为独立交互或入口）");
            foreach (var entry in incoming) if (GUILayout.Button(NodeLabel(entry))) Jump(1, entry.id);
        }
        private void DrawItem(SerializedProperty item)
        {
            Field(item, "name", "道具名称"); Field(item, "sourceNumber", "需求表编号"); Field(item, "placeholder", "正文补充占位");
            Area(item, "function", "功能类别", 45); Area(item, "description", "道具简介", 90);
            Area(item, "programEffect", "效果需求（未实现）", 95); Area(item, "acquisition", "获取条件", 90);
            Area(item, "artNotes", "美术需求", 65); Area(item, "notes", "制作备注", 75); Field(item, "modified", "原表修改记录");
            Field(item, "asset", "关联道具资产");
            var asset = item.FindPropertyRelative("asset").objectReferenceValue as Emerge.Props.PropDefinition;
            using (new EditorGUI.DisabledScope(asset == null))
            {
                if (GUILayout.Button("在道具编辑器打开关联资产")) PropEditorWindow.Open(asset);
                if (GUILayout.Button("定位道具资产")) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
            }
            EditorGUILayout.HelpBox("此处保存剧情需求文案。关联资产的行为初始为 None，修改此处文案不会赋予运行时效果。", MessageType.None);
            Source(item); Usage(Value(item, "id"), 2);
        }
        private void DrawScene(SerializedProperty scene)
        {
            Field(scene, "name", "场景名称"); Field(scene, "sourceNumber", "来源编号");
            Field(scene, "dayAvailability", "开放日期"); Field(scene, "mapGroup", "区域分组");
            Area(scene, "description", "场景需求", 150); Area(scene, "notes", "制作备注", 100);
            Source(scene); Usage(Value(scene, "id"), 3);
        }
        private void DrawCharacter(SerializedProperty character)
        {
            Field(character, "name", "角色名"); Field(character, "role", "剧情定位");
            Field(character, "faction", "势力"); Field(character, "department", "部门");
            Field(character, "identity", "身份 / 职能"); Field(character, "hexagram", "卦象");
            Area(character, "notes", "剧情备注", 130); Field(character, "asset", "现有角色资产");
            var asset = character.FindPropertyRelative("asset").objectReferenceValue;
            using (new EditorGUI.DisabledScope(asset == null))
                if (GUILayout.Button("查看现有角色定义")) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
            if (GUILayout.Button("打开角色编辑器")) Emerge.Characters.Editor.CharacterEditorWindow.Open();
            Source(character); Original(character, "sourceText"); Usage(Value(character, "id"), 4);
        }
        private void Usage(string id, int kind)
        {
            Section("引用此资料的事件");
            var nodes = project.nodes.Where(x => (kind == 2 ? x.itemIds.Contains(id) || x.branches.Any(b => b.grantItemIds.Contains(id) || b.consumeItemIds.Contains(id)) : kind == 3 ? x.sceneIds.Contains(id) : x.characterIds.Contains(id) || x.lines.Any(l => l.speakerId == id))).ToArray();
            if (nodes.Length == 0) EditorGUILayout.LabelField("暂无事件引用");
            foreach (var node in nodes) if (GUILayout.Button(NodeLabel(node))) Jump(1, node.id);
        }
        private void References(SerializedProperty ids, string label, int kind)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            for (int i = 0; i < ids.arraySize; i++)
                using (new EditorGUILayout.HorizontalScope())
                {
                    RelationPopup(ids.GetArrayElementAtIndex(i), "", kind);
                    if (GUILayout.Button("×", GUILayout.Width(25))) { ids.DeleteArrayElementAtIndex(i); break; }
                }
            if (GUILayout.Button("＋ " + label, EditorStyles.miniButton)) { ids.InsertArrayElementAtIndex(ids.arraySize); ids.GetArrayElementAtIndex(ids.arraySize - 1).stringValue = ""; }
        }
        private void RelationPopup(SerializedProperty id, string label, int kind)
        {
            var choices = RelationChoices(kind);
            var ids = new List<string> { "" }; var labels = new List<string> { "（未指定）" };
            foreach (var pair in choices) { ids.Add(pair.Key); labels.Add(pair.Value); }
            int current = ids.IndexOf(id.stringValue);
            if (current < 0) { ids.Add(id.stringValue); labels.Add("⚠ 引用缺失：" + id.stringValue); current = ids.Count - 1; }
            using (new EditorGUILayout.HorizontalScope())
            {
                int next = string.IsNullOrEmpty(label) ? EditorGUILayout.Popup(current, labels.ToArray()) : EditorGUILayout.Popup(label, current, labels.ToArray());
                if (next != current) id.stringValue = ids[next];
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(id.stringValue)))
                    if (GUILayout.Button("查看", GUILayout.Width(44))) Jump(kind, id.stringValue);
            }
        }
        private IEnumerable<KeyValuePair<string, string>> RelationChoices(int kind)
        {
            if (kind == 1) return project.nodes.Select(x => new KeyValuePair<string, string>(x.id, NodeLabel(x)));
            if (kind == 2) return project.items.Select(x => new KeyValuePair<string, string>(x.id, x.name + " · " + x.id));
            if (kind == 3) return project.scenes.Select(x => new KeyValuePair<string, string>(x.id, x.name + " · " + x.id));
            return project.characters.Select(x => new KeyValuePair<string, string>(x.id, x.name + " · " + x.id));
        }
        private void Jump(int targetTab, string id)
        {
            serialized.ApplyModifiedProperties(); tab = targetTab; selectedId = id;
            search = ""; route = ""; day = 0; detailScroll = Vector2.zero;
            GUIUtility.ExitGUI();
        }
        private void Source(SerializedProperty record)
        {
            Section("来源定位");
            using (new EditorGUI.DisabledScope(true)) Area(record, "source", "原件位置", 48);
        }
        private void Original(SerializedProperty record, string name)
        {
            showOriginal = EditorGUILayout.Foldout(showOriginal, "原始文本（只读，与整理稿分开保存）", true);
            if (showOriginal) using (new EditorGUI.DisabledScope(true)) Area(record, name, "原文", 220);
        }
        private static bool ArrayControls(SerializedProperty array, int index, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("上移", EditorStyles.miniButton, GUILayout.Width(45))) { array.MoveArrayElement(index, index - 1); return true; }
                using (new EditorGUI.DisabledScope(index == array.arraySize - 1))
                    if (GUILayout.Button("下移", EditorStyles.miniButton, GUILayout.Width(45))) { array.MoveArrayElement(index, index + 1); return true; }
                if (GUILayout.Button("删除" + label, EditorStyles.miniButton, GUILayout.Width(95))) { array.DeleteArrayElementAtIndex(index); return true; }
            }
            return false;
        }
        private void AppendRecord(SerializedProperty array, object record)
        {
            serialized.ApplyModifiedProperties();
            Undo.RecordObject(project, "添加剧情内容");
            var node = project.nodes[SelectedIndex()];
            if (record is StoryDialogueRecord line) node.lines.Add(line);
            else if (record is StoryBranchRecord branch) node.branches.Add(branch);
            EditorUtility.SetDirty(project); serialized.Update();
            GUIUtility.ExitGUI();
        }
        private void Mutate(string label, Action action)
        {
            serialized.ApplyModifiedProperties(); Undo.RecordObject(project, label); action();
            EditorUtility.SetDirty(project); serialized.Update(); detailScroll = Vector2.zero;
            GUIUtility.ExitGUI();
        }
        private void AddRecord(object source)
        {
            object record;
            if (source != null) record = JsonUtility.FromJson(JsonUtility.ToJson(source), source.GetType());
            else if (tab == 1) record = new StoryNodeRecord { title = "新事件", route = "共同", kind = "待编排", day = day > 0 && day < 5 ? day : 1, order = project.nodes.Count == 0 ? 10 : project.nodes.Max(x => x.order) + 10, placeholder = true };
            else if (tab == 2) record = new StoryItemRecord { name = "新道具", placeholder = true };
            else if (tab == 3) record = new StorySceneRecord { name = "新场景" };
            else if (tab == 4) record = new StoryCharacterRecord { name = "新角色" };
            else if (tab == 5) record = new StoryLoreRecord { title = "新设定" };
            else record = new StoryIssueRecord { title = "新待确认事项", status = "待确认" };
            string id = NewId(new[] { "", "node", "item", "scene", "character", "lore", "issue" }[tab]);
            record.GetType().GetField("id").SetValue(record, id);
            if (source != null)
            {
                var title = record.GetType().GetField(tab == 2 || tab == 3 || tab == 4 ? "name" : "title");
                title.SetValue(record, (string)title.GetValue(record) + " 副本");
                if (record is StoryNodeRecord node)
                    foreach (var branch in node.branches)
                    { if (branch.targetId == ((StoryNodeRecord)source).id) branch.targetId = id; branch.id = NewId("branch"); }
                if (record is StoryItemRecord item) item.asset = null;
                if (record is StoryCharacterRecord character) character.asset = null;
            }
            Records.Add(record); selectedId = id;
        }
        private static string NewId(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 10);
        private static string NodeLabel(StoryNodeRecord node) => $"Day {node.day} · {node.eventNumber} {node.title} [{node.route}]";
        private static string Value(SerializedProperty record, string field) => record.FindPropertyRelative(field)?.stringValue ?? "";
        private static void Section(string label) { EditorGUILayout.Space(8); EditorGUILayout.LabelField(label, EditorStyles.boldLabel); }
        private static void Field(SerializedObject target, string field, string label) => EditorGUILayout.PropertyField(target.FindProperty(field), new GUIContent(label), true);
        private static void Field(SerializedProperty target, string field, string label) => EditorGUILayout.PropertyField(target.FindPropertyRelative(field), new GUIContent(label), true);
        private static void Area(SerializedObject target, string field, string label, float height) => AreaProperty(target.FindProperty(field), label, height);
        private static void Area(SerializedProperty target, string field, string label, float height) => AreaProperty(target.FindPropertyRelative(field), label, height);
        private static void AreaProperty(SerializedProperty property, string label, float height)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            property.stringValue = EditorGUILayout.TextArea(property.stringValue, EditorStyles.textArea, GUILayout.MinHeight(height));
        }
    }
}
