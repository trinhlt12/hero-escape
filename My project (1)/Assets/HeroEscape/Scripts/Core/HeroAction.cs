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
