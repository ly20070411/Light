using System;
using System.Collections.Generic;
using Emerge.Props;
using UnityEngine;
using Emerge.Checks.Divination;

namespace Emerge.Checks
{
    [RequireComponent(typeof(PropGameState)), DisallowMultipleComponent]
    public sealed partial class CheckActorState : MonoBehaviour
    {
        [Serializable] public sealed class Snapshot
        {
            public int version = 1;
            public int attributeRulesVersion;
            public ActorCheckAttributes attributes = new ActorCheckAttributes();
            public List<PointEquipmentSlot> pointEquipment = new List<PointEquipmentSlot>();
            public PointProgressionState progression = new PointProgressionState();
            public int contamination;
            public List<CheckSession> sessions = new List<CheckSession>();
        }

        public ActorCheckAttributes attributes = SixKinAttributes.DefaultBuild();
        public int AttributeRulesVersion { get; private set; }
        [SerializeField] private int contamination;
        [SerializeField] private List<CheckSession> sessions = new List<CheckSession>();
        private CheckPipeline pipeline;
        public int Contamination => contamination;
        public IReadOnlyList<CheckSession> Sessions => sessions;
        public event Action<CheckResult> Resolved;
        public event Action Changed;
        private PropGameState PropState => GetComponent<PropGameState>();

        public void ConfigurePreparationPipeline(CheckPipeline supplied)
        {
            if (supplied == null) throw new ArgumentNullException(nameof(supplied));
            if (sessions.Count != 0) throw new InvalidOperationException("会话开始后不能更换准备规则。");
            pipeline = supplied;
        }

        public void RecordTrace(CheckSession session, string stage, string message)
        {
            if (session == null || !sessions.Contains(session)) return;
            CheckLogicTrace.Record(session, stage, message);
            Changed?.Invoke();
        }

        public CheckSession GetOrPrepare(CheckEventDefinition definition, string contextId = null)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            definition.ValidateOrThrow();
            string key = string.IsNullOrWhiteSpace(contextId) ? definition.eventId : contextId;
            var existing = sessions.Find(session => session.contextId == key);
            if (existing != null)
            {
                if (existing.eventId != definition.eventId)
                    throw new InvalidOperationException("检定上下文已用于另一事件。");
                RecordTrace(existing, "复用会话", "沿用已保存的起卦与加值；当前阶段 " + existing.phase);
                return existing;
            }
            if (pipeline == null) pipeline = new CheckPipeline();
            var prepared = pipeline.Prepare(definition, attributes, AttributeRulesVersion);
            if (AttributeRulesVersion >= 3) prepared.pointEquipment = FreezePointEquipment();
            prepared.contextId = key;
            sessions.Add(prepared);
            RecordTrace(prepared, "事件入口", "事件 " + definition.eventId + "；上下文 " + key + "；冻结五亲角色基础值");
            if (prepared.divination != null)
                RecordTrace(prepared, "起卦输入", "月令 " + prepared.divination.month + "；日辰 " + prepared.divination.day +
                    "；种子 " + prepared.divination.casting.seed + "；正面=3、背面=2，三枚铜币掷六次；规则 " + prepared.divination.rulesVersion);
            Changed?.Invoke();
            return prepared;
        }

        public bool RevealNextCast(CheckSession session)
        {
            if (session == null || !sessions.Contains(session) || session.phase != CheckSessionPhase.Preparing ||
                session.divination == null || !CoinCasting.IsValid(session.divination.casting) ||
                session.divination.revealedLines < 0 || session.divination.revealedLines >= 6) return false;
            var record = session.divination;
            int index = record.revealedLines;
            string[] faces = new string[3];
            for (int coin = 0; coin < 3; coin++) faces[coin] = record.casting.coinFaces[index * 3 + coin] == 1 ? "正(3)" : "背(2)";
            record.revealedLines++;
            session.castingStatus = "已投掷 " + record.revealedLines + "/6 次";
            RecordTrace(session, "投掷", "第 " + record.revealedLines + " 次：" + string.Join(" + ", faces) + " = " + record.casting.yaoValues[index]);
            int value = record.casting.yaoValues[index];
            RecordTrace(session, "填爻", "自下而上第 " + record.revealedLines + " 爻；本卦 " + (value == 7 || value == 9 ? "阳" : "阴") +
                "；变卦 " + (value == 6 || value == 7 ? "阳" : "阴") + (value == 6 || value == 9 ? "；动爻" : "；静爻"));
            return true;
        }

