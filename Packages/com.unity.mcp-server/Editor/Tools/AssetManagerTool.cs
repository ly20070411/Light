using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for asset management operations
    /// </summary>
    public class AssetManagerTool : McpToolBase
    {
        public override string ToolName => "asset_manage";
        public override string Description => "Manage project assets and resources";
        public override string Category => "asset";

        [Serializable]
        public class AssetParameters
        {
            public string action = "list_assets";
            public string folder = "Assets";
            public string filter = "";
            public string type = "";
            public string assetPath = "";
            public string destinationPath = "";
            public bool includeSubfolders = true;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<AssetParameters>(parameters);
                var action = (args.action ?? "list_assets").Trim().ToLowerInvariant();

                return action switch
                {
                    "list_assets" => ListAssets(args),
                    "find_assets" => FindAssets(args),
                    "create_folder" => CreateFolder(args),
                    "move_asset" => MoveAsset(args),
                    "delete_asset" => DeleteAsset(args),
                    _ => CreateErrorResponse($"Unknown action '{args.action}'. Supported actions: list_assets, find_assets, create_folder, move_asset, delete_asset")
                };
            }
            catch (Exception e)
            {
                LogError($"Asset management failed: {e.Message}");
                return CreateErrorResponse($"Asset management failed: {e.Message}", e.StackTrace);
            }
        }

        private object ListAssets(AssetParameters args)
        {
            var folder = NormalizeFolder(args.folder);
            if (folder == null)
            {
                return CreateErrorResponse("Folder must be inside the Assets folder.");
            }

            if (!AssetDatabase.IsValidFolder(folder))
            {
                return CreateErrorResponse($"Folder does not exist: {folder}");
            }

            var assetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                .Where(path => args.includeSubfolders || Path.GetDirectoryName(path) == folder)
                .ToArray();

            var assets = new List<object>();
            foreach (var path in assetPaths)
            {
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                assets.Add(new
                {
                    path,
                    type = assetType?.Name ?? "Unknown"
                });
            }

            return CreateSuccessResponse(new
            {
                folder,
                count = assets.Count,
                assets
            }, "Assets listed");
        }

        private object FindAssets(AssetParameters args)
        {
            var filter = string.IsNullOrWhiteSpace(args.filter) ? "t:Object" : args.filter;
            var searchFolders = NormalizeFolder(args.folder) is string folder ? new[] { folder } : null;

            var guids = AssetDatabase.FindAssets(filter, searchFolders);
            var assets = new List<object>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                assets.Add(new
                {
                    path,
                    type = assetType?.Name ?? "Unknown"
                });
            }

            return CreateSuccessResponse(new
            {
                filter,
                count = assets.Count,
                assets
            }, "Assets found");
        }

        private object CreateFolder(AssetParameters args)
        {
            var folder = NormalizeFolder(args.folder);
            if (folder == null || string.Equals(folder, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return CreateErrorResponse("Provide a folder path inside Assets to create.");
            }

            if (AssetDatabase.IsValidFolder(folder))
            {
                return CreateErrorResponse($"Folder already exists: {folder}");
            }

            var parent = Path.GetDirectoryName(folder)?.Replace("\\", "/") ?? "Assets";
            var name = Path.GetFileName(folder);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                return CreateErrorResponse($"Invalid folder path: {folder}");
            }

            var guid = AssetDatabase.CreateFolder(parent, name);
            var createdPath = AssetDatabase.GUIDToAssetPath(guid);

            return CreateSuccessResponse(new
            {
                path = createdPath
            }, "Folder created");
        }

        private object MoveAsset(AssetParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.assetPath) || string.IsNullOrWhiteSpace(args.destinationPath))
            {
                return CreateErrorResponse("assetPath and destinationPath are required to move an asset.");
            }

            var source = NormalizeAssetPath(args.assetPath);
            var destination = NormalizeAssetPath(args.destinationPath);
            if (source == null || destination == null)
            {
                return CreateErrorResponse("assetPath and destinationPath must be inside the Assets folder.");
            }

            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
            {
                return CreateErrorResponse($"Failed to move asset: {error}");
            }

            return CreateSuccessResponse(new
            {
                from = source,
                to = destination
            }, "Asset moved");
        }

        private object DeleteAsset(AssetParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.assetPath))
            {
                return CreateErrorResponse("assetPath is required to delete an asset.");
            }

            var assetPath = NormalizeAssetPath(args.assetPath);
            if (assetPath == null)
            {
                return CreateErrorResponse("assetPath must be inside the Assets folder.");
            }

            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                return CreateErrorResponse($"Failed to delete asset at {assetPath}");
            }

            return CreateSuccessResponse(new
            {
                path = assetPath
            }, "Asset deleted");
        }

        private string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return "Assets";
            }

            var normalized = folder.Replace("\\", "/").TrimEnd('/');
            return normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? normalized : null;
        }

        private string NormalizeAssetPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var normalized = assetPath.Replace("\\", "/");
            return normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? normalized : null;
        }
    }
}
