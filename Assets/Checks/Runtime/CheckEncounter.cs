using System;
using System.Collections.Generic;
using Emerge.Props;
using Emerge.GameFlow;
using UnityEngine;

namespace Emerge.Checks
{
    public static class CheckEncounter
    {
        public static bool TryBegin(PropInstance owner, PlayerInteractor actor, CheckEventDefinition definition)
        {
            if (owner == null || actor == null || definition == null || !owner.isActiveAndEnabled ||
                actor.IsInDialogue || !GameSessionController.GameplayInputAllowed) return false;
            if (!definition.Validate(out string error)) { actor.ShowFeedback("检定配置无效：" + error); return false; }
            var state = actor.GetComponent<CheckActorState>();
            if (state == null) state = actor.gameObject.AddComponent<CheckActorState>();
            CheckSession session;
            try { session = state.GetOrPrepare(definition, owner.InstanceId + ":" + definition.eventId); }
            catch (Exception exception) { actor.ShowFeedback(exception.Message); return false; }

            if (session.phase == CheckSessionPhase.Preparing)
                return CheckCastingUI.StartCasting(owner, actor, definition, state, session, () =>
                {
                    if (owner != null && owner.isActiveAndEnabled && actor != null && state != null)
                        TryBegin(owner, actor, definition);
                });

            Action<bool> finished = completed =>
            {
                if (state == null) return;
                if (completed)
                {
                    bool pendingCompletion = session.phase == CheckSessionPhase.Resolved;
                    if (pendingCompletion) state.RecordTrace(session, "继续剧情", "结果对白与后续剧情播放完毕。");
                    state.Complete(session);
                }
                else state.RecordTrace(session, "关闭对话", "已关闭检定对白；现有准备和结算结果保留。");
            };
            if (session.phase != CheckSessionPhase.Ready)
                return actor.BeginDialogue(owner, ResultLines(definition, session), finished);

            var choices = new List<PropDialogueChoice>();
            foreach (var option in definition.options)
            {
                bool allowed = state.CanChoose(option, out string reason);
                var behavior = CheckResolver.EffectiveBehavior(session, option.behavior);
                int baseValue = session.attributes.Get(behavior);
                int bonus = session.modifiers[(int)behavior];
                long total = (long)baseValue + bonus;
                string label = option.label + " · " + BehaviorName(behavior) + " " + baseValue +
                    (bonus >= 0 ? " + " : " − ") + Math.Abs((long)bonus) + " = " + total;
                if (definition.revealDifficultyBeforeChoice) label += " / 目标 " + option.targetValue;
                if (option.consumeRequiredItem) label += " · 消耗" + state.RequiredItemName(option.requiredItemKey) + " × " + option.requiredItemAmount;
                if (total < int.MinValue || total > int.MaxValue) { allowed = false; reason = "检定点数超出可用范围。"; }
                choices.Add(new PropDialogueChoice { id = option.id, label = label, enabled = allowed, disabledReason = reason });
            }
            var introduction = new List<PropDialogueLine>
            {
                new PropDialogueLine { speaker = string.IsNullOrWhiteSpace(definition.speaker) ? definition.title : definition.speaker,
                    text = string.IsNullOrWhiteSpace(definition.intro) ? "请选择解决方法。" : definition.intro }
            };
            return actor.BeginOptionDialogue(owner, introduction, choices, id =>
            {
                if (actor == null || state == null) return;
                var selected = definition.options.Find(option => option.id == id);
                state.RecordTrace(session, "选择行动", selected == null ? id : selected.label + "（" + id + "）");
                if (state.TryResolve(definition, session, id, out var result, out string message))
                {
                    state.RecordTrace(session, "结果分支", (result.success ? "成功" : "失败") + "：最终值 " + result.finalValue +
                        "，目标值 " + result.targetValue + "，进入对应对白与后续剧情。");
                    actor.ReplaceDialogue(ResultLines(definition, session));
                }
                else
                {
                    state.RecordTrace(session, "行动未提交", message);
                    actor.CancelDialogue();
                    if (owner != null && owner.isActiveAndEnabled) TryBegin(owner, actor, definition);
                    actor.ShowFeedback(message);
                }
            }, finished);
        }

        private static List<PropDialogueLine> ResultLines(CheckEventDefinition definition, CheckSession session)
        {
            var result = session.result;
            if (result == null) return new List<PropDialogueLine> { new PropDialogueLine { speaker = definition.speaker, text = "本次检定结果不可用。" } };
            var option = definition.options.Find(item => item.id == result.optionId);
            var outcome = option == null ? null : (result.success ? option.success : option.failure);
            string comparison = result.success ? " ≥ " : " < ";
            var lines = new List<PropDialogueLine>
            {
                new PropDialogueLine { speaker = result.success ? "检定成功" : "检定失败",
                    text = BehaviorName(result.behavior) + "：基础 " + result.baseValue + " + 加值 " + result.modifier +
                        " = " + result.finalValue + comparison + "目标 " + result.targetValue }
            };
            if (outcome != null && !string.IsNullOrWhiteSpace(outcome.text))
                lines.Add(new PropDialogueLine { speaker = definition.speaker, text = outcome.text });
            string continuation = outcome != null ? outcome.continuation : definition.completedText;
            if (!string.IsNullOrWhiteSpace(continuation))
                lines.Add(new PropDialogueLine { speaker = "后续剧情", text = continuation });
            else if (!string.IsNullOrWhiteSpace(definition.completedText))
                lines.Add(new PropDialogueLine { speaker = "后续剧情", text = definition.completedText });
            return lines;
        }

        public static string BehaviorName(CheckBehavior behavior)
        {
            switch (behavior)
            {
                case CheckBehavior.Parent: return "父母 / 强化";
                case CheckBehavior.Offspring: return "子孙 / 创造";
                case CheckBehavior.Officer: return "官鬼 / 应对";
                case CheckBehavior.Wealth: return "妻财 / 支配";
                case CheckBehavior.Sibling: return "兄弟 / 同化";
                case CheckBehavior.Self: return "我 / 认知";
                default: return "未知行为";
            }
        }
    }
}