        public bool FinishPreparation(CheckSession session, out string error)
        {
            error = "";
            if (session == null || !sessions.Contains(session)) { error = "检定会话不属于此角色。"; return false; }
            var record = session.divination;
            if (session.phase == CheckSessionPhase.Ready && record != null && record.chart != null)
            {
                if (ValidDivination(session)) return true;
                error = "已准备的起卦输入、排盘或加值不一致。";
                return false;
            }
            if (session.phase != CheckSessionPhase.Preparing || record == null || record.revealedLines != 6 || !CoinCasting.IsValid(record.casting))
            { error = "请先完成六次铜币投掷。"; return false; }
            if (!ValidDivination(session))
            { error = "起卦输入或准备状态不一致，未进行排盘。"; return false; }
            LiuYaoPaiPan.PaiPanResult chart;
            try { chart = new LiuYaoPaiPan().PaiPan(record.month, record.day, record.casting.yaoValues); }
            catch (ArgumentException exception) { error = exception.Message; RecordTrace(session, "排盘失败", error); return false; }
            record.chart = chart;
            session.modifiers = (int[])chart.behaviorModifiers.Clone();
            session.castingStatus = "六次铜币投掷完成，初爻至上爻输入已固定";
            session.chartStatus = "本卦 " + chart.benGuaName + " → 变卦 " + chart.bianGuaName;
            session.modifierStatus = session.attributeRulesVersion == 2 ? "采用文档评分：五亲同类取最高，缺类为0；认知归入父母" : "采用文档评分：同类六亲取最高，缺类为0；我取世爻得分，保留正负检定点数";
            session.phase = CheckSessionPhase.Ready;
            RecordTrace(session, "排盘", session.chartStatus + "；" + chart.benGong + "宫 / " + chart.benGongWuxing +
                "；世爻 " + (chart.shiYaoIndex + 1) + "、应爻 " + (chart.yingYaoIndex + 1));
            foreach (var line in chart.yaos)
                RecordTrace(session, "爻加值", line.yaowei + " " + line.benGanzhi + " " + line.benWuxing + " " + line.benLiuqin +
                    "：" + string.Join("；", line.wangshuaiDetails) + " → " + line.wangshuaiScore);
            for (int behavior = 0; behavior < (session.attributeRulesVersion == 2 ? 5 : 6); behavior++)
                RecordTrace(session, "行为加值", CheckEncounter.BehaviorName((CheckBehavior)behavior) + " = " + session.modifiers[behavior]);
            return true;
        }

        public bool CanChoose(CheckOptionDefinition option, out string reason)
        {
            if (!CheckEventDefinition.ValidateOption(option, out reason)) return false;
            var state = PropState;
            if (state == null) { reason = "角色缺少背包与事件状态。"; return false; }
            foreach (string flag in option.requiredFlags ?? Array.Empty<string>())
                if (!state.HasFlag(flag)) { reason = "尚未满足此行动的前置条件。"; return false; }
            if (!string.IsNullOrWhiteSpace(option.requiredItemKey) && state.Count(option.requiredItemKey) < option.requiredItemAmount)
            { reason = "需要 " + RequiredItemName(option.requiredItemKey) + " × " + option.requiredItemAmount; return false; }
            reason = "";
            return true;
        }

        public string RequiredItemName(string key)
        {
            foreach (var entry in PropState.Inventory)
                if (entry.key == key && !string.IsNullOrWhiteSpace(entry.displayName)) return entry.displayName;
            var library = GetComponent<PlayerInteractor>()?.propLibrary;
            if (library != null)
                foreach (var item in library.Props)
                    if (item != null && item.InventoryKey == key) return item.DisplayName;
            return "所需物品";
        }

