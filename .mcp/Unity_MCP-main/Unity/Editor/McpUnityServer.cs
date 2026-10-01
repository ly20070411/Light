using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System.IO;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Main MCP server for Unity Editor that handles HTTP communication
    /// and coordinates tool execution between AI IDEs and Unity.
    /// </summary>
    [InitializeOnLoad]
    public class McpUnityServer : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unity MCP/Server Window";
        private const string PREF_SERVER_PORT = "UnityMCP_ServerPort";
        private const string PREF_AUTO_START = "UnityMCP_AutoStart";
        private const string PREF_REQUEST_TIMEOUT = "UnityMCP_RequestTimeout";
        
        private static McpUnityServer instance;
        private static HttpListener httpListener;
        private static bool isServerRunning = false;
        private static int serverPort = 8090;
        private static int requestTimeout = 10;
        private static bool autoStart = false;
        private static Thread listenerThread;
        
        private Vector2 scrollPosition;
        private string logText = "";
        private readonly List<string> logs = new List<string>();
        private readonly Dictionary<string, McpToolBase> tools = new Dictionary<string, McpToolBase>();
        
        static McpUnityServer()
        {
            LoadPreferences();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            if (autoStart)
            {
                EditorApplication.delayCall += () => StartServer();
            }
        }

        [MenuItem(MENU_PATH, false, 1)]
        public static void OpenWindow()
        {
            instance = GetWindow<McpUnityServer>("Unity MCP Server");
            instance.minSize = new Vector2(400, 300);
            instance.Show();
        }

        private void OnEnable()
        {
            instance = this;
            LoadPreferences();
            InitializeTools();
            RefreshUI();
        }

        private void OnDisable()
        {
            SavePreferences();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            
            // Header
            EditorGUILayout.LabelField("Unity MCP Server", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Server Configuration
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            
            int newPort = EditorGUILayout.IntField("HTTP Port", serverPort);
            if (newPort != serverPort)
            {
                serverPort = newPort;
                SavePreferences();
            }
            
            int newTimeout = EditorGUILayout.IntField("Request Timeout (seconds)", requestTimeout);
            if (newTimeout != requestTimeout)
            {
                requestTimeout = newTimeout;
                SavePreferences();
            }
            
            bool newAutoStart = EditorGUILayout.Toggle("Auto Start Server", autoStart);
            if (newAutoStart != autoStart)
            {
                autoStart = newAutoStart;
                SavePreferences();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Server Status
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            
            string status = isServerRunning ? "Running" : "Stopped";
            Color statusColor = isServerRunning ? Color.green : Color.red;
            
            GUI.color = statusColor;
            EditorGUILayout.LabelField($"Status: {status}");
            GUI.color = Color.white;
            
            if (isServerRunning)
            {
                EditorGUILayout.LabelField($"HTTP URL: http://localhost:{serverPort}");
                EditorGUILayout.LabelField($"Ready for MCP connections");
            }
            
            EditorGUILayout.Space();
            
            // Server Controls
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !isServerRunning;
            if (GUILayout.Button("Start Server"))
            {
                StartServer();
            }
            
            GUI.enabled = isServerRunning;
            if (GUILayout.Button("Stop Server"))
            {
                StopServer();
            }
            
            GUI.enabled = true;
            if (GUILayout.Button("Restart Server"))
            {
                StopServer();
                EditorApplication.delayCall += () => StartServer();
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Available Tools
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Available Tools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Registered Tools: {tools.Count}");
            
            if (tools.Count > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
                foreach (var tool in tools.Values)
                {
                    EditorGUILayout.LabelField($"• {tool.ToolName} - {tool.Description}");
                }
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Configuration Export
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("MCP Configuration", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Copy MCP Server Configuration to Clipboard"))
            {
                CopyMcpConfigToClipboard();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Logs
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Server Logs", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
            EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Logs"))
            {
                ClearLogs();
            }
            if (GUILayout.Button("Export Logs"))
            {
                ExportLogs();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        public static void StartServer()
        {
            if (isServerRunning)
            {
                LogMessage("Server is already running.");
                return;
            }

            try
            {
                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://localhost:{serverPort}/");
                httpListener.Start();
                
                isServerRunning = true;
                LogMessage($"MCP Server started on port {serverPort}");
                
                // Start listening thread
                listenerThread = new Thread(HandleRequests);
                listenerThread.Start();
                
                // Set environment variable for Node.js server
                Environment.SetEnvironmentVariable("UNITY_PORT", serverPort.ToString());
                Environment.SetEnvironmentVariable("UNITY_REQUEST_TIMEOUT", requestTimeout.ToString());
                
                RefreshUI();
            }
            catch (Exception e)
            {
                LogError($"Failed to start server: {e.Message}");
            }
        }

        public static void StopServer()
        {
            if (!isServerRunning)
            {
                LogMessage("Server is not running.");
                return;
            }

            try
            {
                isServerRunning = false;
                httpListener?.Stop();
                listenerThread?.Join(1000);
                httpListener = null;
                
                LogMessage("MCP Server stopped");
                RefreshUI();
            }
            catch (Exception e)
            {
                LogError($"Error stopping server: {e.Message}");
            }
        }

        private static void HandleRequests()
        {
            while (isServerRunning && httpListener != null)
            {
                try
                {
                    var context = httpListener.GetContext();
                    ProcessRequest(context);
                }
                catch (HttpListenerException)
                {
                    // Expected when stopping the server
                    break;
                }
                catch (Exception e)
                {
                    LogError($"Error handling request: {e.Message}");
                }
            }
        }

        private static void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    requestBody = reader.ReadToEnd();
                }

                var request = JsonConvert.DeserializeObject<McpRequest>(requestBody);
                var response = ProcessMcpRequest(request);
                var responseJson = JsonConvert.SerializeObject(response);

                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.ContentType = "application/json";
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            catch (Exception e)
            {
                LogError($"Error processing request: {e.Message}");
                
                var errorResponse = McpResponse.CreateError(e.Message);
                var errorJson = JsonConvert.SerializeObject(errorResponse);
                byte[] errorBytes = Encoding.UTF8.GetBytes(errorJson);
                
                context.Response.StatusCode = 500;
                context.Response.ContentLength64 = errorBytes.Length;
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
            }
        }

        private void InitializeTools()
        {
            tools.Clear();
            
            // Register all available tools
            RegisterTool(new ProjectAnalyzerTool());
            RegisterTool(new SceneManipulationTool());
            RegisterTool(new AssetManagerTool());
            RegisterTool(new CodeGenerationTool());
            RegisterTool(new BuildManagerTool());
            RegisterTool(new ViewportCaptureTool());
            RegisterTool(new PlayModeTool());
            RegisterTool(new ProjectMemoryTool());
            RegisterTool(new LabTool());

            LogMessage($"Initialized {tools.Count} MCP tools");
        }

        private void RegisterTool(McpToolBase tool)
        {
            if (tool != null && !string.IsNullOrEmpty(tool.ToolName))
            {
                tools[tool.ToolName] = tool;
            }
        }

        public static McpResponse ExecuteTool(string toolName, object parameters)
        {
            if (!instance.tools.ContainsKey(toolName))
            {
                return McpResponse.CreateError($"Tool '{toolName}' not found");
            }

            try
            {
                var tool = instance.tools[toolName];
                McpResponse result = null;
                Exception resultException = null;
                bool isComplete = false;
                AsyncToolResult asyncOp = null;

                // Execute tool on main thread
                EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var execResult = tool.Execute(parameters);

                        if (execResult is AsyncToolResult async)
                        {
                            asyncOp = async;
                        }
                        else
                        {
                            result = McpResponse.CreateSuccess(execResult);
                            LogMessage($"Executed tool: {toolName}");
                            isComplete = true;
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"Error executing tool '{toolName}': {e.Message}");
                        resultException = e;
                        isComplete = true;
                    }
                };

                // Wait for completion (with timeout)
                var startTime = DateTime.Now;
                var defaultTimeout = TimeSpan.FromSeconds(requestTimeout);

                while (!isComplete && DateTime.Now - startTime < defaultTimeout)
                {
                    // Check if async operation was started
                    if (asyncOp != null)
                    {
                        // Extend timeout to accommodate the async operation
                        var asyncTimeout = TimeSpan.FromSeconds(asyncOp.TimeoutSeconds > 0
                            ? asyncOp.TimeoutSeconds
                            : requestTimeout);
                        var asyncStart = DateTime.Now;

                        while (!asyncOp.IsComplete && DateTime.Now - asyncStart < asyncTimeout)
                        {
                            Thread.Sleep(10);
                        }

                        if (asyncOp.IsComplete)
                        {
                            result = asyncOp.Error != null
                                ? McpResponse.CreateError(asyncOp.Error)
                                : McpResponse.CreateSuccess(asyncOp.Result);
                            LogMessage($"Executed async tool: {toolName}");
                        }
                        else
                        {
                            result = McpResponse.CreateError(
                                $"Async tool '{toolName}' timed out after {asyncTimeout.TotalSeconds}s");
                        }

                        isComplete = true;
                        break;
                    }

                    Thread.Sleep(10);
                }

                if (!isComplete)
                {
                    return McpResponse.CreateError($"Tool '{toolName}' execution timed out after {requestTimeout} seconds");
                }

                if (resultException != null)
                {
                    return McpResponse.CreateError(resultException.Message);
                }

                return result;
            }
            catch (Exception e)
            {
                LogError($"Error executing tool '{toolName}': {e.Message}");
                return McpResponse.CreateError(e.Message);
            }
        }

        private static McpResponse ProcessMcpRequest(McpRequest request)
        {
            if (request?.Method == null)
            {
                return McpResponse.CreateError("Invalid request format");
            }

            // Parse method to extract tool name
            var methodParts = request.Method.Split('.');
            if (methodParts.Length < 2)
            {
                return McpResponse.CreateError($"Invalid method format: {request.Method}");
            }

            string toolCategory = methodParts[0];
            string toolAction = methodParts[1];
            string exactToolName = $"{toolCategory}_{toolAction}";

            if (instance == null)
            {
                return McpResponse.CreateError("MCP Server window is not open. Open it via Tools > Unity MCP > Server Window.");
            }

            // Try exact match first (e.g., "scene_capture")
            if (instance.tools.ContainsKey(exactToolName))
            {
                return ExecuteTool(exactToolName, request.Params);
            }

            // Fall back to category-level tool and inject action into params.
            // This routes "playmode.enter" → tool "playmode" with action="enter",
            // and "scene.createGameObject" → tool "scene_manipulate" with action injected.
            foreach (var kvp in instance.tools)
            {
                if (kvp.Value.Category == toolCategory)
                {
                    // Inject the action into the params so the tool can dispatch
                    var paramsWithAction = InjectAction(request.Params, toolAction);
                    return ExecuteTool(kvp.Key, paramsWithAction);
                }
            }

            return McpResponse.CreateError($"No tool found for method: {request.Method}");
        }

        private static object InjectAction(object parameters, string action)
        {
            try
            {
                // Convert params to a mutable dictionary and add the action
                string json = parameters != null
                    ? JsonConvert.SerializeObject(parameters)
                    : "{}";
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                           ?? new Dictionary<string, object>();
                if (!dict.ContainsKey("action"))
                {
                    dict["action"] = action;
                }
                return dict;
            }
            catch
            {
                return parameters;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Handle Unity play mode changes
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                LogMessage("Unity entering Play Mode");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                LogMessage("Unity returned to Edit Mode");
            }
        }

        private static void LoadPreferences()
        {
            serverPort = EditorPrefs.GetInt(PREF_SERVER_PORT, 8090);
            autoStart = EditorPrefs.GetBool(PREF_AUTO_START, false);
            requestTimeout = EditorPrefs.GetInt(PREF_REQUEST_TIMEOUT, 10);
        }

        private static void SavePreferences()
        {
            EditorPrefs.SetInt(PREF_SERVER_PORT, serverPort);
            EditorPrefs.SetBool(PREF_AUTO_START, autoStart);
            EditorPrefs.SetInt(PREF_REQUEST_TIMEOUT, requestTimeout);
        }

        private void CopyMcpConfigToClipboard()
        {
            var config = new
            {
                mcpServers = new
                {
                    unityMcp = new
                    {
                        command = "node",
                        args = new[] { "/absolute/path/to/UnityMCP/Server/build/index.js" },
                        env = new
                        {
                            UNITY_PORT = serverPort.ToString(),
                            REQUEST_TIMEOUT = requestTimeout.ToString()
                        }
                    }
                }
            };

            string jsonConfig = JsonConvert.SerializeObject(config, Formatting.Indented);
            EditorGUIUtility.systemCopyBuffer = jsonConfig;
            
            LogMessage("MCP server configuration copied to clipboard");
            ShowNotification(new GUIContent("Configuration copied to clipboard!"));
        }

        public static void LogMessage(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            if (instance != null)
            {
                instance.logs.Add(logEntry);
                instance.logText = string.Join("\n", instance.logs);
                
                if (instance.logs.Count > 100) // Keep last 100 logs
                {
                    instance.logs.RemoveAt(0);
                }
            }
            
            Debug.Log($"[Unity MCP] {message}");
        }

        public static void LogError(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}";
            
            if (instance != null)
            {
                instance.logs.Add(logEntry);
                instance.logText = string.Join("\n", instance.logs);
            }
            
            Debug.LogError($"[Unity MCP] {message}");
        }

        private void ClearLogs()
        {
            logs.Clear();
            logText = "";
        }

        private void ExportLogs()
        {
            string path = EditorUtility.SaveFilePanel("Export Logs", "", "unity_mcp_logs.txt", "txt");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, logText);
                LogMessage($"Logs exported to: {path}");
            }
        }

        private static void RefreshUI()
        {
            if (instance != null)
            {
                instance.Repaint();
            }
        }
    }

    /// <summary>
    /// Returned by tools that need multiple frames to complete (e.g., runtime observation).
    /// ExecuteTool polls IsComplete on the background thread while EditorApplication.update
    /// drives progress on the main thread.
    /// </summary>
    public class AsyncToolResult
    {
        public volatile bool IsComplete;
        public object Result;
        public string Error;
        public float TimeoutSeconds;
    }

    /// <summary>
    /// MCP request structure
    /// </summary>
    [Serializable]
    public class McpRequest
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; }
        [Newtonsoft.Json.JsonProperty("type")]
        public string Type { get; set; }
        [Newtonsoft.Json.JsonProperty("method")]
        public string Method { get; set; }
        [Newtonsoft.Json.JsonProperty("params")]
        public object Params { get; set; }
    }

    /// <summary>
    /// MCP response structure
    /// </summary>
    [Serializable]
    public class McpResponse
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; }
        [Newtonsoft.Json.JsonProperty("type")]
        public string Type { get; set; } = "response";
        [Newtonsoft.Json.JsonProperty("result")]
        public object Result { get; set; }
        [Newtonsoft.Json.JsonProperty("error")]
        public McpErrorInfo Error { get; set; }

        public static McpResponse CreateSuccess(object result)
        {
            return new McpResponse { Result = result };
        }

        public static McpResponse CreateError(string message)
        {
            return new McpResponse 
            { 
                Error = new McpErrorInfo { Message = message } 
            };
        }
    }

    /// <summary>
    /// MCP error structure
    /// </summary>
    [Serializable]
    public class McpErrorInfo
    {
        [Newtonsoft.Json.JsonProperty("code")]
        public int Code { get; set; }
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; set; }
        [Newtonsoft.Json.JsonProperty("data")]
        public object Data { get; set; }
    }
} 