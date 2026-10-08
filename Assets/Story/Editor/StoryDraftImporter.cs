using System;
using System.IO;
using System.Linq;
using Emerge.Characters;
using Emerge.Props;
using UnityEditor;
using UnityEngine;

namespace Emerge.Story.Editor
{
    /// <summary>Imports authoring data only. Never opens a scene or binds runtime flow.</summary>
    public static class StoryDraftImporter
    {
        public const string SourcePath = "Assets/Story/Source/InitialDraft.json";
        public const string ProjectPath = "Assets/Story/InitialDraft/InitialStoryProject.asset";
        public const string PropLibraryPath = "Assets/Story/InitialDraft/StoryPropLibrary.asset";
        private const string PropsPath = "Assets/Story/InitialDraft/Items";
        private const string CatalogPath = "Assets/Resources/Characters/CharacterCatalog.asset";

        [MenuItem("Tools/剧情/新版初稿/录入或补齐资料")]
        public static void InstallFromMenu()
        {
            var project = EnsureInstalled();
            Selection.activeObject = project;
            Debug.Log("新版剧情资料已录入。已有编辑内容会保留；运行时剧情与道具效果未接入。");
        }

        public static StoryProjectDefinition EnsureInstalled()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请停止 Play 模式后编辑剧情初稿。");
            var project = AssetDatabase.LoadAssetAtPath<StoryProjectDefinition>(ProjectPath);
            bool created = project == null;
            if (created)
            {
                if (!File.Exists(SourcePath)) throw new FileNotFoundException("缺少新版剧情初始稿。", SourcePath);
                EnsureFolder("Assets/Story/InitialDraft");
                project = ScriptableObject.CreateInstance<StoryProjectDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(SourcePath), project);
                    if (project.schemaVersion != 1 || project.nodes.Count == 0 || project.items.Count == 0)
                        throw new InvalidDataException("剧情初稿格式不完整。");
                    AssetDatabase.CreateAsset(project, ProjectPath);
                }
                catch { if (!EditorUtility.IsPersistent(project)) UnityEngine.Object.DestroyImmediate(project); throw; }
            }

            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            bool changed = created;
            foreach (var entry in project.characters)
            {
                if (entry.asset == null && catalog != null)
                {
                    entry.asset = catalog.Find(entry.id);
                    changed |= entry.asset != null;
                }
                if (created && entry.asset != null) ApplyNewCharacterMetadata(entry, project);
            }

            EnsureFolder(PropsPath);
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(PropLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PropLibrary>();
                AssetDatabase.CreateAsset(library, PropLibraryPath);
            }
            foreach (var entry in project.items)
            {
                if (entry.asset == null)
                {
                    string path = PropsPath + "/" + SafeName(entry.id) + ".asset";
                    var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
                    if (prop == null)
                    {
                        prop = ScriptableObject.CreateInstance<PropDefinition>();
                        prop.displayName = entry.name;
                        prop.category = entry.placeholder ? "剧情占位" : "剧情道具 / " + (entry.function ?? "").Replace('\n', ' ');
                        prop.description = entry.description;
                        prop.actions = PropActions.None;
                        prop.inventoryKey = "story." + entry.id;
                        prop.interactionLabel = "";
                        var serialized = new SerializedObject(prop);
                        serialized.FindProperty("id").stringValue = "story." + entry.id;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        AssetDatabase.CreateAsset(prop, path);
                    }
                    entry.asset = prop;
                    changed = true;
                }
                if (library.Add(entry.asset)) EditorUtility.SetDirty(library);
            }
            if (project.layoutReference == null)
            {
                project.layoutReference = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Story/Source/StationLayout.png");
                changed |= project.layoutReference != null;
            }
            if (changed) EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssetIfDirty(library);
            AssetDatabase.SaveAssetIfDirty(project);
            return project;
        }

        private static void ApplyNewCharacterMetadata(StoryCharacterRecord entry, StoryProjectDefinition project)
        {
            var character = entry.asset;
            Undo.RecordObject(character, "录入新版角色资料");
            character.characterName = entry.name;
            character.department = entry.department;
            // Identity keys, portraits, map props and prefabs remain stable.
            var task = project.nodes.FirstOrDefault(node => node.id.StartsWith("task-", StringComparison.Ordinal) && node.characterIds.Contains(entry.id));
            if (task != null)
            {
                character.dayOneLocation = string.Join(" / ", task.sceneIds.Select(id => project.scenes.FirstOrDefault(s => s.id == id)?.name ?? id));
                character.dayOneActivity = task.summary;
                character.dayOneTask = task.interaction + "\n" + task.completion;
            }
            if (entry.id == CharacterIds.ContainmentResearcher || entry.id == CharacterIds.Hydrologist ||
                entry.id == CharacterIds.Geologist || entry.id == CharacterIds.Mechanic)
                character.planningNotes = "根据新版细纲录入姓名与资料。原有角色 ID 和占位资产继续使用；新版任务仅为编辑资料，旧可玩场景未迁移。\n" + entry.notes;
            EditorUtility.SetDirty(character);
            AssetDatabase.SaveAssetIfDirty(character);
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                throw new InvalidDataException("道具资料 ID 仅允许字母、数字、连字符与下划线：" + value);
            return value;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
