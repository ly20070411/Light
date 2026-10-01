using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for controlling Unity Play Mode and inspecting runtime game state.
    /// Enables a complete AI development loop: code → enter play → capture viewport → read logs → fix → repeat.
    /// </summary>
    public class PlayModeTool : McpToolBase
    {
        public override string ToolName => "playmode";
        public override string Description => "Control Play Mode and inspect runtime game state";
        public override string Category => "playmode";

        private static readonly List<LogEntry> capturedLogs = new List<LogEntry>();
        private static int lastLogReadIndex = 0;
        private static bool isLogHandlerRegistered = false;

        [Serializable]
        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public string logType;
            public string timestamp;
        }

        [Serializable]
        private class ObservationTarget
        {
            public string gameObjectName;
            public int instanceId = 0;
            public string componentType;
            public string propertyName;
        }

        [Serializable]
        private class SimulateInputParams
        {
            public string key;
            public float holdDuration = -1f;
        }

        [Serializable]
        private class PlayModeParams
        {
            // Common
            public string action;

            // Enter
            public bool maximizeGameView = false;

            // Step
            public int frames = 1;

            // Inspect
            public string gameObjectName;
            public int instanceId = 0;
            public string[] componentFilter;
            public bool includeChildren = false;

            // SetProperty
            public string componentType;
            public string propertyName;
            public object value;

            // InvokeMethod
            public string methodName;
            public object[] arguments;

            // Console logs
            public string[] logTypes;
            public int limit = 50;
            public bool sinceLastCall = false;
            public string search;

            // Runtime info
            public bool includePerformance = true;
            public bool includePhysics = true;
            public bool includeTime = true;

            // TimeScale
            public float timeScale = 1f;

            // Observe
            public ObservationTarget[] targets;
            public float duration = 2f;
            public int sampleRate = 10;
            public SimulateInputParams simulateInput;

            // SimulateInput (standalone)
            public string key;
            // action is already defined above (reused)
            public float holdDuration = 0.1f;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<PlayModeParams>(parameters);

                EnsureLogHandlerRegistered();

                return args.action switch
                {
                    "getState" => GetPlayModeState(),
                    "enter" => EnterPlayMode(args),
                    "exit" => ExitPlayMode(),
                    "pause" => PausePlayMode(),
                    "resume" => ResumePlayMode(),
                    "step" => StepFrame(args),
                    "inspectGameObject" => InspectGameObject(args),
                    "setProperty" => SetProperty(args),
                    "invokeMethod" => InvokeMethod(args),
                    "getConsoleLogs" => GetConsoleLogs(args),
                    "getRuntimeInfo" => GetRuntimeInfo(args),
                    "setTimeScale" => SetTimeScale(args),
                    "observe" => ObserveRuntime(args),
                    "simulateInput" => SimulateInput(args),
                    _ => CreateErrorResponse($"Unknown playmode action: {args.action}")
                };
            }
            catch (Exception e)
            {
                LogError($"PlayMode tool error: {e.Message}");
                return CreateErrorResponse(e.Message, e.StackTrace);
            }
        }

        private void EnsureLogHandlerRegistered()
        {
            if (!isLogHandlerRegistered)
            {
                Application.logMessageReceived += OnLogMessageReceived;
                isLogHandlerRegistered = true;
            }
        }

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            capturedLogs.Add(new LogEntry
            {
                message = message,
                stackTrace = stackTrace,
                logType = type.ToString(),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            });

            // Cap at 1000 entries to prevent memory issues
            if (capturedLogs.Count > 1000)
            {
                capturedLogs.RemoveAt(0);
                if (lastLogReadIndex > 0) lastLogReadIndex--;
            }
        }

        // ─── Play Mode Control ───────────────────────────────────────────

        private object GetPlayModeState()
        {
            string state;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                state = "stopped";
            else if (EditorApplication.isPaused)
                state = "paused";
            else
                state = "playing";

            return CreateSuccessResponse(new
            {
                state,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                timeSinceStartup = EditorApplication.isPlaying ? Time.realtimeSinceStartup : 0f,
                frameCount = EditorApplication.isPlaying ? Time.frameCount : 0
            }, $"Play Mode state: {state}");
        }

        private object EnterPlayMode(PlayModeParams args)
        {
            if (EditorApplication.isPlaying)
            {
                return CreateSuccessResponse(new { state = "already_playing" },
                    "Already in Play Mode");
            }

            if (EditorApplication.isCompiling)
            {
                return CreateErrorResponse("Cannot enter Play Mode while scripts are compiling. Wait for compilation to finish.");
            }

            // Clear logs on fresh play
            capturedLogs.Clear();
            lastLogReadIndex = 0;

            EditorApplication.isPlaying = true;

            LogMessage("Entering Play Mode");

            return CreateSuccessResponse(new
            {
                state = "entering_playmode",
                hint = "Play Mode is starting. Wait a moment, then use scene.capture to see the running game and playmode.getConsoleLogs to check for errors."
            }, "Entering Play Mode — game is starting");
        }

        private object ExitPlayMode()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return CreateSuccessResponse(new { state = "already_stopped" },
                    "Already in Edit Mode");
            }

            EditorApplication.isPlaying = false;

            LogMessage("Exiting Play Mode");

            return CreateSuccessResponse(new
            {
                state = "exiting_playmode",
                logsCollected = capturedLogs.Count
            }, "Exiting Play Mode — returning to Edit Mode");
        }

        private object PausePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            if (EditorApplication.isPaused)
            {
                return CreateSuccessResponse(new { state = "already_paused" },
                    "Game is already paused");
            }

            EditorApplication.isPaused = true;
            LogMessage("Game paused");

            return CreateSuccessResponse(new
            {
                state = "paused",
                frameCount = Time.frameCount,
                gameTime = Time.time
            }, "Game paused");
        }

        private object ResumePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            if (!EditorApplication.isPaused)
            {
                return CreateSuccessResponse(new { state = "already_running" },
                    "Game is already running");
            }

            EditorApplication.isPaused = false;
            LogMessage("Game resumed");

            return CreateSuccessResponse(new { state = "playing" }, "Game resumed");
        }

        private object StepFrame(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            // Ensure paused before stepping
            if (!EditorApplication.isPaused)
            {
                EditorApplication.isPaused = true;
            }

            int steps = Mathf.Clamp(args.frames, 1, 100);
            for (int i = 0; i < steps; i++)
            {
                EditorApplication.Step();
            }

            LogMessage($"Stepped {steps} frame(s)");

            return CreateSuccessResponse(new
            {
                state = "paused",
                steppedFrames = steps,
                frameCount = Time.frameCount,
                gameTime = Time.time
            }, $"Advanced {steps} frame(s). Game is paused at frame {Time.frameCount}.");
        }

        // ─── Runtime Inspection ──────────────────────────────────────────

        private object InspectGameObject(PlayModeParams args)
        {
            GameObject go = FindGameObject(args.gameObjectName, args.instanceId);
            if (go == null)
            {
                return CreateErrorResponse(
                    $"GameObject not found: {(args.gameObjectName ?? $"ID:{args.instanceId}")}");
            }

            var result = BuildGameObjectInspection(go, args.componentFilter);

            if (args.includeChildren)
            {
                var children = new List<object>();
                foreach (Transform child in go.transform)
                {
                    children.Add(BuildGameObjectInspection(child.gameObject, args.componentFilter));
                }
                result["children"] = children;
            }

            return CreateSuccessResponse(result,
                $"Inspected '{go.name}' — {go.GetComponents<Component>().Length} components");
        }

        private Dictionary<string, object> BuildGameObjectInspection(GameObject go, string[] componentFilter)
        {
            var info = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["instanceId"] = go.GetEntityId(),
                ["active"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["tag"] = go.tag,
                ["layer"] = LayerMask.LayerToName(go.layer),
                ["isStatic"] = go.isStatic
            };

            var components = new List<Dictionary<string, object>>();
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;

                string typeName = component.GetType().Name;

                // Apply filter if specified
                if (componentFilter != null && componentFilter.Length > 0)
                {
                    if (!componentFilter.Any(f =>
                        typeName.Equals(f, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                var compData = new Dictionary<string, object>
                {
                    ["type"] = typeName,
                    ["fullType"] = component.GetType().FullName,
                    ["enabled"] = IsComponentEnabled(component)
                };

                // Extract serialized and public fields
                var properties = ExtractComponentProperties(component);
                if (properties.Count > 0)
                {
                    compData["properties"] = properties;
                }

                components.Add(compData);
            }

            info["components"] = components;
            return info;
        }

        private Dictionary<string, object> ExtractComponentProperties(Component component)
        {
            var props = new Dictionary<string, object>();
            var type = component.GetType();

            // Handle well-known Unity types with clean output
            if (component is Transform t)
            {
                props["position"] = Vec3(t.position);
                props["localPosition"] = Vec3(t.localPosition);
                props["rotation"] = Vec3(t.eulerAngles);
                props["localRotation"] = Vec3(t.localEulerAngles);
                props["localScale"] = Vec3(t.localScale);
                props["childCount"] = t.childCount;
                return props;
            }

            if (component is Rigidbody rb)
            {
                props["velocity"] = Vec3(rb.linearVelocity);
                props["angularVelocity"] = Vec3(rb.angularVelocity);
                props["mass"] = rb.mass;
                props["drag"] = rb.linearDamping;
                props["angularDrag"] = rb.angularDamping;
                props["useGravity"] = rb.useGravity;
                props["isKinematic"] = rb.isKinematic;
                props["isSleeping"] = rb.IsSleeping();
                return props;
            }

            if (component is Rigidbody2D rb2d)
            {
                props["velocity"] = Vec2(rb2d.linearVelocity);
                props["angularVelocity"] = rb2d.angularVelocity;
                props["mass"] = rb2d.mass;
                props["gravityScale"] = rb2d.gravityScale;
                props["bodyType"] = rb2d.bodyType.ToString();
                props["isSleeping"] = rb2d.IsSleeping();
                return props;
            }

            if (component is Camera cam)
            {
                props["fieldOfView"] = cam.fieldOfView;
                props["orthographic"] = cam.orthographic;
                props["orthographicSize"] = cam.orthographicSize;
                props["nearClipPlane"] = cam.nearClipPlane;
                props["farClipPlane"] = cam.farClipPlane;
                props["depth"] = cam.depth;
                return props;
            }

            if (component is Animator anim)
            {
                props["isPlaying"] = anim.isActiveAndEnabled;
                props["speed"] = anim.speed;
                if (anim.runtimeAnimatorController != null)
                {
                    props["controller"] = anim.runtimeAnimatorController.name;
                    var currentState = anim.GetCurrentAnimatorStateInfo(0);
                    props["currentStateLength"] = currentState.length;
                    props["currentStateNormalizedTime"] = currentState.normalizedTime;
                    props["isInTransition"] = anim.IsInTransition(0);
                }
                return props;
            }

            // For custom MonoBehaviour scripts: use reflection to read public fields and serialized fields
            if (component is MonoBehaviour)
            {
                // Public fields
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        props[field.Name] = SerializeValue(field.GetValue(component));
                    }
                    catch { /* skip unreadable fields */ }
                }

                // Serialized private fields
                foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.GetCustomAttribute<SerializeField>() != null)
                    {
                        try
                        {
                            props[field.Name] = SerializeValue(field.GetValue(component));
                        }
                        catch { /* skip */ }
                    }
                }

                // Public properties with getters (non-inherited to avoid noise)
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            props[prop.Name] = SerializeValue(prop.GetValue(component));
                        }
                        catch { /* skip */ }
                    }
                }

                return props;
            }

            // Generic fallback: get public fields only
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    props[field.Name] = SerializeValue(field.GetValue(component));
                }
                catch { /* skip */ }
            }

            return props;
        }

        private object SerializeValue(object value)
        {
            if (value == null) return null;

            var t = value.GetType();

            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal))
                return value;

            if (t.IsEnum)
                return value.ToString();

            if (value is Vector2 v2) return Vec2(v2);
            if (value is Vector3 v3) return Vec3(v3);
            if (value is Vector4 v4) return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (value is Quaternion q) return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is Color c) return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (value is Bounds b) return new { center = Vec3(b.center), size = Vec3(b.size) };
            if (value is Rect rc) return new { x = rc.x, y = rc.y, width = rc.width, height = rc.height };

            if (value is UnityEngine.Object uo)
                return uo != null ? $"{uo.GetType().Name}:{uo.name}" : null;

            if (value is System.Collections.IList list)
            {
                var items = new List<object>();
                int max = Math.Min(list.Count, 20); // cap array output
                for (int i = 0; i < max; i++)
                    items.Add(SerializeValue(list[i]));
                if (list.Count > 20)
                    items.Add($"... and {list.Count - 20} more");
                return items;
            }

            return value.ToString();
        }

        // ─── Runtime Mutation ────────────────────────────────────────────

        private object SetProperty(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Runtime property changes only work during Play Mode.");
            }

            GameObject go = FindGameObject(args.gameObjectName, args.instanceId);
            if (go == null)
            {
                return CreateErrorResponse($"GameObject not found: {args.gameObjectName ?? $"ID:{args.instanceId}"}");
            }

            Component component = FindComponent(go, args.componentType);
            if (component == null)
            {
                return CreateErrorResponse($"Component '{args.componentType}' not found on '{go.name}'");
            }

            var type = component.GetType();

            // Try field first, then property
            var field = type.GetField(args.propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                try
                {
                    object converted = ConvertValue(args.value, field.FieldType);
                    field.SetValue(component, converted);
                    LogMessage($"Set {go.name}.{args.componentType}.{args.propertyName} = {converted}");
                    return CreateSuccessResponse(new
                    {
                        gameObject = go.name,
                        component = args.componentType,
                        property = args.propertyName,
                        newValue = SerializeValue(field.GetValue(component))
                    }, $"Set {args.propertyName} successfully");
                }
                catch (Exception e)
                {
                    return CreateErrorResponse($"Failed to set field '{args.propertyName}': {e.Message}");
                }
            }

            var prop = type.GetProperty(args.propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (prop != null && prop.CanWrite)
            {
                try
                {
                    object converted = ConvertValue(args.value, prop.PropertyType);
                    prop.SetValue(component, converted);
                    LogMessage($"Set {go.name}.{args.componentType}.{args.propertyName} = {converted}");
                    return CreateSuccessResponse(new
                    {
                        gameObject = go.name,
                        component = args.componentType,
                        property = args.propertyName,
                        newValue = SerializeValue(prop.GetValue(component))
                    }, $"Set {args.propertyName} successfully");
                }
                catch (Exception e)
                {
                    return CreateErrorResponse($"Failed to set property '{args.propertyName}': {e.Message}");
                }
            }

            return CreateErrorResponse(
                $"Property/field '{args.propertyName}' not found or not writable on {args.componentType}");
        }

        private object InvokeMethod(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Method invocation only works during Play Mode.");
            }

            GameObject go = FindGameObject(args.gameObjectName, args.instanceId);
            if (go == null)
            {
                return CreateErrorResponse($"GameObject not found: {args.gameObjectName ?? $"ID:{args.instanceId}"}");
            }

            Component component = FindComponent(go, args.componentType);
            if (component == null)
            {
                return CreateErrorResponse($"Component '{args.componentType}' not found on '{go.name}'");
            }

            var type = component.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == args.methodName && !m.IsSpecialName)
                .ToArray();

            if (methods.Length == 0)
            {
                return CreateErrorResponse($"Public method '{args.methodName}' not found on {args.componentType}");
            }

            // Find best matching overload by parameter count
            var methodArgs = args.arguments ?? Array.Empty<object>();
            var method = methods.FirstOrDefault(m => m.GetParameters().Length == methodArgs.Length)
                         ?? methods[0];

            try
            {
                var parameters = method.GetParameters();
                var convertedArgs = new object[parameters.Length];
                for (int i = 0; i < parameters.Length && i < methodArgs.Length; i++)
                {
                    convertedArgs[i] = ConvertValue(methodArgs[i], parameters[i].ParameterType);
                }

                object result = method.Invoke(component, convertedArgs);

                LogMessage($"Invoked {go.name}.{args.componentType}.{args.methodName}()");

                return CreateSuccessResponse(new
                {
                    gameObject = go.name,
                    component = args.componentType,
                    method = args.methodName,
                    returnValue = result != null ? SerializeValue(result) : null,
                    returnType = method.ReturnType.Name
                }, $"Invoked {args.methodName}() successfully");
            }
            catch (TargetInvocationException tie)
            {
                return CreateErrorResponse(
                    $"Method '{args.methodName}' threw an exception: {tie.InnerException?.Message ?? tie.Message}",
                    tie.InnerException?.StackTrace);
            }
            catch (Exception e)
            {
                return CreateErrorResponse($"Failed to invoke '{args.methodName}': {e.Message}");
            }
        }

        // ─── Console Logs ────────────────────────────────────────────────

        private object GetConsoleLogs(PlayModeParams args)
        {
            var types = args.logTypes ?? new[] { "Log", "Warning", "Error", "Exception" };
            var typeSet = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);

            int startIndex = args.sinceLastCall ? lastLogReadIndex : 0;
            var filtered = capturedLogs
                .Skip(startIndex)
                .Where(l => typeSet.Contains(l.logType))
                .Where(l => string.IsNullOrEmpty(args.search) ||
                            l.message.IndexOf(args.search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            int total = filtered.Count;
            var limited = filtered.TakeLast(args.limit).ToList();

            lastLogReadIndex = capturedLogs.Count;

            // Build summary counts
            var counts = limited.GroupBy(l => l.logType)
                .ToDictionary(g => g.Key, g => g.Count());

            return CreateSuccessResponse(new
            {
                logs = limited,
                totalMatching = total,
                returned = limited.Count,
                counts,
                totalCaptured = capturedLogs.Count,
                sinceLastCall = args.sinceLastCall
            }, $"Retrieved {limited.Count} log entries ({counts.GetValueOrDefault("Error", 0)} errors, {counts.GetValueOrDefault("Warning", 0)} warnings)");
        }

        // ─── Runtime Info ────────────────────────────────────────────────

        private object GetRuntimeInfo(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Runtime info is only available during Play Mode.");
            }

            var info = new Dictionary<string, object>
            {
                ["state"] = EditorApplication.isPaused ? "paused" : "playing"
            };

            if (args.includePerformance)
            {
                info["performance"] = new
                {
                    fps = 1f / Time.unscaledDeltaTime,
                    frameTime = Time.unscaledDeltaTime * 1000f, // ms
                    smoothDeltaTime = Time.smoothDeltaTime * 1000f,
                    totalMemoryMB = (float)System.GC.GetTotalMemory(false) / (1024 * 1024),
                    activeGameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include).Length
                };
            }

            if (args.includePhysics)
            {
                info["physics"] = new
                {
                    gravity = Vec3(Physics.gravity),
                    simulationMode = Physics.simulationMode.ToString(),
                    rigidbodyCount = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include).Length,
                    rigidbody2DCount = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include).Length
                };
            }

            if (args.includeTime)
            {
                info["time"] = new
                {
                    time = Time.time,
                    unscaledTime = Time.unscaledTime,
                    deltaTime = Time.deltaTime,
                    timeScale = Time.timeScale,
                    frameCount = Time.frameCount,
                    realtimeSinceStartup = Time.realtimeSinceStartup,
                    fixedDeltaTime = Time.fixedDeltaTime
                };
            }

            return CreateSuccessResponse(info, "Runtime info retrieved");
        }

        // ─── Time Scale ──────────────────────────────────────────────────

        private object SetTimeScale(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Time scale can only be changed during Play Mode.");
            }

            float scale = Mathf.Clamp(args.timeScale, 0f, 100f);
            float previous = Time.timeScale;
            Time.timeScale = scale;

            LogMessage($"Time scale changed: {previous} → {scale}");

            return CreateSuccessResponse(new
            {
                previousTimeScale = previous,
                newTimeScale = scale
            }, $"Time scale set to {scale}x");
        }

        // ─── Runtime Observation ─────────────────────────────────────────

        private static ObservationSession activeObservation;

        private class ObservationSession
        {
            public List<ObservationTarget> targets;
            public float duration;
            public float sampleInterval;
            public float startTime;
            public float nextSampleTime;
            public float inputReleaseTime;
            public KeyCode simulatedKey;
            public bool inputActive;
            public List<Dictionary<string, object>> samples;
            public Dictionary<string, List<object>> valuesByTarget;
            public bool isComplete;
            public object result;
            public AsyncToolResult asyncResult;
            public PlayModeTool parentTool;
        }

        private object ObserveRuntime(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            if (activeObservation != null && !activeObservation.isComplete)
            {
                return CreateErrorResponse("An observation is already in progress. Wait for it to complete.");
            }

            if (args.targets == null || args.targets.Length == 0)
            {
                return CreateErrorResponse("No observation targets specified.");
            }

            float duration = Mathf.Clamp(args.duration, 0.1f, 8f);
            int sampleRate = Mathf.Clamp(args.sampleRate, 1, 30);

            // Validate targets exist before starting
            var validatedTargets = new List<ObservationTarget>();
            foreach (var target in args.targets)
            {
                GameObject go = FindGameObject(target.gameObjectName, target.instanceId);
                if (go == null)
                {
                    return CreateErrorResponse(
                        $"GameObject not found: {target.gameObjectName ?? $"ID:{target.instanceId}"}");
                }

                if (!string.IsNullOrEmpty(target.componentType))
                {
                    Component comp = FindComponent(go, target.componentType);
                    if (comp == null)
                    {
                        return CreateErrorResponse(
                            $"Component '{target.componentType}' not found on '{go.name}'");
                    }
                }

                validatedTargets.Add(target);
            }

            // Parse simulated input key
            KeyCode simKey = KeyCode.None;
            float inputHoldDuration = duration;
            if (args.simulateInput != null && !string.IsNullOrEmpty(args.simulateInput.key))
            {
                simKey = ParseKeyCode(args.simulateInput.key);
                if (simKey == KeyCode.None)
                {
                    return CreateErrorResponse($"Unknown key: '{args.simulateInput.key}'");
                }
                if (args.simulateInput.holdDuration >= 0)
                {
                    inputHoldDuration = Mathf.Min(args.simulateInput.holdDuration, duration);
                }
            }

            // Ensure game is running (not paused)
            if (EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }

            // Create async result that ExecuteTool will poll on the background thread
            var asyncResult = new AsyncToolResult
            {
                TimeoutSeconds = duration + 5f
            };

            activeObservation = new ObservationSession
            {
                targets = validatedTargets,
                duration = duration,
                sampleInterval = 1f / sampleRate,
                startTime = Time.realtimeSinceStartup,
                nextSampleTime = 0f,
                simulatedKey = simKey,
                inputActive = simKey != KeyCode.None,
                inputReleaseTime = Time.realtimeSinceStartup + inputHoldDuration,
                samples = new List<Dictionary<string, object>>(),
                valuesByTarget = new Dictionary<string, List<object>>(),
                isComplete = false,
                result = null,
                asyncResult = asyncResult,
                parentTool = this
            };

            foreach (var t in validatedTargets)
            {
                string key = TargetKey(t);
                activeObservation.valuesByTarget[key] = new List<object>();
            }

            // Start input simulation if requested
            if (activeObservation.inputActive)
            {
                SendKeyEvent(activeObservation.simulatedKey, true);
            }

            EditorApplication.update += ObservationTick;

            McpUnityServer.LogMessage($"[playmode] Started observation: {validatedTargets.Count} targets, {duration}s, {sampleRate} Hz");

            // Return AsyncToolResult — ExecuteTool will poll it on the background thread
            // while EditorApplication.update drives ObservationTick on the main thread
            return asyncResult;
        }

        private static void ObservationTick()
        {
            if (activeObservation == null || activeObservation.isComplete)
            {
                EditorApplication.update -= ObservationTick;
                return;
            }

            var obs = activeObservation;
            float elapsed = Time.realtimeSinceStartup - obs.startTime;

            // Release input if hold duration exceeded
            if (obs.inputActive && Time.realtimeSinceStartup >= obs.inputReleaseTime)
            {
                SendKeyEvent(obs.simulatedKey, false);
                obs.inputActive = false;
            }

            // Check if observation is complete
            if (elapsed >= obs.duration)
            {
                if (obs.inputActive)
                {
                    SendKeyEvent(obs.simulatedKey, false);
                    obs.inputActive = false;
                }

                EditorApplication.update -= ObservationTick;
                var compiledResult = obs.parentTool.CompileObservationResult(obs);
                obs.result = compiledResult;
                obs.isComplete = true;

                // Signal async completion to ExecuteTool's background thread
                if (obs.asyncResult != null)
                {
                    obs.asyncResult.Result = compiledResult;
                    obs.asyncResult.IsComplete = true;
                }
                return;
            }

            // Sample at the configured rate
            if (elapsed < obs.nextSampleTime) return;
            obs.nextSampleTime = elapsed + obs.sampleInterval;

            var sample = new Dictionary<string, object>
            {
                ["frame"] = Time.frameCount,
                ["time"] = Math.Round(Time.time, 4),
                ["elapsed"] = Math.Round(elapsed, 4)
            };

            var values = new Dictionary<string, object>();
            foreach (var target in obs.targets)
            {
                string key = TargetKey(target);
                object val = SampleProperty(target);
                values[key] = val;
                obs.valuesByTarget[key].Add(val);
            }

            sample["values"] = values;
            obs.samples.Add(sample);
        }

        private static object SampleProperty(ObservationTarget target)
        {
            GameObject go = null;
            if (target.instanceId != 0)
            {
#pragma warning disable CS0618
                go = EditorUtility.InstanceIDToObject(target.instanceId) as GameObject;
#pragma warning restore CS0618
            }
            if (go == null && !string.IsNullOrEmpty(target.gameObjectName))
                go = GameObject.Find(target.gameObjectName);
            if (go == null) return null;

            // If no component specified, sample transform position by default
            if (string.IsNullOrEmpty(target.componentType))
            {
                return SerializeValueStatic(go.transform.position);
            }

            Component comp = null;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c.GetType().Name.Equals(target.componentType, StringComparison.OrdinalIgnoreCase) ||
                    c.GetType().FullName.Equals(target.componentType, StringComparison.OrdinalIgnoreCase))
                {
                    comp = c;
                    break;
                }
            }
            if (comp == null) return null;

            // If no property specified, return all properties of the component
            if (string.IsNullOrEmpty(target.propertyName))
            {
                return SampleAllComponentProperties(comp);
            }

            return ReadSingleProperty(comp, target.propertyName);
        }

        private static object ReadSingleProperty(Component comp, string propertyName)
        {
            // Handle well-known types with direct accessors for performance
            if (comp is Transform t)
            {
                return propertyName.ToLower() switch
                {
                    "position" => Vec3Static(t.position),
                    "localposition" => Vec3Static(t.localPosition),
                    "rotation" or "eulerangles" => Vec3Static(t.eulerAngles),
                    "localrotation" or "localeulerangles" => Vec3Static(t.localEulerAngles),
                    "localscale" => Vec3Static(t.localScale),
                    "forward" => Vec3Static(t.forward),
                    "up" => Vec3Static(t.up),
                    "right" => Vec3Static(t.right),
                    _ => null
                };
            }

            if (comp is Rigidbody rb)
            {
                return propertyName.ToLower() switch
                {
                    "velocity" or "linearvelocity" => Vec3Static(rb.linearVelocity),
                    "angularvelocity" => Vec3Static(rb.angularVelocity),
                    "position" => Vec3Static(rb.position),
                    "mass" => rb.mass,
                    "drag" or "lineardamping" => rb.linearDamping,
                    "issleeping" => rb.IsSleeping(),
                    _ => null
                };
            }

            if (comp is Rigidbody2D rb2d)
            {
                return propertyName.ToLower() switch
                {
                    "velocity" or "linearvelocity" => Vec2Static(rb2d.linearVelocity),
                    "angularvelocity" => rb2d.angularVelocity,
                    "position" => Vec2Static(rb2d.position),
                    "mass" => rb2d.mass,
                    "gravityscale" => rb2d.gravityScale,
                    "issleeping" => rb2d.IsSleeping(),
                    _ => null
                };
            }

            if (comp is Animator anim)
            {
                return propertyName.ToLower() switch
                {
                    "speed" => anim.speed,
                    "normalizedtime" => anim.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    "isintransition" => anim.IsInTransition(0),
                    _ => null
                };
            }

            // Reflection fallback for custom MonoBehaviours and other components
            var type = comp.GetType();

            var field = type.GetField(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                try { return SerializeValueStatic(field.GetValue(comp)); }
                catch { return null; }
            }

            var prop = type.GetProperty(propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0)
            {
                try { return SerializeValueStatic(prop.GetValue(comp)); }
                catch { return null; }
            }

            return null;
        }

        private static Dictionary<string, object> SampleAllComponentProperties(Component comp)
        {
            var props = new Dictionary<string, object>();

            if (comp is Transform t)
            {
                props["position"] = Vec3Static(t.position);
                props["rotation"] = Vec3Static(t.eulerAngles);
                props["localScale"] = Vec3Static(t.localScale);
                return props;
            }

            if (comp is Rigidbody rb)
            {
                props["velocity"] = Vec3Static(rb.linearVelocity);
                props["angularVelocity"] = Vec3Static(rb.angularVelocity);
                props["position"] = Vec3Static(rb.position);
                return props;
            }

            if (comp is Rigidbody2D rb2d)
            {
                props["velocity"] = Vec2Static(rb2d.linearVelocity);
                props["position"] = Vec2Static(rb2d.position);
                return props;
            }

            // Reflection for custom scripts
            var type = comp.GetType();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try { props[field.Name] = SerializeValueStatic(field.GetValue(comp)); }
                catch { /* skip */ }
            }

            return props;
        }

        private object CompileObservationResult(ObservationSession obs)
        {
            var summary = new Dictionary<string, object>();

            foreach (var kvp in obs.valuesByTarget)
            {
                string targetKey = kvp.Key;
                var values = kvp.Value;
                if (values.Count == 0) continue;

                var targetSummary = new Dictionary<string, object>
                {
                    ["sampleCount"] = values.Count
                };

                // Try to compute numeric summary for Vector3 values
                if (values[0] is Dictionary<string, object> firstDict && firstDict.ContainsKey("x"))
                {
                    var vectors = new List<Vector3>();
                    foreach (var v in values)
                    {
                        if (v is Dictionary<string, object> dict)
                        {
                            float x = Convert.ToSingle(dict.GetValueOrDefault("x", 0f));
                            float y = Convert.ToSingle(dict.GetValueOrDefault("y", 0f));
                            float z = Convert.ToSingle(dict.GetValueOrDefault("z", 0f));
                            vectors.Add(new Vector3(x, y, z));
                        }
                    }

                    if (vectors.Count > 0)
                    {
                        var first = vectors[0];
                        var last = vectors[vectors.Count - 1];
                        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                        var sum = Vector3.zero;

                        foreach (var v in vectors)
                        {
                            min = Vector3.Min(min, v);
                            max = Vector3.Max(max, v);
                            sum += v;
                        }

                        var mean = sum / vectors.Count;
                        var delta = last - first;

                        targetSummary["first"] = Vec3(first);
                        targetSummary["last"] = Vec3(last);
                        targetSummary["min"] = Vec3(min);
                        targetSummary["max"] = Vec3(max);
                        targetSummary["mean"] = Vec3(mean);
                        targetSummary["delta"] = Vec3(delta);
                        targetSummary["totalDistance"] = delta.magnitude;
                    }
                }
                else if (values[0] is float || values[0] is double || values[0] is int || values[0] is long)
                {
                    // Scalar numeric summary
                    var nums = new List<float>();
                    foreach (var v in values)
                    {
                        try { nums.Add(Convert.ToSingle(v)); }
                        catch { /* skip */ }
                    }

                    if (nums.Count > 0)
                    {
                        targetSummary["first"] = nums[0];
                        targetSummary["last"] = nums[nums.Count - 1];
                        targetSummary["min"] = nums.Min();
                        targetSummary["max"] = nums.Max();
                        targetSummary["mean"] = nums.Average();
                        targetSummary["delta"] = nums[nums.Count - 1] - nums[0];
                    }
                }

                summary[targetKey] = targetSummary;
            }

            return CreateSuccessResponse(new
            {
                observations = obs.samples,
                summary,
                duration = obs.duration,
                totalSamples = obs.samples.Count,
                inputSimulated = obs.simulatedKey != KeyCode.None ? obs.simulatedKey.ToString() : null
            }, $"Observation complete: {obs.samples.Count} samples over {obs.duration}s for {obs.targets.Count} target(s)");
        }

        private static string TargetKey(ObservationTarget t)
        {
            string goName = t.gameObjectName ?? $"ID:{t.instanceId}";
            string comp = t.componentType ?? "Transform";
            string prop = t.propertyName ?? "*";
            return $"{goName}/{comp}.{prop}";
        }

        // ─── Input Simulation ────────────────────────────────────────────

        private object SimulateInput(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            if (string.IsNullOrEmpty(args.key))
            {
                return CreateErrorResponse("No key specified for input simulation.");
            }

            KeyCode keyCode = ParseKeyCode(args.key);
            if (keyCode == KeyCode.None)
            {
                return CreateErrorResponse($"Unknown key: '{args.key}'. Use Unity KeyCode names (e.g., 'w', 'space', 'left shift', 'mouse 0').");
            }

            string inputAction = args.action ?? "tap";

            switch (inputAction)
            {
                case "press":
                    SendKeyEvent(keyCode, true);
                    LogMessage($"Key pressed: {keyCode}");
                    return CreateSuccessResponse(new
                    {
                        key = keyCode.ToString(),
                        action = "press",
                        hint = "Key is held down. Use simulateInput with action='release' to release it."
                    }, $"Key {keyCode} pressed (held down)");

                case "release":
                    SendKeyEvent(keyCode, false);
                    LogMessage($"Key released: {keyCode}");
                    return CreateSuccessResponse(new
                    {
                        key = keyCode.ToString(),
                        action = "release"
                    }, $"Key {keyCode} released");

                case "tap":
                    float hold = Mathf.Clamp(args.holdDuration, 0.01f, 5f);
                    SendKeyEvent(keyCode, true);

                    // Schedule release after hold duration
                    float releaseTime = Time.realtimeSinceStartup + hold;
                    EditorApplication.CallbackFunction releaseCallback = null;
                    releaseCallback = () =>
                    {
                        if (Time.realtimeSinceStartup >= releaseTime)
                        {
                            SendKeyEvent(keyCode, false);
                            EditorApplication.update -= releaseCallback;
                        }
                    };
                    EditorApplication.update += releaseCallback;

                    // Wait for tap to complete
                    var waitStart = DateTime.Now;
                    var waitTimeout = TimeSpan.FromSeconds(hold + 1);
                    while (DateTime.Now - waitStart < waitTimeout)
                    {
                        System.Threading.Thread.Sleep(10);
                        if (Time.realtimeSinceStartup >= releaseTime + 0.05f) break;
                    }

                    LogMessage($"Key tapped: {keyCode} for {hold}s");
                    return CreateSuccessResponse(new
                    {
                        key = keyCode.ToString(),
                        action = "tap",
                        holdDuration = hold
                    }, $"Key {keyCode} tapped for {hold}s");

                default:
                    return CreateErrorResponse($"Unknown input action: '{inputAction}'. Use 'press', 'release', or 'tap'.");
            }
        }

        private static void SendKeyEvent(KeyCode keyCode, bool isDown)
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null)
                {
                    var key = KeyCodeToInputSystemKey(keyCode);
                    if (key != UnityEngine.InputSystem.Key.None)
                    {
                        // Modern Input System simulation disabled for broader version compatibility
                        // (InputState may not be available or signature differs across Input System package versions).
                        // Fall through to the robust legacy Event-based simulation below.
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to legacy approach
            }
#endif
            // Legacy fallback: send Event to Game view
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
                if (gameView != null)
                {
                    var evt = new Event
                    {
                        type = isDown ? EventType.KeyDown : EventType.KeyUp,
                        keyCode = keyCode
                    };
                    gameView.SendEvent(evt);
                }
            }
        }

        private static KeyCode ParseKeyCode(string key)
        {
            if (string.IsNullOrEmpty(key)) return KeyCode.None;

            // Handle common aliases
            string normalized = key.ToLower().Trim();
            switch (normalized)
            {
                case "space": return KeyCode.Space;
                case "enter": case "return": return KeyCode.Return;
                case "escape": case "esc": return KeyCode.Escape;
                case "tab": return KeyCode.Tab;
                case "backspace": return KeyCode.Backspace;
                case "delete": case "del": return KeyCode.Delete;
                case "left shift": case "lshift": return KeyCode.LeftShift;
                case "right shift": case "rshift": return KeyCode.RightShift;
                case "left ctrl": case "lctrl": case "left control": return KeyCode.LeftControl;
                case "right ctrl": case "rctrl": case "right control": return KeyCode.RightControl;
                case "left alt": case "lalt": return KeyCode.LeftAlt;
                case "right alt": case "ralt": return KeyCode.RightAlt;
                case "up": case "uparrow": return KeyCode.UpArrow;
                case "down": case "downarrow": return KeyCode.DownArrow;
                case "left": case "leftarrow": return KeyCode.LeftArrow;
                case "right": case "rightarrow": return KeyCode.RightArrow;
                case "mouse 0": case "mouse0": case "left click": return KeyCode.Mouse0;
                case "mouse 1": case "mouse1": case "right click": return KeyCode.Mouse1;
                case "mouse 2": case "mouse2": case "middle click": return KeyCode.Mouse2;
            }

            // Single character keys (a-z, 0-9)
            if (normalized.Length == 1)
            {
                char c = normalized[0];
                if (c >= 'a' && c <= 'z')
                    return (KeyCode)((int)KeyCode.A + (c - 'a'));
                if (c >= '0' && c <= '9')
                    return (KeyCode)((int)KeyCode.Alpha0 + (c - '0'));
            }

            // Function keys
            if (normalized.StartsWith("f") && int.TryParse(normalized.Substring(1), out int fNum) && fNum >= 1 && fNum <= 15)
            {
                return (KeyCode)((int)KeyCode.F1 + (fNum - 1));
            }

            // Try direct enum parse as last resort
            if (Enum.TryParse<KeyCode>(key, true, out var parsed))
                return parsed;

            return KeyCode.None;
        }

