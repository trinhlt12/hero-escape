using HeroEscape.Core;

namespace HeroEscape.Unity
{
    /// <summary>Mọi chuỗi hiển thị trong game (tiếng Anh), gom một chỗ để tránh trùng lặp và kiểm tra font.</summary>
    public static class UiText
    {
        public const string GameName = "Hero Escape";
        public const string WaitButton = "Wait";
        public const string RestartButton = "Restart";
        public const string NextLevelButton = "Next Level";

        public const string HintBasic =
            "Click a tile next to the hero to move (8 directions). Press Wait to stay.\nReach the exit and don't let X01 catch you.";
        public const string HintDash =
            "Dash moves the hero 2 tiles in a straight line (limited uses).\nClick a tile next to the hero to move, or press Wait to stay.";

        public const string YourTurnSuffix = "Your Turn";
        public const string EnemyTurn = "Enemy Turn";
        public const string Won = "You Win!";
        public const string Lost = "You Lose!";

        public const string WonNext = "Level complete! Press Next Level to continue.";
        public const string WonLast = "Level complete! Press Restart to play again.";
        public const string PressRestart = "Press Restart to try again.";
        public const string Restarted = "Level restarted.";
        public const string EnemyStayed = "X01 has no path and stays put.";
        public const string LevelError = "Level data error. See the Console.";

        public const string ClickHero = "That's the hero. Press Wait to stay in place.";
        public const string NotAdjacent = "You can only move to a tile next to the hero.";

        public const string DashPrompt = "Choose an adjacent tile to dash in that direction.";
        public const string DashPickAdjacent = "Pick one of the 8 tiles next to the hero to set the dash direction.";
        public const string DashCancelled = "Dash cancelled.";
        public const string NoDashUsesLeft = "No Dash uses left.";

        public static string LevelTitle(string levelName) => $"{GameName} · {levelName}";
        public static string YourTurn(int round) => $"Round {round} · {YourTurnSuffix}";
        public static string DashButton(int usesLeft, int maxUses) => $"Dash ({usesLeft}/{maxUses})";
        public static string BackToButton(string levelName) => $"Back to {levelName}";

        public static string MoveError(ActionError error)
        {
            switch (error)
            {
                case ActionError.Wall: return "Can't move there: there's a wall.";
                case ActionError.OccupiedByEnemy: return "Can't move there: X01 is on that tile.";
                case ActionError.CornerBlocked: return "Can't move diagonally: the corner is blocked.";
                case ActionError.OutOfBounds: return "Can't move there: it's off the board.";
                case ActionError.GameOver: return PressRestart;
                default: return string.Empty;
            }
        }

        public static string DashError(ActionError error, int step)
        {
            switch (error)
            {
                case ActionError.Wall: return $"Can't dash that way: a wall blocks step {step}.";
                case ActionError.OccupiedByEnemy: return $"Can't dash that way: X01 blocks step {step}.";
                case ActionError.CornerBlocked: return $"Can't dash that way: the corner is blocked on step {step}.";
                case ActionError.OutOfBounds: return $"Can't dash that way: step {step} leaves the board.";
                case ActionError.NoDashUsesLeft: return NoDashUsesLeft;
                case ActionError.DashNotAvailable: return "Dash isn't available in this level.";
                case ActionError.GameOver: return PressRestart;
                default: return string.Empty;
            }
        }

        /// <summary>Tất cả ký tự có thể hiển thị, dùng để kiểm tra font có đủ glyph.</summary>
        public static string AllDisplayedText
        {
            get
            {
                var text = new System.Text.StringBuilder();
                text.Append(LevelTitle("Level 0123456789")).Append(WaitButton).Append(RestartButton).Append(NextLevelButton)
                    .Append(HintBasic).Append(HintDash).Append(YourTurn(1234567890)).Append(EnemyTurn).Append(Won).Append(Lost)
                    .Append(WonNext).Append(WonLast).Append(PressRestart).Append(Restarted).Append(EnemyStayed).Append(LevelError)
                    .Append(ClickHero).Append(NotAdjacent).Append(DashPrompt).Append(DashPickAdjacent).Append(DashCancelled)
                    .Append(NoDashUsesLeft).Append(DashButton(2, 2)).Append(BackToButton("Level 01"));
                foreach (ActionError error in (ActionError[])System.Enum.GetValues(typeof(ActionError)))
                    text.Append(MoveError(error)).Append(DashError(error, 2));
                return text.ToString();
            }
        }
    }
}
