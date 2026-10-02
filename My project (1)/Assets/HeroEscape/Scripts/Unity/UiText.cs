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
        public const string LevelSelectButton = "Level Select";
        public const string SelectLevelTitle = "Select Level";

        public const string HintBasic =
            "Click a highlighted tile to move. Hover it to preview the enemy turn.\nPress Wait to stay. Reach the exit and don't let X01 catch you.";
        public const string HintDash =
            "Tap the Dash card, then a highlighted tile, to move 2 tiles in a straight line.\nClick a tile next to the hero to move, or press Wait to stay.";

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
        public const string NotAdjacent = "You can only move to a highlighted tile next to the hero.";

        public const string DashPrompt = "Click a highlighted tile to dash there (2 tiles in a straight line).";
        public const string DashPickTarget = "Click a highlighted landing tile to dash.";

        // Dự đoán khi hover (chỉ hiển thị).
        public const string WillBeCaught = "You will be caught";
        public const string WillEscape = "You will escape";
        public const string WillBeFrozen = "Will be frozen";
        public const string DashCancelled = "Dash cancelled.";
        public const string NoDashUsesLeft = "No Dash uses left.";

        public const string StasisDescription = "Skip this enemy's next turn. You can still move or dash.";
        public const string HintStasis =
            "Stasis: " + StasisDescription + "\nDash moves 2 tiles in a straight line. Wait stays in place.";
        public const string StasisPrompt = "Select an enemy within 3 tiles.";
        public const string StasisCancelled = "Stasis cancelled.";
        public const string StasisApplied = "X01 is frozen. Now move, wait or dash.";
        public const string NoStasisUsesLeft = "No Stasis uses left.";
        public const string SupportAlreadyUsed = "You already used a support card this turn.";
        public const string EnemySkipped = "X01 was frozen and skipped its turn.";
        public const string FrozenLabel = "Frozen";

        public const string EnemyOrderHint = "Enemies act in order: 1, then 2.";
        public const string HintMultiEnemy =
            EnemyOrderHint + "\nStasis freezes only the enemy you click. Dash moves 2 tiles in a straight line.";

        /// <summary>Nhãn số thứ tự hành động đặt cạnh X01 (1 = hành động trước).</summary>
        public static string EnemyOrder(int order) => order.ToString();

        public static string LevelTitle(string levelName) => $"{GameName} · {levelName}";
        public const string TacticalLabel = "Tactical";
        public static string LevelTitleTactical(string levelName) => $"{GameName} · {levelName} · {TacticalLabel}";
        public const string HintTactical =
            "Tactical: X01 looks one turn ahead and tries to cut off your escape.\nDash moves 2 tiles. Stasis freezes the enemy you click.";
        public static string YourTurn(int round) => $"Round {round} · {YourTurnSuffix}";
        public static string BackToButton(string levelName) => $"Back to {levelName}";

        // Tay bài. Mô tả đúng luật hiện có; không hứa hẹn mục tiêu hợp lệ là an toàn.
        public const string HeroCardTag = "HERO";
        public const string SupportCardTag = "SUPPORT";
        public const string DashCardName = "Dash";
        public const string StasisCardName = "Stasis";
        public const string DashCardDescription = "Move 2 tiles in a straight line. Walls, enemies and blocked corners stop it. Uses your move.";
        public const string StasisCardDescription = "Freeze an enemy within 3 tiles in line of sight. It skips its next turn. You still move after.";
        public const string BarrierCardName = "Barrier";
        public const string BarrierCardDescription = "Block an adjacent empty tile until the next enemy phase ends.";

        // Barrier: chọn ô, phản hồi và nhãn thời hạn (số pha địch còn lại, không phải thời gian thực).
        public const string BarrierPrompt = "Select an empty tile next to the hero.";
        public const string BarrierCancelled = "Barrier cancelled.";
        public const string BarrierPlaced = "Barrier placed until the enemy turn ends. Now move, wait or dash.";
        public const string NoBarrierUsesLeft = "No Barrier uses left.";
        public const string HintBarrier =
            "Barrier blocks a tile next to the hero for you and X01 until the enemy turn ends.\nOne support card per turn. Dash moves 2 tiles. Stasis freezes X01.";
        public static string BarrierPhasesLabel(int phases) => phases.ToString();

        public static string BarrierError(ActionError error)
        {
            switch (error)
            {
                case ActionError.TargetNotAdjacent: return "Barrier must go on a tile next to the hero.";
                case ActionError.OutOfBounds: return "That tile is off the board.";
                case ActionError.Wall: return "Can't place a barrier on a wall.";
                case ActionError.TargetIsExit: return "Can't place a barrier on the exit.";
                case ActionError.OccupiedByEnemy: return "Can't place a barrier on X01.";
                case ActionError.Barrier: return "There's already a barrier there.";
                case ActionError.CornerBlocked: return "Can't place it diagonally: the corner is blocked.";
                case ActionError.NoBarrierUsesLeft: return NoBarrierUsesLeft;
                case ActionError.SupportAlreadyUsedThisTurn: return SupportAlreadyUsed;
                case ActionError.BarrierNotAvailable: return "Barrier isn't available in this level.";
                case ActionError.GameOver: return PressRestart;
                default: return string.Empty;
            }
        }
        public static string CardUsesLeft(int usesLeft) => usesLeft == 1 ? "1 use left" : $"{usesLeft} uses left";

        public static string StasisError(ActionError error)
        {
            switch (error)
            {
                case ActionError.NoEnemyAtTarget: return "Stasis needs a target. Click X01.";
                case ActionError.TargetOutOfRange: return "X01 is too far. Stasis reaches 3 tiles.";
                case ActionError.NoLineOfSight: return "A wall blocks the line of sight to X01.";
                case ActionError.TargetAlreadyLocked: return "X01 is already frozen.";
                case ActionError.NoStasisUsesLeft: return NoStasisUsesLeft;
                case ActionError.SupportAlreadyUsedThisTurn: return SupportAlreadyUsed;
                case ActionError.StasisNotAvailable: return "Stasis isn't available in this level.";
                case ActionError.GameOver: return PressRestart;
                default: return string.Empty;
            }
        }

        public static string MoveError(ActionError error)
        {
            switch (error)
            {
                case ActionError.Wall: return "Can't move there: there's a wall.";
                case ActionError.Barrier: return "Can't move there: there's a barrier.";
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
                case ActionError.Barrier: return $"Can't dash that way: a barrier blocks step {step}.";
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
                text.Append(LevelTitle("Level 0123456789")).Append(WaitButton).Append(RestartButton).Append(NextLevelButton).Append(LevelSelectButton).Append(SelectLevelTitle)
                    .Append(HintBasic).Append(HintDash).Append(YourTurn(1234567890)).Append(EnemyTurn).Append(Won).Append(Lost)
                    .Append(WonNext).Append(WonLast).Append(PressRestart).Append(Restarted).Append(EnemyStayed).Append(LevelError)
                    .Append(ClickHero).Append(NotAdjacent).Append(DashPrompt).Append(DashPickTarget).Append(WillBeCaught).Append(WillEscape).Append(WillBeFrozen).Append(DashCancelled)
                    .Append(NoDashUsesLeft).Append(BackToButton("Level 01"))
                    .Append(HintStasis).Append(StasisPrompt).Append(StasisCancelled).Append(StasisApplied).Append(NoStasisUsesLeft)
                    .Append(SupportAlreadyUsed).Append(EnemySkipped).Append(FrozenLabel)
                    .Append(HintMultiEnemy).Append(LevelTitleTactical("Level 05")).Append(HintTactical).Append(EnemyOrder(1234567890))
                    .Append(BarrierCardName).Append(BarrierCardDescription).Append(BarrierPrompt).Append(BarrierCancelled).Append(BarrierPlaced)
                    .Append(NoBarrierUsesLeft).Append(HintBarrier).Append(BarrierPhasesLabel(1234567890))
                    .Append(HeroCardTag).Append(SupportCardTag).Append(DashCardName).Append(StasisCardName)
                    .Append(DashCardDescription).Append(StasisCardDescription).Append(CardUsesLeft(1)).Append(CardUsesLeft(23456789));
                foreach (ActionError error in (ActionError[])System.Enum.GetValues(typeof(ActionError)))
                    text.Append(MoveError(error)).Append(DashError(error, 2)).Append(StasisError(error)).Append(BarrierError(error));
                return text.ToString();
            }
        }
    }
}
