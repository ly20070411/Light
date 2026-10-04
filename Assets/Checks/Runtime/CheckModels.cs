using System;
using UnityEngine;
using System.Collections.Generic;
using Emerge.Checks.Divination;

namespace Emerge.Checks
{
    public enum CheckBehavior
    {
        Parent = 0,
        Offspring = 1,
        Officer = 2,
        Wealth = 3,
        Sibling = 4,
        Self = 5
    }

    [Serializable]
    public sealed class ActorCheckAttributes
    {
        public int parent;
        public int offspring;
        public int officer;
        public int wealth;
        public int sibling;
        public int self;

        public int Get(CheckBehavior behavior)
        {
            switch (behavior)
            {
                case CheckBehavior.Parent: return parent;
                case CheckBehavior.Offspring: return offspring;
                case CheckBehavior.Officer: return officer;
                case CheckBehavior.Wealth: return wealth;
                case CheckBehavior.Sibling: return sibling;
                case CheckBehavior.Self: return self;
                default: throw new ArgumentOutOfRangeException(nameof(behavior), behavior, "Unknown check behavior.");
            }
        }

        public ActorCheckAttributes Clone()
        {
            return new ActorCheckAttributes
            {
                parent = parent,
                offspring = offspring,
                officer = officer,
                wealth = wealth,
                sibling = sibling,
                self = self
            };
        }
    }

    [Serializable]
    public sealed class CheckOptionDefinition
    {
        public string id;
        public string label;
        public CheckBehavior behavior;
        public int targetValue;
        public string[] requiredFlags = Array.Empty<string>();
        public string requiredItemKey;
        public int requiredItemAmount = 1;
        public bool consumeRequiredItem;
        public CheckOutcomeDefinition success;
        public CheckOutcomeDefinition failure;
    }

    [Serializable]
    public sealed class CheckOutcomeDefinition
    {
        public string text;
        public string continuation;
        public string[] grantedFlags = Array.Empty<string>();
        public string rewardItemKey;
        public string rewardItemName;
        public int rewardItemAmount;
        public int contaminationDelta;
    }

    public enum CheckSessionPhase
    {
        Ready = 0,
        Resolved = 1,
        Completed = 2,
        Preparing = 3
    }

    [Serializable]
    public sealed class CheckModifierPreparation
    {
        public string status;
        public int[] modifiers = new int[6];
    }

    [Serializable]
    public sealed class CheckSession
    {
        public string eventId;
        public string contextId;
        public string sessionId;
        public ActorCheckAttributes attributes;
        public int[] modifiers = new int[6];
        public string castingStatus;
        public string chartStatus;
        public string modifierStatus;
        public CheckSessionPhase phase = CheckSessionPhase.Ready;
        [SerializeReference] public CheckResult result;
        public bool outcomeApplied;
        [SerializeReference] public DivinationRecord divination;
        public List<CheckTraceEntry> logicTrace = new List<CheckTraceEntry>();
    }

    [Serializable]
    public sealed class CheckResult
    {
        public string optionId;
        public CheckBehavior behavior;
        public int baseValue;
        public int modifier;
        public int finalValue;
        public int targetValue;
        public long margin;
        public bool success;
    }
}