        public bool TryResolve(CheckEventDefinition definition, CheckSession session, string optionId,
            out CheckResult result, out string error)
        {
            result = null;
            error = "";
            if (definition == null || !definition.Validate(out error)) return false;
            if (session == null || !sessions.Contains(session) || session.eventId != definition.eventId)
            { error = "检定会话不属于当前角色或事件。"; return false; }
            if (session.phase == CheckSessionPhase.Preparing)
            { error = "请先完成本次起卦与排盘。"; return false; }
            if (session.phase != CheckSessionPhase.Ready || session.result != null || session.outcomeApplied)
            { error = "本次检定已经结算。"; return false; }
            if (session.attributes == null || session.modifiers == null || session.modifiers.Length != 6)
            { error = "检定会话缺少完整的属性或加值数据。"; return false; }
            if (!ValidDivination(session))
            { error = "起卦输入、排盘或加值不一致，行动未提交。"; return false; }
            var option = definition.options.Find(item => item.id == optionId);
            if (option == null) { error = "没有这个行动选项。"; return false; }
            if (!CanChoose(option, out error)) return false;
            var behavior = CheckResolver.EffectiveBehavior(session, option.behavior);
            RecordTrace(session, "提交行动", option.label + "；基础 " + session.attributes.Get(behavior) + " + 加值 " + session.modifiers[(int)behavior] + "；目标 " + option.targetValue +
                "；前置标记 " + string.Join(",", option.requiredFlags ?? Array.Empty<string>()) + "；前置物品 " +
                (string.IsNullOrWhiteSpace(option.requiredItemKey) ? "无" : option.requiredItemKey + " × " + option.requiredItemAmount));

            CheckOutcomeDefinition outcome;
            int nextContamination;
            try
            {
                int finalValue = CheckResolver.Points(session, behavior).finalPoints;
                outcome = finalValue >= option.targetValue ? option.success : option.failure;
                nextContamination = Math.Max(0, checked(contamination + outcome.contaminationDelta));
                if (!string.IsNullOrWhiteSpace(outcome.rewardItemKey))
                    if ((long)PropState.Count(outcome.rewardItemKey) + outcome.rewardItemAmount > int.MaxValue)
                        throw new OverflowException("背包物品数量超出可用范围。");
                result = CheckResolver.Resolve(session, option);
            }
            catch (Exception exception) when (exception is OverflowException || exception is ArgumentException || exception is InvalidOperationException)
            { error = "无法结算检定：" + exception.Message; return false; }

            // The resolver seals the session before inventory notifications can re-enter this method.
            if (option.consumeRequiredItem && !PropState.RemoveItem(option.requiredItemKey, option.requiredItemAmount))
            {
                session.result = null;
                session.phase = CheckSessionPhase.Ready;
                result = null;
                error = "所需物品不足，行动未提交。";
                return false;
            }
            session.outcomeApplied = true;
            if (result.pointCalculation != null) RecordTrace(session, "点数链", result.pointCalculation.Describe());
            contamination = nextContamination;
            foreach (string flag in outcome.grantedFlags ?? Array.Empty<string>()) PropState.SetFlag(flag);
            if (!string.IsNullOrWhiteSpace(outcome.rewardItemKey))
                PropState.AddItem(outcome.rewardItemKey, string.IsNullOrWhiteSpace(outcome.rewardItemName) ? outcome.rewardItemKey : outcome.rewardItemName, outcome.rewardItemAmount);
            Changed?.Invoke();
            RecordTrace(session, "判定", result.finalValue + (result.success ? " ≥ " : " < ") + result.targetValue + "；" + (result.success ? "成功" : "失败"));
            RecordTrace(session, "应用后果", "分支 " + (result.success ? "成功" : "失败") + "；物品消耗 " + (option.consumeRequiredItem ? option.requiredItemKey + " × " + option.requiredItemAmount : "无") +
                "；侵染变动 " + outcome.contaminationDelta + "；标记 " + string.Join(",", outcome.grantedFlags ?? Array.Empty<string>()) +
                "；奖励 " + (string.IsNullOrWhiteSpace(outcome.rewardItemKey) ? "无" : outcome.rewardItemKey + " × " + outcome.rewardItemAmount));
            Resolved?.Invoke(result);
            return true;
        }

        public void Complete(CheckSession session)
        {
            if (session == null || !sessions.Contains(session) || session.phase != CheckSessionPhase.Resolved || !session.outcomeApplied) return;
            session.phase = CheckSessionPhase.Completed;
            RecordTrace(session, "剧情完成", "结果与分支后续已读完；会话完成");
            Changed?.Invoke();
        }

