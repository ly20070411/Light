using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Checks
{
    // All point operands name their scope explicitly. Current is evaluated again after each rule.
    public enum PointSource { Constant, Base, Growth, Outside, Check, Current }
    public enum PointComparison { Always, Less, LessOrEqual, Equal, GreaterOrEqual, Greater, NotEqual }
    public enum PointOperation { Add, Set, Multiply, Minimum, Maximum }

    [Serializable] public sealed class PointOperand
    {
        public PointSource source;
        public int constant;
        public int Read(PointContext context)
        {
            switch (source)
            {
                case PointSource.Constant: return constant;
                case PointSource.Base: return context.basePoints;
                case PointSource.Growth: return context.growthPoints;
                case PointSource.Outside: return context.outsidePoints;
                case PointSource.Check: return context.checkPoints;
                case PointSource.Current: return context.currentPoints;
                default: throw new ArgumentOutOfRangeException(nameof(source));
            }
        }
    }
    [Serializable] public sealed class PointModifierRule
    {
        public PointComparison comparison;
        public PointOperand left = new PointOperand { source = PointSource.Current };
        public PointOperand right = new PointOperand { source = PointSource.Outside };
        public PointOperation operation;
        public PointOperand operand = new PointOperand();
        public bool Matches(PointContext context)
        {
            if (comparison == PointComparison.Always) return true;
            int a = left.Read(context), b = right.Read(context);
            switch (comparison)
            {
                case PointComparison.Less: return a < b;
                case PointComparison.LessOrEqual: return a <= b;
                case PointComparison.Equal: return a == b;
                case PointComparison.GreaterOrEqual: return a >= b;
                case PointComparison.Greater: return a > b;
                case PointComparison.NotEqual: return a != b;
                default: throw new ArgumentOutOfRangeException(nameof(comparison));
            }
        }
        public int Apply(PointContext context)
        {
            int value = operand.Read(context);
            switch (operation)
            {
                case PointOperation.Add: return checked(context.currentPoints + value);
                case PointOperation.Set: return value;
                case PointOperation.Multiply: return checked(context.currentPoints * value);
                case PointOperation.Minimum: return Math.Min(context.currentPoints, value);
                case PointOperation.Maximum: return Math.Max(context.currentPoints, value);
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }
    }
    [Serializable] public sealed class PointActionAddon
    {
        public string effectKey;
        [TextArea] public string description;
        public PointOperand amount = new PointOperand();
    }
    [Serializable] public sealed class PointItemData
    {
        public string key, displayName;
        [TextArea] public string description;
        public List<PointModifierRule> modifiers = new List<PointModifierRule>();
        public List<PointActionAddon> actionAddons = new List<PointActionAddon>();
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(displayName) || modifiers == null || actionAddons == null ||
                modifiers.Count > 128 || actionAddons.Count > 128) return false;
            foreach (var rule in modifiers)
                if (rule == null || !Enum.IsDefined(typeof(PointComparison), rule.comparison) || !Enum.IsDefined(typeof(PointOperation), rule.operation) ||
                    !ValidOperand(rule.left) || !ValidOperand(rule.right) || !ValidOperand(rule.operand)) return false;
            foreach (var effect in actionAddons)
                if (effect == null || string.IsNullOrWhiteSpace(effect.effectKey) || !ValidOperand(effect.amount)) return false;
            return true;
        }
        private static bool ValidOperand(PointOperand value) => value != null && Enum.IsDefined(typeof(PointSource), value.source);
        public PointItemData Clone() => JsonUtility.FromJson<PointItemData>(JsonUtility.ToJson(this));
    }
    [CreateAssetMenu(menuName = "Emerge/点数系统/成长道具", fileName = "PointItem")]
    public sealed class PointItemDefinition : ScriptableObject
    {
        public bool randomizeOnAcquire;
        public PointItemData data = new PointItemData();
    }
}
