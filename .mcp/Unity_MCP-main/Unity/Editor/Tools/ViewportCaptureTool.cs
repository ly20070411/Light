using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for capturing viewport screenshots from the Scene or Game view,
    /// giving AI assistants the ability to visually verify their work in Unity.
    /// </summary>
    public class ViewportCaptureTool : McpToolBase
    {
        public override string ToolName => "scene_capture";
        public override string Description => "Capture a screenshot of the Unity Scene or Game view for visual inspection";
        public override string Category => "scene";

        [Serializable]
        public class CaptureParameters
        {
            public string view = "game";       // "game" or "scene"
            public int width = 960;
            public int height = 540;
            public string cameraName = "";     // specific camera (empty = main camera)
            public bool includeUI = true;
            public bool includeGizmos = false;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<CaptureParameters>(parameters);
                args.width = Mathf.Clamp(args.width, 64, 3840);
                args.height = Mathf.Clamp(args.height, 64, 2160);

                string view = (args.view ?? "game").Trim().ToLowerInvariant();

                return view switch
                {
                    "game" => CaptureGameView(args),
                    "scene" => CaptureSceneView(args),
                    _ => CreateErrorResponse($"Unknown view '{view}'. Use 'game' or 'scene'.")
                };
            }
            catch (Exception e)
            {
                LogError($"Viewport capture failed: {e.Message}");
                return CreateErrorResponse($"Viewport capture failed: {e.Message}", e.StackTrace);
            }
        }

        private object CaptureGameView(CaptureParameters args)
        {
            Camera camera = FindCamera(args.cameraName);
            if (camera == null)
            {
                return CreateErrorResponse(
                    string.IsNullOrEmpty(args.cameraName)
                        ? "No Main Camera found in scene. Add a Camera tagged 'MainCamera' or specify a camera name."
                        : $"Camera '{args.cameraName}' not found in scene.");
            }

            var renderTexture = new RenderTexture(args.width, args.height, 24, RenderTextureFormat.ARGB32);
            var previousRT = camera.targetTexture;

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                camera.targetTexture = previousRT;

                RenderTexture.active = renderTexture;
                var screenshot = new Texture2D(args.width, args.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, args.width, args.height), 0, 0);
                screenshot.Apply();
                RenderTexture.active = null;

                byte[] pngData = screenshot.EncodeToPNG();
                string base64 = Convert.ToBase64String(pngData);

                UnityEngine.Object.DestroyImmediate(screenshot);
                UnityEngine.Object.DestroyImmediate(renderTexture);

                LogMessage($"Captured Game view ({args.width}x{args.height}) from camera '{camera.name}'");

                return CreateSuccessResponse(new
                {
                    imageBase64 = base64,
                    mimeType = "image/png",
                    width = args.width,
                    height = args.height,
                    view = "game",
                    camera = camera.name,
                    cameraPosition = new { x = camera.transform.position.x, y = camera.transform.position.y, z = camera.transform.position.z },
                    cameraRotation = new { x = camera.transform.eulerAngles.x, y = camera.transform.eulerAngles.y, z = camera.transform.eulerAngles.z },
                    fieldOfView = camera.fieldOfView,
                    sizeBytes = pngData.Length
                }, "Game view captured successfully");
            }
            catch
            {
                camera.targetTexture = previousRT;
                RenderTexture.active = null;
                if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
                throw;
            }
        }

        private object CaptureSceneView(CaptureParameters args)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return CreateErrorResponse("No active Scene View found. Open a Scene View window in Unity.");
            }

            var sceneCamera = sceneView.camera;
            if (sceneCamera == null)
            {
                return CreateErrorResponse("Scene View camera is not available.");
            }

            var renderTexture = new RenderTexture(args.width, args.height, 24, RenderTextureFormat.ARGB32);
            var previousRT = sceneCamera.targetTexture;

            try
            {
                sceneCamera.targetTexture = renderTexture;

                if (args.includeGizmos)
                {
                    sceneView.Repaint();
                }

                sceneCamera.Render();
                sceneCamera.targetTexture = previousRT;

                RenderTexture.active = renderTexture;
                var screenshot = new Texture2D(args.width, args.height, TextureFormat.RGB24, false);
                screenshot.ReadPixels(new Rect(0, 0, args.width, args.height), 0, 0);
                screenshot.Apply();
                RenderTexture.active = null;

                byte[] pngData = screenshot.EncodeToPNG();
                string base64 = Convert.ToBase64String(pngData);

                UnityEngine.Object.DestroyImmediate(screenshot);
                UnityEngine.Object.DestroyImmediate(renderTexture);

                var pivot = sceneView.pivot;
                var rotation = sceneView.rotation.eulerAngles;
                var size = sceneView.size;
                var is2D = sceneView.in2DMode;

                LogMessage($"Captured Scene view ({args.width}x{args.height})");

                return CreateSuccessResponse(new
                {
                    imageBase64 = base64,
                    mimeType = "image/png",
                    width = args.width,
                    height = args.height,
                    view = "scene",
                    sceneViewInfo = new
                    {
                        pivot = new { x = pivot.x, y = pivot.y, z = pivot.z },
                        rotation = new { x = rotation.x, y = rotation.y, z = rotation.z },
                        size = size,
                        is2DMode = is2D,
                        orthographic = sceneView.orthographic
                    },
                    sizeBytes = pngData.Length
                }, "Scene view captured successfully");
            }
            catch
            {
                sceneCamera.targetTexture = previousRT;
                RenderTexture.active = null;
                if (renderTexture != null) UnityEngine.Object.DestroyImmediate(renderTexture);
                throw;
            }
        }

        private Camera FindCamera(string cameraName)
        {
            if (!string.IsNullOrEmpty(cameraName))
            {
                var go = GameObject.Find(cameraName);
                if (go != null)
                {
                    return go.GetComponent<Camera>();
                }
                return null;
            }
            return Camera.main;
        }
    }
}
