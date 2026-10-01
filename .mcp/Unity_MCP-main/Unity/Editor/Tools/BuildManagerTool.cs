using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for build management operations
    /// </summary>
    public class BuildManagerTool : McpToolBase
    {
        public override string ToolName => "build_manage";
        public override string Description => "Manage Unity builds and build settings";
        public override string Category => "build";

        [Serializable]
        public class BuildParameters
        {
            public string target = "StandaloneWindows64";
            public string buildPath = "";
            public bool developmentBuild = false;
            public bool scriptDebugging = false;
            public bool connectProfiler = false;
            public bool allowDebugging = false;
            public string[] scenes = null;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<BuildParameters>(parameters);
                
                // Parse build target
                if (!Enum.TryParse<BuildTarget>(args.target, out var buildTarget))
                {
                    return CreateErrorResponse($"Invalid build target: {args.target}");
                }

                // Validate build path
                if (string.IsNullOrEmpty(args.buildPath))
                {
                    args.buildPath = GetDefaultBuildPath(buildTarget);
                }

                // Ensure build directory exists
                var buildDir = Path.GetDirectoryName(args.buildPath);
                if (!Directory.Exists(buildDir))
                {
                    Directory.CreateDirectory(buildDir);
                }

                // Configure build options
                var buildOptions = BuildOptions.None;
                if (args.developmentBuild) buildOptions |= BuildOptions.Development;
                if (args.scriptDebugging) buildOptions |= BuildOptions.AllowDebugging;
                if (args.connectProfiler) buildOptions |= BuildOptions.ConnectWithProfiler;

                // Get scenes to build
                var scenesToBuild = args.scenes ?? GetScenesInBuildSettings();

                LogMessage($"Starting build for {buildTarget} at {args.buildPath}");

                // Perform the build
                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenesToBuild,
                    locationPathName = args.buildPath,
                    target = buildTarget,
                    options = buildOptions
                };

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                
                var buildResult = new
                {
                    success = report.summary.result == BuildResult.Succeeded,
                    result = report.summary.result.ToString(),
                    totalTime = report.summary.totalTime.TotalSeconds,
                    totalSize = report.summary.totalSize,
                    buildPath = args.buildPath,
                    platform = buildTarget.ToString(),
                    warnings = report.summary.totalWarnings,
                    errors = report.summary.totalErrors,
                    steps = GetBuildSteps(report)
                };

                if (report.summary.result == BuildResult.Succeeded)
                {
                    LogMessage($"Build completed successfully in {report.summary.totalTime.TotalSeconds:F2} seconds");
                    return CreateSuccessResponse(buildResult, "Build completed successfully");
                }
                else
                {
                    LogError($"Build failed: {report.summary.result}");
                    return CreateErrorResponse($"Build failed: {report.summary.result}", buildResult);
                }
            }
            catch (Exception e)
            {
                LogError($"Build operation failed: {e.Message}");
                return CreateErrorResponse($"Build operation failed: {e.Message}", e.StackTrace);
            }
        }

        private string GetDefaultBuildPath(BuildTarget target)
        {
            var projectName = Application.productName;
            var extension = GetBuildExtension(target);
            var platformFolder = target.ToString();
            
            return Path.Combine("Builds", platformFolder, $"{projectName}{extension}");
        }

        private string GetBuildExtension(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return ".exe";
                case BuildTarget.StandaloneOSX:
                    return ".app";
                case BuildTarget.StandaloneLinux64:
                    return "";
                case BuildTarget.Android:
                    return ".apk";
                case BuildTarget.iOS:
                    return "";
                case BuildTarget.WebGL:
                    return "";
                default:
                    return "";
            }
        }

        private string[] GetScenesInBuildSettings()
        {
            var scenes = new string[EditorBuildSettings.scenes.Length];
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                scenes[i] = EditorBuildSettings.scenes[i].path;
            }
            return scenes;
        }

        private object[] GetBuildSteps(BuildReport report)
        {
            var steps = new object[report.steps.Length];
            for (int i = 0; i < report.steps.Length; i++)
            {
                var step = report.steps[i];
                steps[i] = new
                {
                    name = step.name,
                    duration = step.duration.TotalSeconds,
                    messages = step.messages?.Length ?? 0
                };
            }
            return steps;
        }

        public override bool CanExecute()
        {
            return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
        }
    }
} 