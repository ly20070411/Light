using System;
using Emerge.Checks.Divination;

namespace Emerge.Checks
{
    // Explicit provider injection remains available for deterministic tests and alternate game rules.
    public interface ICheckCastingProvider
    {
        string Prepare(CheckEventDefinition definition);
    }

    public interface ICheckChartProvider
    {
        string Prepare(CheckEventDefinition definition, string castingStatus);
    }

    public interface ICheckModifierProvider
    {
        CheckModifierPreparation Prepare(CheckEventDefinition definition, string chartStatus);
    }

    public sealed class PlaceholderCastingProvider : ICheckCastingProvider
    {
        public string Prepare(CheckEventDefinition definition)
        {
            return "起卦：占位，暂未执行";
        }
    }

    public sealed class PlaceholderChartProvider : ICheckChartProvider
    {
        public string Prepare(CheckEventDefinition definition, string castingStatus)
        {
            return "排盘：占位，暂未执行";
        }
    }

    public sealed class PlaceholderModifierProvider : ICheckModifierProvider
    {
        public CheckModifierPreparation Prepare(CheckEventDefinition definition, string chartStatus)
        {
            return new CheckModifierPreparation
            {
                status = "加值计算：占位，六类行为加值均为 0",
                modifiers = new int[6]
            };
        }
    }

    public sealed class CheckPipeline
    {
        private readonly ICheckCastingProvider castingProvider;
        private readonly ICheckChartProvider chartProvider;
        private readonly ICheckModifierProvider modifierProvider;
        private readonly bool customProviders;

        public CheckPipeline(
            ICheckCastingProvider castingProvider = null,
            ICheckChartProvider chartProvider = null,
            ICheckModifierProvider modifierProvider = null)
        {
            customProviders = castingProvider != null || chartProvider != null || modifierProvider != null;
            this.castingProvider = castingProvider ?? new PlaceholderCastingProvider();
            this.chartProvider = chartProvider ?? new PlaceholderChartProvider();
            this.modifierProvider = modifierProvider ?? new PlaceholderModifierProvider();
        }

        public CheckSession Prepare(CheckEventDefinition definition, ActorCheckAttributes attributes)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (attributes == null)
                throw new ArgumentNullException(nameof(attributes));
            definition.ValidateOrThrow();

            // Freeze attributes before entering any provider, so later actor edits do not reroll the check.
            ActorCheckAttributes snapshot = attributes.Clone();
            if (!customProviders && definition.useDivination)
            {
                if (!LiuYaoPaiPan.TryNormalizeCalendar(definition.divinationMonth, definition.divinationDay,
                    out string month, out string day, out string error)) throw new ArgumentException(error);
                int seed = definition.useFixedDivinationSeed ? definition.divinationSeed : BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
                return new CheckSession
                {
                    eventId = definition.eventId, contextId = definition.eventId, sessionId = Guid.NewGuid().ToString("N"),
                    attributeRulesVersion = SixKinAttributes.RulesVersion,
                    attributes = snapshot, modifiers = new int[6], phase = CheckSessionPhase.Preparing,
                    castingStatus = "等待掷出三枚铜币，共六次", chartStatus = "等待六爻齐全后排盘", modifierStatus = "等待排盘加值",
                    divination = new DivinationRecord { month = month, day = day, casting = CoinCasting.Cast(seed) }
                };
            }
            string castingStatus = castingProvider.Prepare(definition);
            string chartStatus = chartProvider.Prepare(definition, castingStatus);
            CheckModifierPreparation preparation = modifierProvider.Prepare(definition, chartStatus);
            if (preparation == null || preparation.modifiers == null || preparation.modifiers.Length != 6)
                throw new InvalidOperationException("The modifier provider must return exactly six behavior modifiers.");

            return new CheckSession
            {
                eventId = definition.eventId,
                contextId = definition.eventId,
                sessionId = Guid.NewGuid().ToString("N"),
                attributeRulesVersion = SixKinAttributes.RulesVersion,
                attributes = snapshot,
                modifiers = (int[])preparation.modifiers.Clone(),
                castingStatus = castingStatus,
                chartStatus = chartStatus,
                modifierStatus = preparation.status,
                phase = CheckSessionPhase.Ready,
                result = null,
                outcomeApplied = false
            };
        }
    }
}
