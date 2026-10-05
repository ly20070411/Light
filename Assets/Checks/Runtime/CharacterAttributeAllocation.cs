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
        public const int Count = 5;
        public const int RulesVersion = 2;
        private static readonly SixKinAttributeInfo[] Definitions =
        {
            new SixKinAttributeInfo("父母", "强化", "积累知识、继承经验，强化已有能力，并理解、分析当前处境。", "学习、研习、分析、强化技能或装备。", "战斗中司防御：护盾、减伤。"),
            new SixKinAttributeInfo("子孙", "创造", "将想法变成实际成果，创造、制作或修复事物。", "制作道具、修复机关、改造设施。", "战斗中司调息：回气、净化。"),
            new SixKinAttributeInfo("官鬼", "应对", "面对外界威胁与不利影响时，保护自身并作出应对。", "躲避危险、抵抗侵染、承受冲击、摆脱异常。", "战斗中司攻伐：单体攻击、群体攻击。"),
            new SixKinAttributeInfo("妻财", "支配", "主动施加力量，改变目标的状态，取得控制权。", "攻击、压制、破坏、强行突破。", "战斗中司疗愈：直接治疗、持续恢复。"),
            new SixKinAttributeInfo("兄弟", "同化", "与他人及环境建立联系，通过观察、交流、探索和协作寻找出路。", "观察、探索、说服、交涉、协作。", "战斗中司控制：沉默、束缚。")
        };

        public static SixKinAttributeInfo Get(CheckBehavior attribute)
        {
            int index = (int)attribute;
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(attribute));
            return Definitions[index];
        }

        public static ActorCheckAttributes DefaultBuild() => new ActorCheckAttributes
        { parent = 2, offspring = 2, officer = 2, wealth = 1, sibling = 1 };

        public static bool IsValidBuild(ActorCheckAttributes attributes)
        {
            if (attributes == null || attributes.self != 0) return false;
            long total = 0;
            for (int i = 0; i < Count; i++)
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
            if (initial == null) return;
            if (!SixKinAttributes.IsValidBuild(initial)) throw new ArgumentException("初始五亲点数必须合计为 8。", nameof(initial));
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
                (delta == 1 && Remaining == 0) || (delta == -1 && points[index] == 0)) return false;
            points[index] += delta; Spent += delta; return true;
        }
        public void Reset() { Array.Clear(points, 0, points.Length); Spent = 0; }
        public ActorCheckAttributes ToAttributes() => new ActorCheckAttributes
        {
            parent = points[0], offspring = points[1], officer = points[2],
            wealth = points[3], sibling = points[4]
        };
    }
}
