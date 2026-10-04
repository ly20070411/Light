using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Props.Editor
{
    // Unity's Duplicate command copies serialized IDs. Give every placed instance its own save identity.
    [InitializeOnLoad]
    internal static class PropSceneSync
    {
        private static bool pending = true;
        private static readonly Dictionary<string, PropInstance> owners = new Dictionary<string, PropInstance>();
        static PropSceneSync()
        {
            EditorApplication.hierarchyChanged += () => pending = true;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (!pending || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            pending = false;
            var instances = Resources.FindObjectsOfTypeAll<PropInstance>();
            var current = new HashSet<PropInstance>(instances);
            foreach (var id in new List<string>(owners.Keys))
                if (owners[id] == null || !current.Contains(owners[id]) || owners[id].InstanceId != id) owners.Remove(id);
            foreach (var instance in instances)
            {
                if (EditorUtility.IsPersistent(instance) || !instance.gameObject.scene.IsValid()) continue;
                if (string.IsNullOrWhiteSpace(instance.InstanceId) ||
                    (owners.TryGetValue(instance.InstanceId, out var existing) && existing != instance))
                {
                    Undo.RecordObject(instance, "分配独立道具实例 ID"); instance.RenewIdentity();
                    EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
                }
                owners[instance.InstanceId] = instance;
            }
        }
        internal static void SyncNow() { pending = true; Tick(); }
    }
}
