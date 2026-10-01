using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for analyzing Unity project structure, settings, and configuration
    /// </summary>
    public class ProjectAnalyzerTool : McpToolBase
    {
        public override string ToolName => "project_analyze";
        public override string Description => "Analyze Unity project structure and return comprehensive information";
        public override string Category => "project";

        [Serializable]
        public class AnalyzeParameters
        {
            public bool includeAssets = false;
            public bool includePackages = true;
            public bool includeScenes = true;
            public bool includeSettings = true;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<AnalyzeParameters>(parameters);
                LogMessage("Starting project analysis...");

                var projectInfo = new
                {
                    project = GetProjectInfo(),
                    settings = args.includeSettings ? GetProjectSettings() : null,
                    scenes = args.includeScenes ? GetSceneInfo() : null,
                    packages = args.includePackages ? GetPackageInfo() : null,
                    assets = args.includeAssets ? GetAssetInfo() : null,
                    buildSettings = GetBuildSettings(),
                    performance = GetPerformanceMetrics()
                };

                LogMessage("Project analysis completed successfully");
                return CreateSuccessResponse(projectInfo, "Project analysis completed");
            }
            catch (Exception e)
            {
                LogError($"Project analysis failed: {e.Message}");
                return CreateErrorResponse($"Project analysis failed: {e.Message}", e.StackTrace);
            }
        }

        private object GetProjectInfo()
        {
            return new
            {
                projectName = Application.productName,
                projectPath = Application.dataPath.Replace("/Assets", ""),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                companyName = Application.companyName,
                version = Application.version,
                identifier = Application.identifier,
                buildGUID = Application.buildGUID,
                cloudProjectId = Application.cloudProjectId,
                isEditor = Application.isEditor,
                systemLanguage = Application.systemLanguage.ToString()
            };
        }

        private object GetProjectSettings()
        {
            var selectedBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(selectedBuildTarget));

            return new
            {
                playerSettings = new
                {
                    companyName = PlayerSettings.companyName,
                    productName = PlayerSettings.productName,
                    applicationIdentifier = PlayerSettings.applicationIdentifier,
                    bundleVersion = PlayerSettings.bundleVersion,
                    defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                    defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                    colorSpace = PlayerSettings.colorSpace.ToString(),
                    apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(namedBuildTarget).ToString(),
                    scriptingBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget).ToString()
                },
                qualitySettings = new
                {
                    activeColorSpace = QualitySettings.activeColorSpace.ToString(),
                    desiredColorSpace = QualitySettings.desiredColorSpace.ToString(),
                    maxQueuedFrames = QualitySettings.maxQueuedFrames,
                    pixelLightCount = QualitySettings.pixelLightCount,
                    shadowCascades = QualitySettings.shadowCascades,
                    shadowDistance = QualitySettings.shadowDistance,
                    vSyncCount = QualitySettings.vSyncCount
                },
                physicsSettings = new
                {
                    gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                    defaultMaterial = GetDefaultPhysicsMaterial(),
                    bounceThreshold = Physics.bounceThreshold,
                    sleepThreshold = Physics.sleepThreshold,
                    defaultContactOffset = Physics.defaultContactOffset,
                    defaultSolverIterations = Physics.defaultSolverIterations,
                    defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations
                }
            };
        }

        private string GetDefaultPhysicsMaterial()
        {
            try
            {
                // Physics.defaultMaterial was removed in newer Unity versions
                // Try to get it via reflection for backwards compatibility
                var physicsType = typeof(Physics);
                var defaultMaterialProperty = physicsType.GetProperty("defaultMaterial");
                if (defaultMaterialProperty != null)
                {
                    var material = defaultMaterialProperty.GetValue(null) as PhysicMaterial;
                    return material?.name ?? "None";
                }
                return "Not Available";
            }
            catch
            {
                return "Not Available";
            }
        }

        private object GetSceneInfo()
        {
            var scenes = new List<object>();
            
            // Get scenes in build settings
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var scenePath = EditorBuildSettings.scenes[i];
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.path);
                
                scenes.Add(new
                {
                    name = sceneAsset?.name ?? "Unknown",
                    path = scenePath.path,
                    enabled = scenePath.enabled,
                    buildIndex = i,
                    isLoaded = IsSceneLoaded(scenePath.path),
                    isDirty = IsSceneDirty(scenePath.path)
                });
            }

            return new
            {
                totalScenes = scenes.Count,
                activeScene = EditorSceneManager.GetActiveScene().name,
                loadedScenes = EditorSceneManager.loadedSceneCount,
                scenes = scenes
            };
        }

        private object GetPackageInfo()
        {
            var packages = new List<object>();
            
            try
            {
                var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
                if (File.Exists(manifestPath))
                {
                    var manifestContent = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<PackageManifest>(manifestContent);
                    
                    if (manifest?.dependencies != null)
                    {
                        foreach (var dependency in manifest.dependencies)
                        {
                            packages.Add(new
                            {
                                name = dependency.Key,
                                version = dependency.Value,
                                isBuiltIn = dependency.Value.StartsWith("file:") || dependency.Key.StartsWith("com.unity.")
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"Failed to read package manifest: {e.Message}");
            }

            return new
            {
                totalPackages = packages.Count,
                packages = packages.OrderBy(p => ((dynamic)p).name).ToList()
            };
        }

        private object GetAssetInfo()
        {
            var assetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/"))
                .ToArray();

            var assetsByType = new Dictionary<string, int>();
            var totalSize = 0L;

            foreach (var assetPath in assetPaths)
            {
                try
                {
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (assetType != null)
                    {
                        var typeName = assetType.Name;
                        if (!assetsByType.ContainsKey(typeName))
                            assetsByType[typeName] = 0;
                        assetsByType[typeName]++;
                    }

                    var fileInfo = new FileInfo(assetPath);
                    if (fileInfo.Exists)
                        totalSize += fileInfo.Length;
                }
                catch
                {
                    // Skip problematic assets
                }
            }

            return new
            {
                totalAssets = assetPaths.Length,
                totalSizeBytes = totalSize,
                totalSizeMB = Math.Round(totalSize / (1024.0 * 1024.0), 2),
                assetsByType = assetsByType.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private object GetBuildSettings()
        {
            return new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                development = EditorUserBuildSettings.development,
                connectProfiler = EditorUserBuildSettings.connectProfiler,
                buildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly,
                allowDebugging = EditorUserBuildSettings.allowDebugging,
                symlinkLibraries = EditorUserBuildSettings.symlinkLibraries,
                exportAsGoogleAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject
            };
        }

        private object GetPerformanceMetrics()
        {
            return new
            {
                systemInfo = new
                {
                    operatingSystem = SystemInfo.operatingSystem,
                    processorType = SystemInfo.processorType,
                    processorCount = SystemInfo.processorCount,
                    systemMemorySize = SystemInfo.systemMemorySize,
                    graphicsDeviceName = SystemInfo.graphicsDeviceName,
                    graphicsMemorySize = SystemInfo.graphicsMemorySize,
                    maxTextureSize = SystemInfo.maxTextureSize,
                    supportsComputeShaders = SystemInfo.supportsComputeShaders,
                    deviceModel = SystemInfo.deviceModel,
                    deviceName = SystemInfo.deviceName
                },
                editorMetrics = new
                {
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    timeSinceStartup = EditorApplication.timeSinceStartup
                }
            };
        }

        private bool IsSceneLoaded(string scenePath)
        {
            for (int i = 0; i < EditorSceneManager.loadedSceneCount; i++)
            {
                var loadedScene = EditorSceneManager.GetSceneAt(i);
                if (loadedScene.path == scenePath)
                    return true;
            }
            return false;
        }

        private bool IsSceneDirty(string scenePath)
        {
            for (int i = 0; i < EditorSceneManager.loadedSceneCount; i++)
            {
                var loadedScene = EditorSceneManager.GetSceneAt(i);
                if (loadedScene.path == scenePath)
                    return loadedScene.isDirty;
            }
            return false;
        }

        [Serializable]
        private class PackageManifest
        {
            public Dictionary<string, string> dependencies;
        }
    }
} 