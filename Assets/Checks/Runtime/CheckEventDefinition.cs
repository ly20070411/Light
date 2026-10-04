using System;
using System.Collections.Generic;
using UnityEngine;
using Emerge.Checks.Divination;

namespace Emerge.Checks
{
    [CreateAssetMenu(fileName = "CheckEvent", menuName = "Emerge/检定系统/检定事件")]
    public sealed class CheckEventDefinition : ScriptableObject
    {
        public string eventId;
        public string title;
        public string speaker;
        [TextArea(2, 6)] public string intro;
        [TextArea(2, 6)] public string completedText;
        public bool revealDifficultyBeforeChoice;
        public bool useDivination = true;
        public string divinationMonth = "巳月";
        public string divinationDay = "戊子日";
        public bool useFixedDivinationSeed;
        public int divinationSeed;
        public List<CheckOptionDefinition> options = new List<CheckOptionDefinition>();

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return Fail("Event ID is required.", out error);

            if (options == null || options.Count == 0)
                return Fail("The event must have at least one option.", out error);
            if (useDivination && !LiuYaoPaiPan.TryNormalizeCalendar(divinationMonth, divinationDay, out _, out _, out error))
                return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < options.Count; i++)
            {
                if (!ValidateOption(options[i], out string optionError))
                    return Fail("Option " + i + ": " + optionError, out error);

                if (!ids.Add(options[i].id))
                    return Fail("Duplicate option ID: " + options[i].id, out error);
            }

            error = null;
            return true;
        }

        public void ValidateOrThrow()
        {
            if (!Validate(out string error))
                throw new InvalidOperationException("Invalid check event: " + error);
        }

        public static bool ValidateOption(CheckOptionDefinition option, out string error)
        {
            if (option == null)
                return Fail("The option cannot be null.", out error);
            if (string.IsNullOrWhiteSpace(option.id))
                return Fail("Option ID is required.", out error);
            if (string.IsNullOrWhiteSpace(option.label))
                return Fail("Option label is required.", out error);
            if (!Enum.IsDefined(typeof(CheckBehavior), option.behavior))
                return Fail("The behavior is invalid.", out error);
            if (!ValidateFlags(option.requiredFlags, out error))
                return false;
            if (option.requiredItemAmount < 0)
                return Fail("Required item amount cannot be negative.", out error);

            bool hasRequiredItem = !string.IsNullOrWhiteSpace(option.requiredItemKey);
            if (hasRequiredItem && option.requiredItemAmount < 1)
                return Fail("A required item must have a positive amount.", out error);
            if (!hasRequiredItem && option.consumeRequiredItem)
                return Fail("Consuming an item requires an item key.", out error);
            if (option.success == null || option.failure == null)
                return Fail("Both success and failure outcomes are required.", out error);
            if (!ValidateOutcome(option.success, out string successError))
                return Fail("Success outcome: " + successError, out error);
            if (!ValidateOutcome(option.failure, out string failureError))
                return Fail("Failure outcome: " + failureError, out error);

            error = null;
            return true;
        }

        private static bool ValidateOutcome(CheckOutcomeDefinition outcome, out string error)
        {
            if (!ValidateFlags(outcome.grantedFlags, out error))
                return false;
            if (outcome.rewardItemAmount < 0)
                return Fail("Reward item amount cannot be negative.", out error);

            bool hasRewardItem = !string.IsNullOrWhiteSpace(outcome.rewardItemKey);
            if (hasRewardItem && outcome.rewardItemAmount < 1)
                return Fail("A reward item must have a positive amount.", out error);
            if (!hasRewardItem && outcome.rewardItemAmount != 0)
                return Fail("A reward item amount requires an item key.", out error);

            error = null;
            return true;
        }

        private static bool ValidateFlags(string[] flags, out string error)
        {
            if (flags != null)
            {
                for (int i = 0; i < flags.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(flags[i]))
                        return Fail("Flag IDs cannot be empty.", out error);
                }
            }

            error = null;
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
