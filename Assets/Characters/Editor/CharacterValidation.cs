using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Props;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Characters.Editor
{
    [InitializeOnLoad]
    public static class CharacterValidation
    {
        [Serializable] private sealed class Check { public string name, observed; public bool passed; }
        [Serializable] private sealed class Report { public bool success; public string completedUtc; public List<Check> checks = new List<Check>(); }
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class CommandResult { public string id, state, message; public bool success; }
        private static double nextPoll;

        static CharacterValidation() { EditorApplication.update += Poll; }
        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            const string path = "Validation/characters-command.json";
            if (!File.Exists(path)) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
            if (command == null || string.IsNullOrWhiteSpace(command.id) || SessionState.GetString("Light.Characters.LastCommand", "") == command.id) return;
            SessionState.SetString("Light.Characters.LastCommand", command.id);
            var result = new CommandResult { id = command.id, state = "complete" };
            try
            {
                if (command.verb == "install-and-validate") InstallAndValidate();
                else if (command.verb == "validate") Run();
                else if (command.verb == "open-editor") CharacterEditorWindow.Open();
                else throw new InvalidOperationException("未知角色命令：" + command.verb);
                result.success = true; result.message = "角色操作完成。";
            }
            catch (Exception exception) { result.success = false; result.state = "failed"; result.message = exception.ToString(); Debug.LogException(exception); }
            File.WriteAllText("Validation/characters-command-result.json", JsonUtility.ToJson(result, true));
        }

        public static void InstallAndValidate()
        {
            CharacterAssetFactory.Install();
            var catalog = CharacterCatalog.LoadDefault();
            string before = JsonUtility.ToJson(catalog);
            var definitions = catalog.Characters.Select(JsonUtility.ToJson).ToArray();
            CharacterAssetFactory.Install();
            if (before != JsonUtility.ToJson(catalog) || !definitions.SequenceEqual(catalog.Characters.Select(JsonUtility.ToJson)))
                throw new InvalidOperationException("重复安装改变了已有角色数据。");
            Run();
        }

        [MenuItem("Tools/角色系统/验证角色资源")]
        public static void Run()
        {
            var report = new Report();
            var catalog = CharacterCatalog.LoadDefault();
            Add(report, "Runtime Resources catalog loads all eight roles", catalog != null && catalog.Characters.Count == 8, "7 current-team members and Tan Yue");
            if (catalog != null)
            {
                var characters = catalog.Characters.Where(c => c != null).ToArray();
                Add(report, "Stable IDs are unique and searchable", characters.Length == 8 && characters.All(c => !string.IsNullOrWhiteSpace(c.Id) && catalog.Find(c.Id) == c) && characters.Select(c => c.Id).Distinct().Count() == 8, "Lookup uses fixed IDs");
                Add(report, "Current and previous teams remain separate", characters.Count(c => c.team == CharacterTeam.CurrentSurvey) == 7 && characters.Count(c => c.team == CharacterTeam.PreviousSurvey) == 1 && catalog.Find(CharacterIds.TanYue)?.team == CharacterTeam.PreviousSurvey, "Tan Yue is outside the seven-person party");
                Add(report, "Four unnamed members retain profession labels", characters.Count(c => c.IsNamePending) == 4 && characters.All(c => !string.IsNullOrWhiteSpace(c.DisplayName)), "No invented names");
                Add(report, "Narrative roles and gender match the supplied roster", characters.Count(c => c.narrativeRole == CharacterRole.Player) == 1 && characters.Count(c => c.narrativeRole == CharacterRole.Main) == 1 && characters.Count(c => c.narrativeRole == CharacterRole.Supporting) == 2 && characters.Count(c => c.narrativeRole == CharacterRole.Npc) == 4 && catalog.Find(CharacterIds.TanYue)?.gender == CharacterGender.Female, "Player / main / supporting / NPC");
                var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(PropAssetFactory.LibraryPath);
                Add(report, "All role definitions are available in the map brush", library != null && characters.All(c => c.mapProp != null && c.mapProp.character == c && c.mapProp.DisplayName == c.DisplayName && library.Props.Contains(c.mapProp)), "Existing prop library integration");
                Add(report, "Sprites and portraits import as pixel sprites", characters.All(c => ValidSprite(c.mapSprite) && ValidSprite(c.portrait)), "Sprite import, Point filtering, no mipmaps");
                Add(report, "Planning fields contain the supplied data", characters.All(c => !string.IsNullOrWhiteSpace(c.faction) && !string.IsNullOrWhiteSpace(c.hexagram) && !string.IsNullOrWhiteSpace(c.abilityDescription) && !string.IsNullOrWhiteSpace(c.dayOneLocation) && !string.IsNullOrWhiteSpace(c.dayOneTask)), "Identity / power / Day1");

                var preview = EditorSceneManager.NewPreviewScene();
                try
                {
                    foreach (var character in characters)
                    {
                        var instance = character.prefab != null ? PrefabUtility.InstantiatePrefab(character.prefab, preview) as GameObject : null;
                        var prop = instance != null ? instance.GetComponent<PropInstance>() : null;
                        if (prop != null) prop.ApplyDefinition();
                        var sprite = instance != null ? instance.GetComponentInChildren<SpriteRenderer>() : null;
                        Add(report, "Prefab loads and renders " + character.Id, prop != null && prop.Character == character && sprite != null && sprite.sprite == character.mapSprite && prop.SolidCollider != null && prop.SolidCollider.enabled, character.DisplayName);
                    }
                    var actorObject = new GameObject("Temporary dialogue integration actor");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(actorObject, preview);
                    var actor = actorObject.AddComponent<PlayerInteractor>();
                    var owner = preview.GetRootGameObjects().Select(go => go.GetComponent<PropInstance>()).First(p => p != null);
                    var role = catalog.Find(CharacterIds.LinXi);
                    var line = new PropDialogueLine { character = role, text = "角色引用验证" };
                    bool started = actor.BeginDialogue(owner, new List<PropDialogueLine> { line }, _ => { });
                    Add(report, "Real dialogue resolves a referenced name and portrait", started && actor.CurrentDialogueSpeaker == role.DisplayName && line.Portrait == role.portrait, "PlayerInteractor uses role references");
                    actor.CancelDialogue();
                    line.speaker = "无名之人"; line.portrait = catalog.Find(CharacterIds.TanYue).portrait;
                    started = actor.BeginDialogue(owner, new List<PropDialogueLine> { line }, _ => { });
                    Add(report, "Dialogue overrides support concealed identities", started && actor.CurrentDialogueSpeaker == "无名之人" && line.Portrait == line.portrait, "Explicit speaker and portrait take priority");
                    actor.CancelDialogue();
                    var legacy = new PropDialogueLine { speaker = "终端", text = "旧对话" };
                    started = actor.BeginDialogue(owner, new List<PropDialogueLine> { legacy }, _ => { });
                    Add(report, "Existing string-based dialogue remains usable", started && actor.CurrentDialogueSpeaker == "终端" && legacy.Portrait == null, "No character reference required");
                    actor.CancelDialogue();
                    string originalId = role.Id, originalName = role.characterName;
                    try
                    {
                        role.characterName = "临时改名验证";
                        Add(report, "Renaming updates references without changing story keys", catalog.Find(originalId) == role && role.mapProp.DisplayName == role.characterName && new PropDialogueLine { character = role }.SpeakerName == role.characterName, "Runtime name resolution");
                    }
                    finally { role.characterName = originalName; }
                    var first = role.Spawn(Vector3.zero); var second = role.Spawn(Vector3.one);
                    try
                    {
                        var a = first.GetComponent<PropInstance>(); var b = second.GetComponent<PropInstance>();
                        Add(report, "Spawning the same role gives independent save identities", a.Character == role && b.Character == role && !string.IsNullOrWhiteSpace(a.InstanceId) && a.InstanceId != b.InstanceId, "Same character ID, distinct prop instance IDs");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
                }
                finally { EditorSceneManager.ClosePreviewScene(preview); }
            }
            report.success = report.checks.All(c => c.passed); report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/characters-results.json", JsonUtility.ToJson(report, true));
            if (!report.success) throw new InvalidOperationException("角色资源验证失败，查看 Validation/characters-results.json。");
            Debug.Log("CHARACTERS_VALIDATION_OK: " + report.checks.Count + " checks.");
        }

        private static bool ValidSprite(Sprite sprite)
        {
            if (sprite == null) return false;
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
            return importer != null && importer.textureType == TextureImporterType.Sprite && importer.filterMode == FilterMode.Point && !importer.mipmapEnabled;
        }
        private static void Add(Report report, string name, bool passed, string observed) => report.checks.Add(new Check { name = name, passed = passed, observed = observed });
    }
}
