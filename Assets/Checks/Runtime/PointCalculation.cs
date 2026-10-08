using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Emerge.Checks
{
    [Serializable] public sealed class PointEquipmentSlot
    {
        public string instanceId, itemKey;
        public CheckBehavior attribute;
        public PointItemData instanceData;
    }
    [Serializable] public sealed class FrozenPointItem
    {
        public string instanceId;
        public CheckBehavior attribute;
        public PointItemData item;
    }
    [Serializable] public sealed class PointContext
    {
        public int basePoints, growthPoints, outsidePoints, checkPoints, currentPoints;
    }
    [Serializable] public sealed class PointRuleTrace
    {
        public int ruleIndex, before, after;
        public bool triggered;
    }
    [Serializable] public sealed class PointIteration
    {
        public int iteration, before, after;
        public string instanceId, itemKey, itemName;
        public List<PointRuleTrace> rules = new List<PointRuleTrace>();
    }
    [Serializable] public sealed class ResolvedPointAddon
    {
        public string instanceId, effectKey, description;
        public int amount;
    }
    public enum PointActionKind { Shield, ReduceEnemyAttackPoints, Damage, Vulnerability, DamageBonus, ActionPoints }
    [Serializable] public sealed class PointActionValue
    {
        public PointActionKind kind;
        // Ratios remain ratios; no hidden rounding, clamping, duration or stacking rule.
        public double amount;
        public string description;
    }
    [Serializable] public sealed class PointCalculationResult
    {
        public CheckBehavior attribute;
        public int basePoints, growthPoints, outsidePoints, checkPoints, initialCurrentPoints, finalPoints;
        public List<PointIteration> iterations = new List<PointIteration>();
        public List<ResolvedPointAddon> actionAddons = new List<ResolvedPointAddon>();
        public PointActionValue action;
        public string Describe()
        {
            string text = "基础 " + basePoints + " + 成长 " + growthPoints + " = 局外 " + outsidePoints +
                "；检定 " + checkPoints.ToString("+0;-0;0") + " → 当前点数[0] " + initialCurrentPoints;
            foreach (var step in iterations) text += "\n" + step.itemName + "：当前点数[" + step.iteration + "] " + step.before + " → " + step.after;
            return text + "\n最终点数 x = " + finalPoints + "；" + action.description;
        }
    }
    public static class PointCalculation
    {
        public static bool ValidSlots(List<PointEquipmentSlot> slots)
        {
            if (slots == null || slots.Count > 1024) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slot in slots)
                if (slot == null || string.IsNullOrWhiteSpace(slot.instanceId) || string.IsNullOrWhiteSpace(slot.itemKey) ||
                    !Enum.IsDefined(typeof(CheckBehavior), slot.attribute) || !ids.Add(slot.instanceId) ||
                    (slot.instanceData != null && (!slot.instanceData.IsValid() || slot.instanceData.key != slot.itemKey))) return false;
            return true;
        }
        public static bool ValidFrozenItems(List<FrozenPointItem> items)
        {
            if (items == null || items.Count > 1024) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
                if (item == null || string.IsNullOrWhiteSpace(item.instanceId) || !ids.Add(item.instanceId) ||
                    !Enum.IsDefined(typeof(CheckBehavior), item.attribute) || item.item == null || !item.item.IsValid()) return false;
            return true;
        }
        public static List<FrozenPointItem> Freeze(List<PointEquipmentSlot> slots, PointItemCatalog catalog)
        {
            if (!ValidSlots(slots)) throw new ArgumentException("成长道具配置无效。");
            var result = new List<FrozenPointItem>();
            foreach (var slot in slots)
            {
                var definition = catalog != null ? catalog.Find(slot.itemKey) : null;
                var data = slot.instanceData ?? definition?.data;
                if (data == null || !data.IsValid()) throw new InvalidOperationException("缺少有效的成长道具定义：" + slot.itemKey);
                result.Add(new FrozenPointItem { instanceId = slot.instanceId, attribute = slot.attribute, item = data.Clone() });
            }
            return result;
        }
        public static PointCalculationResult Calculate(ActorCheckAttributes baseAttributes, CheckBehavior attribute, int checkPoints,
            List<FrozenPointItem> items)
        {
            if (baseAttributes == null || !Enum.IsDefined(typeof(CheckBehavior), attribute) || !ValidFrozenItems(items))
                throw new ArgumentException("点数计算输入无效。");
            var selected = items.Where(item => item.attribute == attribute).ToList();
            var context = new PointContext { basePoints = baseAttributes.Get(attribute), growthPoints = selected.Count, checkPoints = checkPoints };
            context.outsidePoints = checked(context.basePoints + context.growthPoints);
            context.currentPoints = checked(context.outsidePoints + context.checkPoints);
            var result = new PointCalculationResult { attribute = attribute, basePoints = context.basePoints, growthPoints = context.growthPoints,
                outsidePoints = context.outsidePoints, checkPoints = checkPoints, initialCurrentPoints = context.currentPoints };
            foreach (var equipped in selected)
            {
                var iteration = new PointIteration { iteration = result.iterations.Count + 1, before = context.currentPoints,
                    instanceId = equipped.instanceId, itemKey = equipped.item.key, itemName = equipped.item.displayName };
                foreach (var rule in equipped.item.modifiers)
                {
                    int before = context.currentPoints;
                    bool triggered = rule.Matches(context);
                    if (triggered) context.currentPoints = rule.Apply(context);
                    iteration.rules.Add(new PointRuleTrace { ruleIndex = iteration.rules.Count, before = before, after = context.currentPoints, triggered = triggered });
                }
                iteration.after = context.currentPoints;
                result.iterations.Add(iteration);
                foreach (var addon in equipped.item.actionAddons)
                    result.actionAddons.Add(new ResolvedPointAddon { instanceId = equipped.instanceId, effectKey = addon.effectKey,
                        description = addon.description, amount = addon.amount.Read(context) });
            }
            result.finalPoints = context.currentPoints;
            result.action = ActionValue(attribute, result.finalPoints);
            return result;
        }
        public static PointActionValue ActionValue(CheckBehavior attribute, int x)
        {
            switch (attribute)
            {
                case CheckBehavior.Parent: return new PointActionValue { kind = PointActionKind.Shield, amount = x, description = "获得 " + x + " 点护盾" };
                case CheckBehavior.Offspring: return new PointActionValue { kind = PointActionKind.ReduceEnemyAttackPoints, amount = x * 1.25, description = "敌方攻击最终点数减少 " + (x * 1.25).ToString("0.###") };
                case CheckBehavior.Officer: return new PointActionValue { kind = PointActionKind.Damage, amount = x, description = "造成 " + x + " 点伤害" };
                case CheckBehavior.Wealth: return new PointActionValue { kind = PointActionKind.Vulnerability, amount = x * .125, description = "赋予 " + (x * 12.5).ToString("0.###") + "% 易伤" };
                case CheckBehavior.Sibling: return new PointActionValue { kind = PointActionKind.DamageBonus, amount = x * .1, description = "增加 " + (x * 10.0).ToString("0.###") + "% 伤害" };
                case CheckBehavior.Self: return new PointActionValue { kind = PointActionKind.ActionPoints, amount = x, description = "获得 " + x + " 点行动点" };
                default: throw new ArgumentOutOfRangeException(nameof(attribute));
            }
        }
    }
}
