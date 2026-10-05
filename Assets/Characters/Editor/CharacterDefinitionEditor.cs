using UnityEditor;
using UnityEngine;

namespace Emerge.Characters.Editor
{
    [CustomEditor(typeof(CharacterDefinition))]
    public sealed class CharacterDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(true)) Field("id", "角色 ID（剧情调用键）");
            Section("身份与定位"); Field("characterName", "姓名（未命名时留空）"); Field("roleLabel", "职能显示名");
            Enum("gender", "性别", new[] { "男", "女" });
            Enum("narrativeRole", "剧情定位", new[] { "玩家角色", "主要人物", "次要人物", "普通 NPC" });
            Enum("team", "所属队伍", new[] { "当前勘察队", "前一支科考队" });
            Field("faction", "势力"); Field("department", "部门 / 学院"); Field("title", "身份 / 职称"); Field("function", "队内职能");
            Section("卦象与权能"); Field("hexagram", "卦象");
            Enum("rank", "层级", new[] { "天眼", "掌卦", "卦主" });
            Field("abilityName", "权能名称"); Field("abilityDescription", "权能设定");
            Section("人物资料"); Field("background", "背景与人物经历"); Field("appearanceNotes", "服饰与形象要求");
            Section("第一日安排"); Field("dayOneLocation", "所在场景"); Field("dayOneActivity", "正在进行的活动"); Field("dayOneTask", "任务安排");
            Section("资源引用"); Field("mapSprite", "地图形象"); Field("portrait", "对话头像");
            Field("prefab", "静态角色预制体"); Field("mapProp", "地图道具定义");
            Section("策划备注"); Field("planningNotes", "待定项 / 注意事项");
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            var character = (CharacterDefinition)target;
            if (changed) CharacterAssetFactory.SyncMapProp(character);
            if (character.IsNamePending) EditorGUILayout.HelpBox("尚未命名，当前使用职能显示名。补充姓名后，角色 ID 与已有引用保持不变。", MessageType.Info);
        }

        private void Field(string field, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(field), new GUIContent(label), true);
        private void Enum(string field, string label, string[] values)
        {
            var property = serializedObject.FindProperty(field);
            property.enumValueIndex = EditorGUILayout.Popup(label, property.enumValueIndex, values);
        }
        private static void Section(string title) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(title, EditorStyles.boldLabel); }
    }
}
