using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Emerge.Story.Editor
{
    /// <summary>Local, narrow authoring commands. Does not accept code, scene paths or runtime actions.</summary>
    [InitializeOnLoad]
    public static class StoryAuthoringCommands
    {
        private const string CommandPath = "Validation/story-authoring-command.json";
        private const string ResultPath = "Validation/story-authoring-command-result.json";
        private const string SessionKey = "Light.Story.Authoring.LastCommand";
        private static double nextPoll;
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class Result
        {
            public string id, verb, message, completedUtc;
            public bool success;
        }

        static StoryAuthoringCommands() { EditorApplication.update += Update; }
        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(CommandPath)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(CommandPath)); }
            catch { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(SessionKey, "")) return;
            SessionState.SetString(SessionKey, command.id);
            var result = new Result { id = command.id, verb = command.verb, completedUtc = DateTime.UtcNow.ToString("o") };
            try
            {
                switch (command.verb)
                {
                    case "install": StoryDraftImporter.EnsureInstalled(); result.success = true; break;
                    case "open": StoryEditorWindow.Open(); result.success = true; break;
                    case "validate": result.success = StoryAuthoringValidation.ValidateAndWrite(); break;
                    default: throw new InvalidOperationException("仅支持 install、open、validate 剧情资料操作。");
                }
                result.message = result.success ? "剧情资料操作完成。" : "剧情资料校验未通过，请查看报告。";
            }
            catch (Exception exception) { result.success = false; result.message = exception.ToString(); Debug.LogException(exception); }
            Directory.CreateDirectory("Validation");
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
        }
    }
}
