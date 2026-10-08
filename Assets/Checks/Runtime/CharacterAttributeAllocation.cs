using System;

namespace Emerge.Checks
{
    public sealed class SixKinAttributeInfo
    {
        public readonly string name, behavior, description, actions, battleRole;
        public SixKinAttributeInfo(string name, string behavior, string description, string actions, string battleRole)
        { this.name = name; this.behavior = behavior; this.description = description; this.actions = actions; this.battleRole = battleRole; }
    }

    public static class SixKinAttributes
    {
        public const int StartingPoints = 8;
        public const int Count = 6;
        public const int Minimum = 1;
        public const int TotalBasePoints = Count * Minimum + StartingPoints;
        public const int RulesVersion = 3;
        private static readonly SixKinAttributeInfo[] Definitions =
        {
            new SixKinAttributeInfo("父母", "增伤", "基础行动：自身增伤增加 x × 10%。", "增伤 +x × 10%", "敌方回合结束清零；重复使用相加。"),
            new SixKinAttributeInfo("子孙", "易伤", "基础行动：目标易伤增加 x × 12.5%。", "易伤 +x × 12.5%", "敌方回合结束清零；重复使用相加。"),
            new SixKinAttributeInfo("官鬼", "护盾", "基础行动：获得最终点数 x 点护盾。", "获得 x 点护盾", "护盾吸收伤害，自己的下回合开始清零。"),
            new SixKinAttributeInfo("妻财", "进攻", "基础行动：对目标造成最终点数 x 点伤害。", "造成 x 点伤害", "伤害受增伤、目标易伤和敌方效果影响。"),
            new SixKinAttributeInfo("兄弟", "削弱", "基础行动：目标敌人攻击点数减少 x × 1.25。", "攻击点数 −x × 1.25", "敌方回合结束清零；重复使用相加。"),
            new SixKinAttributeInfo("我", "行动点", "每回合定卦后，自动获得最终点数 x 点行动点。", "自动获得 x 行动点", "每回合另获 3 AP；每次行动消耗 2 AP，手动结束回合。")
        };

        public static SixKinAttributeInfo Get(CheckBehavior attribute)
        {
            int index = (int)attribute;
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(attribute));
            return Definitions[index];
        }

        public static ActorCheckAttributes DefaultBuild() => new ActorCheckAttributes
        { parent = 3, offspring = 2, officer = 3, wealth = 2, sibling = 2, self = 2 };

        public static bool IsValidBuild(ActorCheckAttributes attributes)
        {
            if (attributes == null) return false;
            long total = 0;
            for (int i = 0; i < Count; i++)
            {
                int value = attributes.Get((CheckBehavior)i);
                if (value < Minimum || value > Minimum + StartingPoints) return false;
                total += value;
            }
            return total == TotalBasePoints;
        }

        public static bool IsValidLegacyBuild(ActorCheckAttributes attributes)
        {
            if (attributes == null || attributes.self != 0) return false;
            long total = 0;
            for (int i = 0; i < 5; i++)
            {
                int value = attributes.Get((CheckBehavior)i);
                if (value < 0 || value > StartingPoints) return false;
                total += value;
            }
            return total == StartingPoints;
        }
    }

    // A draft only exists on the creation page. Confirmed values use the normal check-state save format.
    public sealed class CharacterAttributeAllocation
    {
        private readonly int[] points = new int[SixKinAttributes.Count];
        public int Spent { get; private set; }
        public int Remaining => SixKinAttributes.StartingPoints - Spent;
        public bool IsComplete => Remaining == 0;
        public CharacterAttributeAllocation(ActorCheckAttributes initial = null)
        {
            Reset();
            if (initial == null) return;
            if (!SixKinAttributes.IsValidBuild(initial)) throw new ArgumentException("六亲各至少 1 点，额外分配点数必须合计为 8。", nameof(initial));
            for (int i = 0; i < points.Length; i++) points[i] = initial.Get((CheckBehavior)i);
            Spent = SixKinAttributes.StartingPoints;
        }
        public int Get(CheckBehavior attribute)
        {
            int index = (int)attribute;
            if (index < 0 || index >= points.Length) throw new ArgumentOutOfRangeException(nameof(attribute));
            return points[index];
        }
        public bool TryChange(CheckBehavior attribute, int delta)
        {
            int index = (int)attribute;
            if (index < 0 || index >= points.Length || (delta != 1 && delta != -1) ||
                (delta == 1 && Remaining == 0) || (delta == -1 && points[index] == SixKinAttributes.Minimum)) return false;
            points[index] += delta; Spent += delta; return true;
        }
        public void Reset() { for (int i = 0; i < points.Length; i++) points[i] = SixKinAttributes.Minimum; Spent = 0; }
        public ActorCheckAttributes ToAttributes() => new ActorCheckAttributes
        {
            parent = points[0], offspring = points[1], officer = points[2],
            wealth = points[3], sibling = points[4], self = points[5]
        };
    }
}
