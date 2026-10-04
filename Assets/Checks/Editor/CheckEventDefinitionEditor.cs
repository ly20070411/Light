using System;
using UnityEditor;
using UnityEngine;

namespace Emerge.Checks.Editor
{
    [CustomEditor(typeof(CheckEventDefinition))]
    public sealed class CheckEventDefinitionEditor : UnityEditor.Editor
    {
        private static readonly string[] Behaviors = { "父母 / 学习与强化", "子孙 / 制作与修复", "官鬼 / 被动应对", "妻财 / 战斗与控制", "兄弟 / 探索与说服", "我 / 思考与观察" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("启用起卦后先投掷三枚铜钱六次，再依据本次日期排盘与计算行为加值。提交行动时结算并应用分支，关闭或读档后保留本次结果。", MessageType.Info);
            Field("eventId", "事件 ID（唯一且稳定）"); Field("title", "事件名称"); Field("speaker", "说话者");
            Field("intro", "进入检定时的对话"); Field("completedText", "默认后续剧情");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("起卦与排盘", EditorStyles.boldLabel);
            Field("useDivination", "启用六爻起卦");
            if (serializedObject.FindProperty("useDivination").boolValue)
            {
                Field("divinationMonth", "月令（例：巳月）"); Field("divinationDay", "日辰（例：戊子日）");
                Field("useFixedDivinationSeed", "使用固定随机种子（测试）");
                if (serializedObject.FindProperty("useFixedDivinationSeed").boolValue) Field("divinationSeed", "随机种子");
                EditorGUILayout.HelpBox("日期在创建检定会话时固定。固定种子用于复现铜钱结果；已经准备的会话不会因重新交互而重摇。", MessageType.None);
            }
            Field("revealDifficultyBeforeChoice", "选择前展示目标值");
            var options = serializedObject.FindProperty("options");
            EditorGUILayout.Space(); EditorGUILayout.LabelField("行动选项", EditorStyles.boldLabel);
            int remove = -1;
            for (int i = 0; i < options.arraySize; i++)
            {
                var option = options.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                option.isExpanded = EditorGUILayout.Foldout(option.isExpanded, "行动 " + (i + 1) + " · " + option.FindPropertyRelative("label").stringValue, true);
                if (GUILayout.Button("删除", GUILayout.Width(52))) remove = i;
                EditorGUILayout.EndHorizontal();
                if (option.isExpanded)
                {
                    Relative(option, "id", "选项 ID"); Relative(option, "label", "按钮文字");
                    var behavior = option.FindPropertyRelative("behavior");
                    behavior.enumValueIndex = EditorGUILayout.Popup("行为类型", behavior.enumValueIndex, Behaviors);
                    Relative(option, "targetValue", "检定目标值"); Relative(option, "requiredFlags", "前置标记（全部满足）");
                    Relative(option, "requiredItemKey", "前置物品键");
                    if (!string.IsNullOrWhiteSpace(option.FindPropertyRelative("requiredItemKey").stringValue))
                    { Relative(option, "requiredItemAmount", "需要的物品数量"); Relative(option, "consumeRequiredItem", "提交行动时消耗物品"); }
                    Outcome(option.FindPropertyRelative("success"), "成功分支");
                    Outcome(option.FindPropertyRelative("failure"), "失败分支");
                }
                EditorGUILayout.EndVertical();
            }
            if (remove >= 0) options.DeleteArrayElementAtIndex(remove);
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("添加行动"))
            {
                var asset = (CheckEventDefinition)target;
                Undo.RecordObject(asset, "添加检定行动");
                asset.options.Add(new CheckOptionDefinition { id = Guid.NewGuid().ToString("N"), label = "新行动",
                    success = new CheckOutcomeDefinition(), failure = new CheckOutcomeDefinition() });
                EditorUtility.SetDirty(asset);
            }
            if (!((CheckEventDefinition)target).Validate(out string error)) EditorGUILayout.HelpBox("配置尚未完成：" + error, MessageType.Warning);
        }

        private void Field(string name, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), new GUIContent(label), true);
        private static void Relative(SerializedProperty source, string name, string label)
            => EditorGUILayout.PropertyField(source.FindPropertyRelative(name), new GUIContent(label), true);
        private static void Outcome(SerializedProperty source, string label)
        {
            source.isExpanded = EditorGUILayout.Foldout(source.isExpanded, label, true);
            if (!source.isExpanded) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("结果对白");
            var text = source.FindPropertyRelative("text"); text.stringValue = EditorGUILayout.TextArea(text.stringValue, GUILayout.MinHeight(44));
            EditorGUILayout.LabelField("后续剧情");
            var continuation = source.FindPropertyRelative("continuation"); continuation.stringValue = EditorGUILayout.TextArea(continuation.stringValue, GUILayout.MinHeight(44));
            Relative(source, "grantedFlags", "设置剧情标记"); Relative(source, "rewardItemKey", "奖励物品键");
            if (!string.IsNullOrWhiteSpace(source.FindPropertyRelative("rewardItemKey").stringValue))
            { Relative(source, "rewardItemName", "奖励物品名称"); Relative(source, "rewardItemAmount", "奖励数量"); }
            Relative(source, "contaminationDelta", "侵染变化（情境测试）");
            EditorGUI.indentLevel--;
        }
    }
}
