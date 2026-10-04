using Emerge.PixelMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Props.Editor
{
    public static class PropPlacementService
    {
        public static PropInstance Place(PropDefinition definition, Transform root, Vector3 position, float angle,
            MapPlacementMode mode, Vector2 grid)
        {
            if (definition == null || root == null) return null;
            var obj = new GameObject(definition.DisplayName);
            obj.transform.SetParent(root, false);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(0f, 0f, definition.allowRotation ? angle : 0f);
            var instance = obj.AddComponent<PropInstance>();
            instance.Configure(definition, mode, new Vector2Int(Mathf.RoundToInt(position.x / Mathf.Max(.01f, grid.x)),
                Mathf.RoundToInt(position.y / Mathf.Max(.01f, grid.y))));
            Undo.RegisterCreatedObjectUndo(obj, "放置交互道具");
            EditorSceneManager.MarkSceneDirty(obj.scene);
            return instance;
        }
        public static PropInstance FindAt(Transform root, Vector3 world)
        {
            if (root == null) return null;
            PropInstance best = null; float distance = float.PositiveInfinity;
            foreach (var instance in root.GetComponentsInChildren<PropInstance>(true))
            {
                if (instance.Definition == null) continue;
                if (Mathf.Abs(instance.transform.position.z - world.z) > .001f) continue;
                Vector3 local = instance.transform.InverseTransformPoint(world);
                bool hit = false;
                foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer.bounds.Contains(new Vector3(world.x, world.y, renderer.bounds.center.z))) { hit = true; break; }
                if (!hit && instance.Definition.sprite == null && Mathf.Abs(local.x) < instance.Definition.worldSize.x / 2f &&
                    Mathf.Abs(local.y) < instance.Definition.worldSize.y / 2f) hit = true;
                if (!hit) continue;
                float z = Mathf.Abs(instance.transform.position.z - world.z);
                if (z < distance) { best = instance; distance = z; }
            }
            return best;
        }
        public static bool Erase(Transform root, Vector3 world)
        {
            var instance = FindAt(root, world);
            if (instance == null) return false;
            var scene = instance.gameObject.scene;
            Undo.DestroyObjectImmediate(instance.gameObject); EditorSceneManager.MarkSceneDirty(scene); return true;
        }
    }
}
