namespace HeroEscape.Core
{
    public enum HeroActionKind
    {
        Move,
        Wait,
    }

    /// <summary>Hành động chính của Hero trong một lượt: đi một ô theo một hướng, hoặc Chờ.</summary>
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

        public override string ToString() => Kind == HeroActionKind.Wait ? "WAIT" : Direction.ToString();
    }
}
