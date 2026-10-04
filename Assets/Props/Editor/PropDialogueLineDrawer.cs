using UnityEditor;
using UnityEngine;

namespace Emerge.Props.Editor
{
    [CustomPropertyDrawer(typeof(PropDialogueLine))]
    internal sealed class PropDialogueLineDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 2 + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("text")) + 14f;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            var rect = new Rect(position.x, position.y, position.width, line);
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("speaker"), new GUIContent("说话人"));
            rect.y += line + 4;
            rect.height = EditorGUI.GetPropertyHeight(property.FindPropertyRelative("text"));
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("text"), new GUIContent("对话内容"));
            rect.y += rect.height + 4; rect.height = line;
            EditorGUI.PropertyField(rect, property.FindPropertyRelative("portrait"), new GUIContent("头像（可选）"));
            EditorGUI.EndProperty();
        }
    }
}
