namespace HeroEscape.Core
{
    public enum HeroActionKind
    {
        Move,
        Wait,
        Dash,
    }

    /// <summary>
    /// Hành động chính của Hero trong một lượt: đi một ô, Chờ, hoặc Dash (đi thẳng 2 ô theo một hướng).
    /// </summary>
    public readonly struct HeroAction
    {
        public readonly HeroActionKind Kind;
        public readonly Direction Direction;

        HeroAction(HeroActionKind kind, Direction direction)
        {
            Kind = kind;
            Direction = direction;
        }

        public static HeroAction Move(Direction direction) => new HeroAction(HeroActionKind.Move, direction);

        public static HeroAction Wait => new HeroAction(HeroActionKind.Wait, default);

        public static HeroAction Dash(Direction direction) => new HeroAction(HeroActionKind.Dash, direction);

        public override string ToString()
        {
            switch (Kind)
            {
                case HeroActionKind.Wait: return "WAIT";
                case HeroActionKind.Dash: return "DASH-" + Direction;
                default: return Direction.ToString();
            }
        }
    }
}

namespace HeroEscape.Core
{
    /// <summary>
    /// Một lượt trong lời giải ghi trong file level: phụ trợ tùy chọn (Stasis lên một X theo ID, hoặc Barrier ở ô kề
    /// Hero theo hướng), rồi đúng một hành động chính.
    /// </summary>
    public readonly struct SolutionTurn
    {
        /// <summary>ID X01 bị Stasis trước hành động chính; null nếu không dùng Stasis.</summary>
        public readonly string StasisTarget;

        /// <summary>Hướng từ Hero tới ô đặt Barrier trước hành động chính; null nếu không dùng Barrier.</summary>
        public readonly Direction? BarrierDirection;

        public readonly HeroAction Main;

        public SolutionTurn(string stasisTarget, Direction? barrierDirection, HeroAction main)
        {
            StasisTarget = stasisTarget;
            BarrierDirection = barrierDirection;
            Main = main;
        }

        public override string ToString() =>
            (StasisTarget != null ? $"STASIS({StasisTarget})+" : string.Empty) +
            (BarrierDirection.HasValue ? $"BARRIER-{BarrierDirection.Value}+" : string.Empty) + Main;
    }
}