        public Snapshot CaptureSnapshot() => JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(new Snapshot
        { attributeRulesVersion = AttributeRulesVersion, attributes = attributes, pointEquipment = pointEquipment, progression = progression, contamination = contamination, sessions = sessions }));

        public bool TrySetAllocatedAttributes(ActorCheckAttributes allocated)
        {
            if (!SixKinAttributes.IsValidBuild(allocated)) return false;
            // Keep the live session objects and their frozen attributes, including an open dialogue.
            attributes = allocated.Clone();
            AttributeRulesVersion = SixKinAttributes.RulesVersion;
            Changed?.Invoke();
            return true;
        }

        public bool RestoreSnapshot(Snapshot saved)
        {
            if (!IsValidSnapshot(saved)) return false;
            // Copy before cancellation callbacks can touch the previous live state.
            var copy = JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(saved));
            GetComponent<PlayerInteractor>()?.CancelDialogue();
            attributes = copy.attributes;
            pointEquipment = copy.pointEquipment ?? new List<PointEquipmentSlot>();
            progression = copy.progression ?? new PointProgressionState();
            AttributeRulesVersion = copy.attributeRulesVersion;
            contamination = copy.contamination;
            sessions = copy.sessions;
            Changed?.Invoke();
            return true;
        }

        public static bool IsValidSnapshot(Snapshot saved)
        {
            if (saved == null || saved.version != 1 || saved.attributeRulesVersion < 0 || saved.attributeRulesVersion > SixKinAttributes.RulesVersion ||
                (saved.attributeRulesVersion == SixKinAttributes.RulesVersion && !SixKinAttributes.IsValidBuild(saved.attributes)) ||
                (saved.attributeRulesVersion == 2 && !SixKinAttributes.IsValidLegacyBuild(saved.attributes)) ||
                (saved.attributeRulesVersion >= 3 && !PointCalculation.ValidSlots(saved.pointEquipment)) || saved.attributes == null || saved.contamination < 0 ||
                saved.sessions == null || saved.sessions.Count > 10000 ||
                (saved.progression != null && !saved.progression.Valid(saved.pointEquipment ?? new List<PointEquipmentSlot>()))) return false;
            var contexts = new HashSet<string>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var session in saved.sessions)
            {
                if (session == null || string.IsNullOrWhiteSpace(session.eventId) || string.IsNullOrWhiteSpace(session.contextId) ||
                    string.IsNullOrWhiteSpace(session.sessionId) || !contexts.Add(session.contextId) || !ids.Add(session.sessionId) ||
                    session.attributeRulesVersion < 0 || session.attributeRulesVersion > SixKinAttributes.RulesVersion ||
                    session.attributes == null || session.modifiers == null || session.modifiers.Length != 6 ||
                    !Enum.IsDefined(typeof(CheckSessionPhase), session.phase)) return false;
                if (!ValidDivination(session) || (session.attributeRulesVersion >= 3 && !PointCalculation.ValidFrozenItems(session.pointEquipment))) return false;
                if (session.logicTrace != null)
                    for (int i = 0; i < session.logicTrace.Count; i++)
                        if (session.logicTrace[i] == null || session.logicTrace[i].sequence != i + 1 || string.IsNullOrWhiteSpace(session.logicTrace[i].stage)) return false;
                if (session.phase == CheckSessionPhase.Preparing)
                {
                    if (session.result != null || session.outcomeApplied) return false;
                    continue;
                }
                if (session.phase == CheckSessionPhase.Ready)
                {
                    if (session.result != null || session.outcomeApplied) return false;
                    continue;
                }
                var result = session.result;
                if (result == null || !session.outcomeApplied || string.IsNullOrWhiteSpace(result.optionId) ||
                    !Enum.IsDefined(typeof(CheckBehavior), result.behavior)) return false;
                long sum;
                PointCalculationResult calculation = null;
                try
                {
                    if (session.attributeRulesVersion >= 3) { calculation = CheckResolver.Points(session, result.behavior); sum = calculation.finalPoints; }
                    else sum = (long)session.attributes.Get(result.behavior) + session.modifiers[(int)result.behavior];
                }
                catch (Exception) { return false; }
                if (calculation != null && (result.pointCalculation == null || JsonUtility.ToJson(calculation) != JsonUtility.ToJson(result.pointCalculation))) return false;
                if (sum != result.finalValue || result.baseValue != session.attributes.Get(result.behavior) ||
                    result.modifier != session.modifiers[(int)result.behavior] ||
                    result.margin != (long)result.finalValue - result.targetValue || result.success != (result.finalValue >= result.targetValue)) return false;
            }
            return true;
        }

        private static bool ValidDivination(CheckSession session)
        {
            var record = session.divination;
            // Older saves and explicitly injected alternate pipelines have no divination record.
            if (record == null) return session.phase != CheckSessionPhase.Preparing;
            if (session.modifiers == null || session.modifiers.Length != 6) return false;
            if (record.rulesVersion != DivinationRecord.CurrentRulesVersion || !CoinCasting.IsValid(record.casting) ||
                record.revealedLines < 0 || record.revealedLines > 6 ||
                !LiuYaoPaiPan.TryNormalizeCalendar(record.month, record.day, out string month, out string day, out _) ||
                record.month != month || record.day != day) return false;
            if (session.phase == CheckSessionPhase.Preparing)
            {
                if (record.chart != null) return false;
                foreach (int modifier in session.modifiers) if (modifier != 0) return false;
                return true;
            }
            if (record.revealedLines != 6 || !LiuYaoPaiPan.IsValidResult(record.chart) ||
                record.chart.yueling != record.month || record.chart.richen != record.day) return false;
            for (int i = 0; i < 6; i++)
                if (record.chart.yao6789[i] != record.casting.yaoValues[i] || session.modifiers[i] != record.chart.behaviorModifiers[i]) return false;
            return true;
        }
    }
}
