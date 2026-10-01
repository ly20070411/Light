using UnityEditor;
using UnityEngine;

namespace Emerge.PixelMap.Editor
{
    [CustomEditor(typeof(MapBlockDefinition))]
    internal sealed class MapBlockDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            if (changed) ApplyToScene((MapBlockDefinition)target);
        }

        internal static void ApplyToScene(MapBlockDefinition definition)
        {
            if (definition == null) return;
            var instances = Resources.FindObjectsOfTypeAll<MapBlockInstance>();
            foreach (var instance in instances)
            {
                if (instance == null || instance.Definition != definition ||
                    EditorUtility.IsPersistent(instance)) continue;
                Undo.RecordObject(instance.gameObject, "更新地图方块属性");
                instance.ApplyDefinition();
                EditorUtility.SetDirty(instance);
            }
            SceneView.RepaintAll();
        }
    }
}
