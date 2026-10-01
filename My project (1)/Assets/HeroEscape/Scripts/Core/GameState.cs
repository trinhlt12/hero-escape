using System;

namespace HeroEscape.Core
{
    public enum GameOutcome
    {
        Playing,
        Won,
        Lost,
    }

    /// <summary>
    /// Trạng thái trận, bất biến: mỗi lần chuyển trạng thái tạo đối tượng mới,
    /// nên giữ tham chiếu tới trạng thái đầu màn là đủ để Restart.
    /// </summary>
    public sealed class GameState : IEquatable<GameState>
    {
        public LevelData Level { get; }
        public GridPos HeroPos { get; }
        public GridPos EnemyPos { get; }
        public int Round { get; }
        public GameOutcome Outcome { get; }

        /// <summary>Số lần Dash còn lại trong lần chơi này.</summary>
        public int DashUsesLeft { get; }

        GameState(LevelData level, GridPos heroPos, GridPos enemyPos, int round, GameOutcome outcome, int dashUsesLeft)
        {
            Level = level;
            HeroPos = heroPos;
            EnemyPos = enemyPos;
            Round = round;
            Outcome = outcome;
            DashUsesLeft = dashUsesLeft;
        }

        public static GameState CreateInitial(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            return new GameState(level, level.HeroStart, level.EnemyStart, 1, GameOutcome.Playing, level.DashMaxUses);
        }

        public GameState WithHero(GridPos pos) => new GameState(Level, pos, EnemyPos, Round, Outcome, DashUsesLeft);
        public GameState WithEnemy(GridPos pos) => new GameState(Level, HeroPos, pos, Round, Outcome, DashUsesLeft);
        public GameState WithOutcome(GameOutcome outcome) => new GameState(Level, HeroPos, EnemyPos, Round, outcome, DashUsesLeft);
        public GameState WithNextRound() => new GameState(Level, HeroPos, EnemyPos, Round + 1, Outcome, DashUsesLeft);
        public GameState WithDashUsed() => new GameState(Level, HeroPos, EnemyPos, Round, Outcome, DashUsesLeft - 1);

        public bool Equals(GameState other) =>
            other != null && ReferenceEquals(Level, other.Level) && HeroPos == other.HeroPos &&
            EnemyPos == other.EnemyPos && Round == other.Round && Outcome == other.Outcome &&
            DashUsesLeft == other.DashUsesLeft;

        public override bool Equals(object obj) => Equals(obj as GameState);

        public override int GetHashCode() =>
            (((HeroPos.GetHashCode() * 397 ^ EnemyPos.GetHashCode()) * 397 ^ Round) * 397 ^ (int)Outcome) * 397 ^ DashUsesLeft;

        public override string ToString() => $"Vòng {Round} Hero{HeroPos} X01{EnemyPos} Dash {DashUsesLeft} {Outcome}";
    }
}
