using System;
using System.Collections.Generic;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Day1
{
    [Serializable]
    public sealed class Day1QuizQuestion
    {
        [TextArea(2, 5)] public string prompt;
        public string correctLabel;
        public string reviewLabel;
        [TextArea(2, 5)] public string correctFeedback;
        [TextArea(2, 5)] public string reviewFeedback;
    }

    [Serializable]
    public sealed class Day1NpcScript
    {
        public string roleId;
        public List<PropDialogueLine> offer = new List<PropDialogueLine>();
        public List<PropDialogueLine> accepted = new List<PropDialogueLine>();
        public List<PropDialogueLine> ongoing = new List<PropDialogueLine>();
        public List<PropDialogueLine> handover = new List<PropDialogueLine>();
        public List<PropDialogueLine> completed = new List<PropDialogueLine>();
    }

    [CreateAssetMenu(fileName = "Day1Story", menuName = "Emerge/剧情/Day1 文案")]
    public sealed class Day1StoryDefinition : ScriptableObject
    {
        [TextArea(4, 14)] public string planningNotes;
        public List<Day1QuizQuestion> quiz = new List<Day1QuizQuestion>();
        public List<PropDialogueLine> rules = new List<PropDialogueLine>();
        public List<PropDialogueLine> environment = new List<PropDialogueLine>();
        public List<PropDialogueLine> supplies = new List<PropDialogueLine>();
        public List<PropDialogueLine> shadowAfter = new List<PropDialogueLine>();
        public List<PropDialogueLine> seaGate = new List<PropDialogueLine>();
        public List<PropDialogueLine> tools = new List<PropDialogueLine>();
        public List<PropDialogueLine> powerBefore = new List<PropDialogueLine>();
        public List<PropDialogueLine> powerAfter = new List<PropDialogueLine>();
        public List<PropDialogueLine> closing = new List<PropDialogueLine>();
        public List<Day1NpcScript> npcs = new List<Day1NpcScript>();
    }
}