#if ENABLE_INPUT_SYSTEM
        private static UnityEngine.InputSystem.Key KeyCodeToInputSystemKey(KeyCode keyCode)
        {
            // Map common KeyCodes to InputSystem Key enum
            if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                return (UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.A + (keyCode - KeyCode.A));
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
                return (UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.Digit0 + (keyCode - KeyCode.Alpha0));

            return keyCode switch
            {
                KeyCode.Space => UnityEngine.InputSystem.Key.Space,
                KeyCode.Return => UnityEngine.InputSystem.Key.Enter,
                KeyCode.Escape => UnityEngine.InputSystem.Key.Escape,
                KeyCode.Tab => UnityEngine.InputSystem.Key.Tab,
                KeyCode.LeftShift => UnityEngine.InputSystem.Key.LeftShift,
                KeyCode.RightShift => UnityEngine.InputSystem.Key.RightShift,
                KeyCode.LeftControl => UnityEngine.InputSystem.Key.LeftCtrl,
                KeyCode.RightControl => UnityEngine.InputSystem.Key.RightCtrl,
                KeyCode.LeftAlt => UnityEngine.InputSystem.Key.LeftAlt,
                KeyCode.RightAlt => UnityEngine.InputSystem.Key.RightAlt,
                KeyCode.UpArrow => UnityEngine.InputSystem.Key.UpArrow,
                KeyCode.DownArrow => UnityEngine.InputSystem.Key.DownArrow,
                KeyCode.LeftArrow => UnityEngine.InputSystem.Key.LeftArrow,
                KeyCode.RightArrow => UnityEngine.InputSystem.Key.RightArrow,
                _ => UnityEngine.InputSystem.Key.None
            };
        }
