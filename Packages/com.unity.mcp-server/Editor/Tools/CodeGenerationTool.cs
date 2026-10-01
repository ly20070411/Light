using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for code generation operations
    /// </summary>
    public class CodeGenerationTool : McpToolBase
    {
        public override string ToolName => "code_generate";
        public override string Description => "Generate and manage C# scripts";
        public override string Category => "code";

        [Serializable]
        public class CodeGenerationParameters
        {
            public string action = "create_script";
            public string scriptName = "NewScript";
            public string folderPath = "Assets";
            public string @namespace = "";
            public string baseClass = "MonoBehaviour";
            public string template = "";
            public bool overwrite = false;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<CodeGenerationParameters>(parameters);
                var action = (args.action ?? "create_script").Trim().ToLowerInvariant();

                return action switch
                {
                    "create_script" => CreateScript(args),
                    "list_templates" => ListTemplates(),
                    _ => CreateErrorResponse($"Unknown action '{args.action}'. Supported actions: create_script, list_templates")
                };
            }
            catch (Exception e)
            {
                LogError($"Code generation failed: {e.Message}");
                return CreateErrorResponse($"Code generation failed: {e.Message}", e.StackTrace);
            }
        }

        private object CreateScript(CodeGenerationParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.scriptName))
            {
                return CreateErrorResponse("Script name is required.");
            }

            var safeFolder = string.IsNullOrWhiteSpace(args.folderPath) ? "Assets" : args.folderPath.Trim();
            if (!safeFolder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return CreateErrorResponse("Folder path must be inside the Assets folder.");
            }

            if (!AssetDatabase.IsValidFolder(safeFolder))
            {
                var parent = "Assets";
                var child = safeFolder.Replace("Assets/", "");
                if (string.IsNullOrWhiteSpace(child))
                {
                    return CreateErrorResponse($"Folder does not exist: {safeFolder}");
                }

                var parts = child.Split('/');
                foreach (var part in parts)
                {
                    var next = $"{parent}/{part}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(parent, part);
                    }
                    parent = next;
                }
            }

            var fileName = $"{args.scriptName}.cs";
            var targetPath = $"{safeFolder}/{fileName}";
            var scriptPath = targetPath;
            var exists = File.Exists(targetPath);

            if (exists && !args.overwrite)
            {
                return CreateErrorResponse($"A script named '{args.scriptName}' already exists in {safeFolder}. Set overwrite to true to replace.");
            }

            if (!exists && !args.overwrite)
            {
                scriptPath = AssetDatabase.GenerateUniqueAssetPath(targetPath);
            }

            var contents = string.IsNullOrWhiteSpace(args.template)
                ? BuildDefaultTemplate(args.scriptName, args.@namespace, args.baseClass)
                : args.template.Replace("{SCRIPT_NAME}", args.scriptName)
                    .Replace("{NAMESPACE}", args.@namespace ?? "")
                    .Replace("{BASE_CLASS}", args.baseClass ?? "MonoBehaviour");

            System.IO.File.WriteAllText(scriptPath, contents);
            AssetDatabase.ImportAsset(scriptPath);
            LogMessage($"Created script at {scriptPath}");

            return CreateSuccessResponse(new
            {
                path = scriptPath,
                name = args.scriptName,
                folder = safeFolder
            }, "Script created successfully");
        }

        private object ListTemplates()
        {
            return CreateSuccessResponse(new
            {
                templates = new[]
                {
                    new
                    {
                        name = "Default MonoBehaviour",
                        description = "Basic MonoBehaviour template",
                        placeholders = new[] { "{SCRIPT_NAME}", "{NAMESPACE}", "{BASE_CLASS}" }
                    }
                }
            }, "Available templates listed");
        }

        private string BuildDefaultTemplate(string scriptName, string scriptNamespace, string baseClass)
        {
            var namespaceLine = string.IsNullOrWhiteSpace(scriptNamespace)
                ? ""
                : $"namespace {scriptNamespace}\n{{\n";

            var namespaceClose = string.IsNullOrWhiteSpace(scriptNamespace) ? "" : "\n}";
            var indent = string.IsNullOrWhiteSpace(scriptNamespace) ? "" : "    ";
            var parentClass = string.IsNullOrWhiteSpace(baseClass) ? "MonoBehaviour" : baseClass;

            return
$@"using UnityEngine;

{namespaceLine}{indent}public class {scriptName} : {parentClass}
{indent}{{
{indent}    private void Start()
{indent}    {{
{indent}    }}

{indent}    private void Update()
{indent}    {{
{indent}    }}
{indent}}}{namespaceClose}
";
        }
    }
}
