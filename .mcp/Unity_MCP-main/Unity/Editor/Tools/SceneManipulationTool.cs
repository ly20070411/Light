using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for scene manipulation operations
    /// </summary>
    public class SceneManipulationTool : McpToolBase
    {
        public override string ToolName => "scene_manipulate";
        public override string Description => "Manipulate scene objects and hierarchy";
        public override string Category => "scene";

        [Serializable]
        public class SceneParameters
        {
            public string action = "list_root_objects";
            public string name = "New Game Object";
            public string parentName = "";
            public string targetName = "";
            public float[] position = null;
            public float[] rotation = null;
            public float[] scale = null;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<SceneParameters>(parameters);
                var action = (args.action ?? "list_root_objects").Trim().ToLowerInvariant();

                return action switch
                {
                    "create_object" => CreateObject(args),
                    "delete_object" => DeleteObject(args),
                    "set_transform" => SetTransform(args),
                    "list_root_objects" => ListRootObjects(),
                    _ => CreateErrorResponse($"Unknown action '{args.action}'. Supported actions: create_object, delete_object, set_transform, list_root_objects")
                };
            }
            catch (Exception e)
            {
                LogError($"Scene manipulation failed: {e.Message}");
                return CreateErrorResponse($"Scene manipulation failed: {e.Message}", e.StackTrace);
            }
        }

        private object CreateObject(SceneParameters args)
        {
            var name = string.IsNullOrWhiteSpace(args.name) ? "New Game Object" : args.name;
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create GameObject");

            if (!string.IsNullOrWhiteSpace(args.parentName))
            {
                var parent = GameObject.Find(args.parentName);
                if (parent != null)
                {
                    gameObject.transform.SetParent(parent.transform);
                }
            }

            ApplyTransform(gameObject.transform, args);
            MarkSceneDirty(gameObject.scene);

            return CreateSuccessResponse(new
            {
                name = gameObject.name,
                instanceId = gameObject.GetEntityId(), // Use GetEntityId (GetInstanceID is obsolete)
                scene = gameObject.scene.name
            }, "GameObject created");
        }

        private object DeleteObject(SceneParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.targetName))
            {
                return CreateErrorResponse("Target name is required to delete an object.");
            }

            var target = GameObject.Find(args.targetName);
            if (target == null)
            {
                return CreateErrorResponse($"GameObject '{args.targetName}' not found.");
            }

            Undo.DestroyObjectImmediate(target);
            MarkSceneDirty(target.scene);

            return CreateSuccessResponse(new
            {
                name = args.targetName
            }, "GameObject deleted");
        }

        private object SetTransform(SceneParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.targetName))
            {
                return CreateErrorResponse("Target name is required to set transform.");
            }

            var target = GameObject.Find(args.targetName);
            if (target == null)
            {
                return CreateErrorResponse($"GameObject '{args.targetName}' not found.");
            }

            Undo.RecordObject(target.transform, "Set Transform");
            ApplyTransform(target.transform, args);
            MarkSceneDirty(target.scene);

            return CreateSuccessResponse(new
            {
                name = target.name,
                position = new { x = target.transform.position.x, y = target.transform.position.y, z = target.transform.position.z },
                rotation = new { x = target.transform.eulerAngles.x, y = target.transform.eulerAngles.y, z = target.transform.eulerAngles.z },
                scale = new { x = target.transform.localScale.x, y = target.transform.localScale.y, z = target.transform.localScale.z }
            }, "Transform updated");
        }

        private object ListRootObjects()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var rootInfo = new List<object>();

            foreach (var root in roots)
            {
                rootInfo.Add(new
                {
                    name = root.name,
                    instanceId = root.GetEntityId(), // Use GetEntityId (GetInstanceID is obsolete)
                    childCount = root.transform.childCount,
                    activeSelf = root.activeSelf
                });
            }

            return CreateSuccessResponse(new
            {
                scene = scene.name,
                rootCount = roots.Length,
                roots = rootInfo
            }, "Root objects listed");
        }

        private void ApplyTransform(Transform target, SceneParameters args)
        {
            if (args.position?.Length == 3)
            {
                target.position = new Vector3(args.position[0], args.position[1], args.position[2]);
            }

            if (args.rotation?.Length == 3)
            {
                target.eulerAngles = new Vector3(args.rotation[0], args.rotation[1], args.rotation[2]);
            }

            if (args.scale?.Length == 3)
            {
                target.localScale = new Vector3(args.scale[0], args.scale[1], args.scale[2]);
            }
        }

        private void MarkSceneDirty(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