#endif

        private static void CleanupObservation()
        {
            if (activeObservation != null && activeObservation.inputActive)
            {
                SendKeyEvent(activeObservation.simulatedKey, false);
            }
            activeObservation = null;
        }

        // Static versions of serialization helpers for use in static observation callbacks
        private static object SerializeValueStatic(object value)
        {
            if (value == null) return null;
            var t = value.GetType();
            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal)) return value;
            if (t.IsEnum) return value.ToString();
            if (value is Vector2 v2) return Vec2Static(v2);
            if (value is Vector3 v3) return Vec3Static(v3);
            if (value is Vector4 v4) return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (value is Quaternion q) return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is Color c) return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (value is Bounds b) return new { center = Vec3Static(b.center), size = Vec3Static(b.size) };
            if (value is UnityEngine.Object uo) return uo != null ? $"{uo.GetType().Name}:{uo.name}" : null;
            return value.ToString();
        }

        private static object Vec2Static(Vector2 v) => new Dictionary<string, object> { ["x"] = v.x, ["y"] = v.y };
        private static object Vec3Static(Vector3 v) => new Dictionary<string, object> { ["x"] = v.x, ["y"] = v.y, ["z"] = v.z };

        // ─── Helpers ─────────────────────────────────────────────────────

        private GameObject FindGameObject(string name, int instanceId)
        {
            if (instanceId != 0)
            {
#pragma warning disable CS0618
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618
            }

            if (!string.IsNullOrEmpty(name))
            {
                // First try exact path
                var go = GameObject.Find(name);
                if (go != null) return go;

                // Then search all objects including inactive
                foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (obj.name == name && obj.scene.isLoaded)
                        return obj;
                }
            }

            return null;
        }

        private Component FindComponent(GameObject go, string componentType)
        {
            if (string.IsNullOrEmpty(componentType)) return null;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name.Equals(componentType, StringComparison.OrdinalIgnoreCase) ||
                    comp.GetType().FullName.Equals(componentType, StringComparison.OrdinalIgnoreCase))
                    return comp;
            }

            return null;
        }

        private bool IsComponentEnabled(Component component)
        {
            if (component is Behaviour b) return b.enabled;
            if (component is Renderer r) return r.enabled;
            if (component is Collider c) return c.enabled;
            return true;
        }

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            // Handle JObject/JToken from JSON deserialization
            if (value is JObject jObj)
            {
                if (targetType == typeof(Vector2))
                    return new Vector2(jObj.Value<float>("x"), jObj.Value<float>("y"));
                if (targetType == typeof(Vector3))
                    return new Vector3(jObj.Value<float>("x"), jObj.Value<float>("y"), jObj.Value<float>("z"));
                if (targetType == typeof(Vector4))
                    return new Vector4(jObj.Value<float>("x"), jObj.Value<float>("y"), jObj.Value<float>("z"), jObj.Value<float>("w"));
                if (targetType == typeof(Color))
                    return new Color(jObj.Value<float>("r"), jObj.Value<float>("g"), jObj.Value<float>("b"), jObj.Value<float>("a"));
                if (targetType == typeof(Quaternion))
                    return new Quaternion(jObj.Value<float>("x"), jObj.Value<float>("y"), jObj.Value<float>("z"), jObj.Value<float>("w"));

                return JsonConvert.DeserializeObject(jObj.ToString(), targetType);
            }

            if (value is JToken jToken)
            {
                return jToken.ToObject(targetType);
            }

            if (targetType.IsEnum && value is string s)
                return Enum.Parse(targetType, s, true);

            return Convert.ChangeType(value, targetType);
        }

        private static object Vec2(Vector2 v) => new { x = v.x, y = v.y };
        private static object Vec3(Vector3 v) => new { x = v.x, y = v.y, z = v.z };
    }
}
