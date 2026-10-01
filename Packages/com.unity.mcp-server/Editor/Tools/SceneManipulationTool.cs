using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for scene manipulation operations.
    ///
    /// The Node bridge publishes fine-grained tools (scene.createGameObject,
    /// scene.query, scene.modifyComponent, ...) and ProcessMcpRequest reaches
    /// this class through its category fallback, injecting the method suffix as
    /// "action" (camelCase is normalised to snake_case here). This tool must
    /// therefore accept the Node side's parameter names as well as the ones the
    /// Unity-side helpers use.
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

            // The two callers disagree on names: Node sends name/gameObjectName/
            // parent, the Unity-side helpers use name/targetName/parentName.
            public string name = "";
            public string gameObjectName = "";
            public string targetName = "";
            public string parentName = "";
            public string parent = "";
            public string primitive = "";
            public string[] components = null;
            public string tag = "";
            public int layer = 0;

            public int gameObjectId = 0;
            public int instanceId = 0;

            // Node serialises these as {"x":..,"y":..,"z":..}; older callers
            // send [x,y,z]. Both are accepted -- receiving an object in a float[]
            // field used to throw inside GetParameters<T>, which silently fell
            // back to a default-constructed parameter set (and therefore to the
            // wrong action).
            public object position = null;
            public object rotation = null;
            public object scale = null;
            public bool relative = false;

            // scene.modifyComponent
            public string componentType = "";
            public object properties = null;

            // scene.selectObjects
            public string[] objectNames = null;
            public int[] instanceIds = null;

            // scene.query
            public string filter = "";
            public bool includeInactive = false;
            public int maxDepth = -1;

            // scene.save / scene.load
            public string path = "";
            public string scenePath = "";
            public bool additive = false;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<SceneParameters>(parameters);
                var action = NormalizeAction(args.action);

                // scene.modifyComponent carries its own action
                // ("add"/"remove"/"modify") and InjectAction only fills the key
                // in when it is absent, so that value arrives here instead of
                // "modifyComponent". A component type disambiguates the two.
                if (!string.IsNullOrWhiteSpace(args.componentType) &&
                    (action == "add" || action == "remove" || action == "modify"))
                {
                    action = "modify_component";
                }

                return action switch
                {
                    "create_object" or "create_game_object" => CreateObject(args),
                    "delete_object" or "delete_game_object" => DeleteObject(args),
                    "set_transform" or "move_game_object" or "move" => SetTransform(args),
                    "list_root_objects" or "query" or "hierarchy" => QueryHierarchy(args),
                    "modify_component" => ModifyComponent(args),
                    "select_objects" or "select" => SelectObjects(args),
                    "save" or "save_scene" => SaveScene(args),
                    "load" or "load_scene" or "open" or "open_scene" => LoadScene(args),
                    _ => CreateErrorResponse(
                        $"Unknown action '{action}'. Supported actions: create_object, delete_object, " +
                        "set_transform, list_root_objects, query, modify_component, select_objects, save, load")
                };
            }
            catch (Exception e)
            {
                LogError($"Scene manipulation failed: {e.Message}");
                return CreateErrorResponse($"Scene manipulation failed: {e.Message}", e.StackTrace);
            }
        }

        // camelCase/PascalCase -> snake_case ("createGameObject" -> "create_object"),
        // so Node-side tool names like scene.createGameObject route correctly.
        private static string NormalizeAction(string raw)
        {
            string a = string.IsNullOrWhiteSpace(raw) ? "list_root_objects" : raw.Trim();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                char c = a[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && sb.Length > 0 && sb[sb.Length - 1] != '_')
                    {
                        sb.Append('_');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string ResolveName(SceneParameters args)
        {
            if (!string.IsNullOrWhiteSpace(args.gameObjectName)) return args.gameObjectName.Trim();
            if (!string.IsNullOrWhiteSpace(args.targetName)) return args.targetName.Trim();
            if (!string.IsNullOrWhiteSpace(args.name)) return args.name.Trim();
            return "";
        }

        private static int ResolveInstanceId(SceneParameters args)
        {
            if (args.gameObjectId != 0) return args.gameObjectId;
            if (args.instanceId != 0) return args.instanceId;
            return 0;
        }

        /// <summary>
        /// Resolves a GameObject by instance id first, then by name.
        /// </summary>
        private static GameObject FindTarget(SceneParameters args, out string error)
        {
            error = null;

            int id = ResolveInstanceId(args);
            if (id != 0)
            {
                var byId = EditorUtility.InstanceIDToObject(id) as GameObject;
                if (byId != null)
                {
                    return byId;
                }
            }

            string name = ResolveName(args);
            if (!string.IsNullOrWhiteSpace(name))
            {
                var byName = GameObject.Find(name);
                if (byName != null)
                {
                    return byName;
                }
            }

            error = id != 0
                ? $"No GameObject found for instanceId {id}."
                : $"GameObject '{name}' not found.";
            return null;
        }

        private object CreateObject(SceneParameters args)
        {
            var name = ResolveName(args);
            if (string.IsNullOrWhiteSpace(name)) name = "New Game Object";

            // A primitive is built when "primitive" names one, or when the
            // requested name is itself a primitive type ("Cube", "Sphere", ...).
            // The name fallback matters because the Node bridge's
            // scene.createGameObject schema exposes no "primitive" field
            // (additionalProperties is false), so naming the object is the only
            // way a caller can ask for a real cube through it.
            PrimitiveType primitiveType;
            bool isPrimitive = TryResolvePrimitive(args.primitive, out primitiveType) ||
                               TryResolvePrimitive(name, out primitiveType);

            GameObject gameObject = isPrimitive
                ? GameObject.CreatePrimitive(primitiveType)
                : new GameObject();
            gameObject.name = name;
            Undo.RegisterCreatedObjectUndo(gameObject, "Create GameObject");

            string parentName = !string.IsNullOrWhiteSpace(args.parentName) ? args.parentName : args.parent;
            if (!string.IsNullOrWhiteSpace(parentName))
            {
                var parent = GameObject.Find(parentName);
                if (parent != null)
                {
                    gameObject.transform.SetParent(parent.transform);
                }
            }

            ApplyTransform(gameObject.transform, args, false);

            // The Node schema advertises components/tag/layer but they were
            // previously ignored, so a caller could not build a usable object
            // in one round trip.
            var added = new List<string>();
            if (args.components != null)
            {
                foreach (string typeName in args.components)
                {
                    if (string.IsNullOrWhiteSpace(typeName)) continue;

                    Type componentType = FindComponentType(typeName.Trim());
                    if (componentType == null || gameObject.GetComponent(componentType) != null) continue;

                    Undo.AddComponent(gameObject, componentType);
                    added.Add(componentType.Name);
                }
            }

            if (!string.IsNullOrWhiteSpace(args.tag))
            {
                try
                {
                    gameObject.tag = args.tag.Trim();
                }
                catch (Exception)
                {
                    // The tag is not declared in this project; naming it in the
                    // response is enough, the object itself is still created.
                }
            }

            if (args.layer != 0)
            {
                gameObject.layer = args.layer;
            }

            MarkSceneDirty(gameObject.scene);

            return CreateSuccessResponse(new
            {
                name = gameObject.name,
                instanceId = gameObject.GetInstanceID(), // 2022.3 API: GetInstanceID (Unity 6 renamed it to GetEntityId)
                scene = gameObject.scene.name,
                primitive = isPrimitive ? primitiveType.ToString() : null,
                componentsAdded = added
            }, "GameObject created");
        }

        /// <summary>
        /// Resolves a primitive by name. Numeric strings are rejected because
        /// Enum.TryParse would otherwise accept "1" and silently build a Cube.
        /// </summary>
        private static bool TryResolvePrimitive(string raw, out PrimitiveType primitiveType)
        {
            primitiveType = default;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string trimmed = raw.Trim();
            if (char.IsDigit(trimmed[0]))
            {
                return false;
            }

            return Enum.TryParse(trimmed, true, out primitiveType) &&
                   Enum.IsDefined(typeof(PrimitiveType), primitiveType);
        }

        private object DeleteObject(SceneParameters args)
        {
            var target = FindTarget(args, out string error);
            if (target == null)
            {
                return CreateErrorResponse(error);
            }

            var scene = target.scene;
            string name = target.name;

            Undo.DestroyObjectImmediate(target);
            MarkSceneDirty(scene);

            return CreateSuccessResponse(new
            {
                name = name
            }, "GameObject deleted");
        }

        private object SetTransform(SceneParameters args)
        {
            var target = FindTarget(args, out string error);
            if (target == null)
            {
                return CreateErrorResponse(error);
            }

            Undo.RecordObject(target.transform, "Set Transform");
            ApplyTransform(target.transform, args, args.relative);
            MarkSceneDirty(target.scene);

            return CreateSuccessResponse(new
            {
                name = target.name,
                position = new { x = target.transform.position.x, y = target.transform.position.y, z = target.transform.position.z },
                rotation = new { x = target.transform.eulerAngles.x, y = target.transform.eulerAngles.y, z = target.transform.eulerAngles.z },
                scale = new { x = target.transform.localScale.x, y = target.transform.localScale.y, z = target.transform.localScale.z }
            }, "Transform updated");
        }

        private object QueryHierarchy(SceneParameters args)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var rootInfo = new List<object>();

            foreach (var root in roots)
            {
                CollectHierarchy(root.transform, args, 0, rootInfo);
            }

            return CreateSuccessResponse(new
            {
                scene = scene.name,
                rootCount = roots.Length,
                returned = rootInfo.Count,
                filter = string.IsNullOrWhiteSpace(args.filter) ? null : args.filter,
                maxDepth = args.maxDepth,
                includeInactive = args.includeInactive,
                roots = rootInfo
            }, rootInfo.Count == 0 ? "No matching root objects" : "Root objects listed");
        }

        private void CollectHierarchy(Transform transform, SceneParameters args, int depth, List<object> sink)
        {
            var go = transform.gameObject;

            if (args.includeInactive || go.activeInHierarchy)
            {
                bool matches = string.IsNullOrWhiteSpace(args.filter) ||
                               go.name.IndexOf(args.filter, StringComparison.OrdinalIgnoreCase) >= 0;

                if (matches)
                {
                    var components = new List<string>();
                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (component != null)
                        {
                            components.Add(component.GetType().Name);
                        }
                    }

                    sink.Add(new
                    {
                        name = go.name,
                        instanceId = go.GetInstanceID(), // 2022.3 API: GetInstanceID (Unity 6 renamed it to GetEntityId)
                        childCount = transform.childCount,
                        activeSelf = go.activeSelf,
                        depth = depth,
                        components = components
                    });
                }
            }

            if (args.maxDepth >= 0 && depth >= args.maxDepth)
            {
                return;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                CollectHierarchy(transform.GetChild(i), args, depth + 1, sink);
            }
        }

        private object ModifyComponent(SceneParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.componentType))
            {
                return CreateErrorResponse("componentType is required.");
            }

            var target = FindTarget(args, out string error);
            if (target == null)
            {
                return CreateErrorResponse(error);
            }

            // "add"/"remove"/"modify" come straight from the Node schema; when
            // the caller used modifyComponent the sub-action was normalised into
            // args.action, so read it back from there.
            string sub = NormalizeAction(args.action);
            if (sub != "add" && sub != "remove" && sub != "modify")
            {
                sub = "modify";
            }

            Type componentType = FindComponentType(args.componentType);
            if (componentType == null)
            {
                return CreateErrorResponse($"Component type '{args.componentType}' not found.");
            }

            if (sub == "remove")
            {
                var existing = target.GetComponent(componentType);
                if (existing == null)
                {
                    return CreateErrorResponse($"'{target.name}' has no {componentType.Name} component.");
                }
                Undo.DestroyObjectImmediate(existing);
                MarkSceneDirty(target.scene);
                return CreateSuccessResponse(new { name = target.name, component = componentType.Name }, "Component removed");
            }

            Component component = target.GetComponent(componentType);
            if (component == null)
            {
                if (sub == "modify")
                {
                    return CreateErrorResponse($"'{target.name}' has no {componentType.Name} component. Use action 'add'.");
                }
                component = Undo.AddComponent(target, componentType);
            }

            int applied = ApplyProperties(component, args.properties);
            EditorUtility.SetDirty(component);
            MarkSceneDirty(target.scene);

            return CreateSuccessResponse(new
            {
                name = target.name,
                component = componentType.Name,
                propertiesApplied = applied
            }, sub == "add" ? "Component added" : "Component modified");
        }

        private static Type FindComponentType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found = null;
                try
                {
                    found = assembly.GetType(typeName, false, true);
                }
                catch (Exception)
                {
                    // Some dynamic assemblies refuse GetType; ignore them.
                }

                if (found != null && typeof(Component).IsAssignableFrom(found))
                {
                    return found;
                }
            }

            // Fall back to a short-name scan so "Rigidbody" works without a namespace.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (typeof(Component).IsAssignableFrom(type) &&
                        string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Assigns public fields/properties from a JSON object. Returns how many
        /// values were applied; unknown members are skipped rather than fatal.
        /// </summary>
        private static int ApplyProperties(Component component, object rawProperties)
        {
            if (rawProperties == null)
            {
                return 0;
            }

            var token = rawProperties as JToken ?? JToken.FromObject(rawProperties);
            if (token.Type != JTokenType.Object)
            {
                return 0;
            }

            int applied = 0;
            var type = component.GetType();

            foreach (var property in (JObject)token)
            {
                if (TryAssignMember(component, type, property.Key, property.Value))
                {
                    applied++;
                }
            }

            return applied;
        }

        private static bool TryAssignMember(object target, Type type, string memberName, JToken value)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

            try
            {
                PropertyInfo property = type.GetProperty(memberName, Flags);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(target, ConvertToken(value, property.PropertyType), null);
                    return true;
                }

                FieldInfo field = type.GetField(memberName, Flags);
                if (field != null)
                {
                    field.SetValue(target, ConvertToken(value, field.FieldType));
                    return true;
                }
            }
            catch (Exception)
            {
                // Wrong shape for this member; leave it untouched.
            }

            return false;
        }

        private static object ConvertToken(JToken value, Type targetType)
        {
            if (targetType == typeof(Vector3)) return ToVector3(value, Vector3.zero);
            if (targetType == typeof(Vector2)) { var v = ToVector3(value, Vector3.zero); return new Vector2(v.x, v.y); }
            if (targetType == typeof(Color)) return ToColor(value);
            if (targetType == typeof(Quaternion))
            {
                var v = ToVector3(value, Vector3.zero);
                return Quaternion.Euler(v);
            }
            if (targetType.IsEnum) return Enum.Parse(targetType, value.ToString(), true);
            if (targetType == typeof(string)) return value.ToString();

            return Convert.ChangeType(value.ToObject(targetType), targetType);
        }

        private static Color ToColor(JToken value)
        {
            if (value.Type == JTokenType.Object)
            {
                float r = value["r"]?.ToObject<float>() ?? 1f;
                float g = value["g"]?.ToObject<float>() ?? 1f;
                float b = value["b"]?.ToObject<float>() ?? 1f;
                float a = value["a"]?.ToObject<float>() ?? 1f;
                return new Color(r, g, b, a);
            }

            if (value.Type == JTokenType.Array)
            {
                var array = (JArray)value;
                return new Color(
                    array.Count > 0 ? array[0].ToObject<float>() : 1f,
                    array.Count > 1 ? array[1].ToObject<float>() : 1f,
                    array.Count > 2 ? array[2].ToObject<float>() : 1f,
                    array.Count > 3 ? array[3].ToObject<float>() : 1f);
            }

            return Color.white;
        }

        private object SelectObjects(SceneParameters args)
        {
            var selected = new List<UnityEngine.Object>();

            if (args.instanceIds != null)
            {
                foreach (int id in args.instanceIds)
                {
                    var obj = EditorUtility.InstanceIDToObject(id);
                    if (obj != null)
                    {
                        selected.Add(obj);
                    }
                }
            }

            if (args.objectNames != null)
            {
                foreach (string name in args.objectNames)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var go = GameObject.Find(name.Trim());
                    if (go != null)
                    {
                        selected.Add(go);
                    }
                }
            }

            Selection.objects = selected.ToArray();

            return CreateSuccessResponse(new
            {
                selectedCount = selected.Count
            }, $"Selected {selected.Count} object(s)");
        }

        private object SaveScene(SceneParameters args)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return CreateErrorResponse("No valid active scene to save.");
            }

            string path = !string.IsNullOrWhiteSpace(args.path) ? args.path.Trim() : scene.path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return CreateErrorResponse("Scene has no path yet; provide 'path' (relative to the Assets folder).");
            }

            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                path = "Assets/" + path.TrimStart('/', '\\');
            }

            bool saved = EditorSceneManager.SaveScene(scene, path);
            if (!saved)
            {
                return CreateErrorResponse($"Failed to save scene to '{path}'.");
            }

            return CreateSuccessResponse(new
            {
                scene = scene.name,
                path = path
            }, "Scene saved");
        }

        private object LoadScene(SceneParameters args)
        {
            string path = !string.IsNullOrWhiteSpace(args.scenePath) ? args.scenePath.Trim() : args.path.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                return CreateErrorResponse("scenePath is required.");
            }

            var active = EditorSceneManager.GetActiveScene();
            if (!args.additive && active.IsValid() && active.isDirty)
            {
                // Never discard unsaved work silently.
                return CreateErrorResponse(
                    $"Current scene '{active.name}' has unsaved changes. " +
                    "Call scene.save first, or pass additive=true.");
            }

            var mode = args.additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
            var opened = EditorSceneManager.OpenScene(path, mode);

            return CreateSuccessResponse(new
            {
                scene = opened.name,
                path = opened.path,
                additive = args.additive
            }, "Scene loaded");
        }

        private static void ApplyTransform(Transform target, SceneParameters args, bool relative)
        {
            if (TryToVector3(args.position, out var position))
            {
                target.position = relative ? target.position + position : position;
            }

            if (TryToVector3(args.rotation, out var rotation))
            {
                target.eulerAngles = relative ? target.eulerAngles + rotation : rotation;
            }

            if (TryToVector3(args.scale, out var scale))
            {
                target.localScale = relative ? Vector3.Scale(target.localScale, scale) : scale;
            }
        }

        /// <summary>
        /// Accepts {"x":..,"y":..,"z":..} (Node bridge) and [x,y,z] (older callers).
        /// </summary>
        private static bool TryToVector3(object raw, out Vector3 value)
        {
            value = Vector3.zero;
            if (raw == null)
            {
                return false;
            }

            if (raw is Vector3 vector)
            {
                value = vector;
                return true;
            }

            JToken token;
            try
            {
                token = raw as JToken ?? JToken.FromObject(raw);
            }
            catch (Exception)
            {
                return false;
            }

            if (token.Type == JTokenType.Array)
            {
                var array = (JArray)token;
                if (array.Count < 3)
                {
                    return false;
                }
                value = new Vector3(array[0].ToObject<float>(), array[1].ToObject<float>(), array[2].ToObject<float>());
                return true;
            }

            if (token.Type == JTokenType.Object)
            {
                value = new Vector3(
                    token["x"]?.ToObject<float>() ?? 0f,
                    token["y"]?.ToObject<float>() ?? 0f,
                    token["z"]?.ToObject<float>() ?? 0f);
                return true;
            }

            return false;
        }

        /// <summary>
        /// JToken overload used when coercing component property values.
        /// </summary>
        private static Vector3 ToVector3(JToken value, Vector3 fallback)
        {
            return TryToVector3((object)value, out var parsed) ? parsed : fallback;
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
