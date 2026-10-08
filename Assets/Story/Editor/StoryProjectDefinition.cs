using System;
using System.Collections.Generic;
using Emerge.Characters;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Story.Editor
{
    // Authoring data only. Keeping these types in Editor excludes them from game builds.
    [CreateAssetMenu(fileName = "StoryProject", menuName = "Emerge/剧情/剧情项目（编辑资料）")]
    public sealed class StoryProjectDefinition : ScriptableObject
    {
        public int schemaVersion = 1;
        public string title = "", revision = "", scope = "";
        public Texture2D layoutReference;
        public List<StorySourceRecord> sources = new List<StorySourceRecord>();
        public List<StoryCharacterRecord> characters = new List<StoryCharacterRecord>();
        public List<StorySceneRecord> scenes = new List<StorySceneRecord>();
        public List<StoryItemRecord> items = new List<StoryItemRecord>();
        public List<StoryNodeRecord> nodes = new List<StoryNodeRecord>();
        public List<StoryRuleRecord> rules = new List<StoryRuleRecord>();
        public List<StoryLoreRecord> lore = new List<StoryLoreRecord>();
        public List<StoryIssueRecord> issues = new List<StoryIssueRecord>();
    }

    [Serializable] public sealed class StorySourceRecord
    { public string id = "", fileName = "", sha256 = "", notes = ""; }

    [Serializable] public sealed class StoryCharacterRecord
    {
        public string id = "", name = "", role = "", faction = "", department = "", identity = "", hexagram = "", notes = "", sourceText = "", source = "";
        public CharacterDefinition asset;
    }

    [Serializable] public sealed class StorySceneRecord
    { public string id = "", name = "", sourceNumber = "", dayAvailability = "", mapGroup = "", description = "", notes = "", source = ""; }

    [Serializable] public sealed class StoryItemRecord
    {
        public string id = "", sourceNumber = "", name = "", function = "", description = "", programEffect = "", acquisition = "", artNotes = "", modified = "", notes = "", source = "";
        public bool placeholder;
        public PropDefinition asset;
    }

    [Serializable] public sealed class StoryNodeRecord
    {
        public string id = "", eventNumber = "", title = "", route = "", kind = "", summary = "", trigger = "", completion = "", interaction = "", staging = "", designNotes = "", source = "", sourceText = "";
        public int day = 1, order;
        public bool placeholder, optional;
        public List<string> sceneIds = new List<string>();
        public List<string> characterIds = new List<string>();
        public List<string> itemIds = new List<string>();
        public List<StoryDialogueRecord> lines = new List<StoryDialogueRecord>();
        public List<StoryBranchRecord> branches = new List<StoryBranchRecord>();
    }

    [Serializable] public sealed class StoryDialogueRecord
    { public string speakerId = "", speaker = "", text = "", notes = ""; }

    [Serializable] public sealed class StoryBranchRecord
    {
        public string id = "", label = "", condition = "", targetId = "", notes = "";
        public int hiddenValueDelta;
        public bool hiddenValueConfirmed;
        public List<string> grantItemIds = new List<string>();
        public List<string> consumeItemIds = new List<string>();
    }

    [Serializable] public sealed class StoryRuleRecord
    { public string id = "", title = "", text = "", source = "", notes = ""; }

    [Serializable] public sealed class StoryLoreRecord
    { public string id = "", title = "", text = "", source = ""; public bool secret; }

    [Serializable] public sealed class StoryIssueRecord
    { public string id = "", title = "", detail = "", source = "", status = "", resolution = ""; }
}
