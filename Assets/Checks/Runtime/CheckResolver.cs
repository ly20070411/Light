using System;

namespace Emerge.Checks
{
    public static class CheckResolver
    {
        public static CheckBehavior EffectiveBehavior(CheckSession session, CheckBehavior behavior)
            => session.attributeRulesVersion == 2 && behavior == CheckBehavior.Self ? CheckBehavior.Parent : behavior;
        public static PointCalculationResult Points(CheckSession session, CheckBehavior behavior)
            => PointCalculation.Calculate(session.attributes, EffectiveBehavior(session, behavior),
                session.modifiers[(int)EffectiveBehavior(session, behavior)], session.attributeRulesVersion >= 3 ? session.pointEquipment : new System.Collections.Generic.List<FrozenPointItem>());
        public static CheckResult Resolve(CheckSession session, CheckOptionDefinition option)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (option == null)
                throw new ArgumentNullException(nameof(option));
            if (session.phase != CheckSessionPhase.Ready || session.result != null || session.outcomeApplied)
                throw new InvalidOperationException("This session has already been resolved.");
            if (session.attributes == null)
                throw new InvalidOperationException("The session has no attribute snapshot.");
            if (session.modifiers == null || session.modifiers.Length != 6)
                throw new InvalidOperationException("The session must contain exactly six behavior modifiers.");
            if (!CheckEventDefinition.ValidateOption(option, out string error))
                throw new ArgumentException("Invalid check option: " + error, nameof(option));

            var behavior = EffectiveBehavior(session, option.behavior);
            int baseValue = session.attributes.Get(behavior);
            int modifier = session.modifiers[(int)behavior];
            var calculation = Points(session, behavior);
            int finalValue = calculation.finalPoints;
            var result = new CheckResult
            {
                optionId = option.id,
                behavior = behavior,
                baseValue = baseValue,
                modifier = modifier,
                finalValue = finalValue,
                pointCalculation = session.attributeRulesVersion >= 3 ? calculation : null,
                targetValue = option.targetValue,
                margin = (long)finalValue - option.targetValue,
                success = finalValue >= option.targetValue
            };

            // Do not change the session until all validation and arithmetic have succeeded.
            session.result = result;
            session.phase = CheckSessionPhase.Resolved;
            return result;
        }
    }
}
