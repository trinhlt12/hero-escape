using System.Linq;
using HeroEscape.Core;
using HeroEscape.Unity;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class TurnInputControllerTests
    {
        static TurnInputController NewInput(int dash, params string[] rows) =>
            new TurnInputController(new GameSession(
                LevelParser.Parse($"id: INPUT\ndash: {dash}\ngrid:\n" + string.Join("\n", rows)),
                TestLevels.NewSimulation()));

        // Hero (0,0), X01 (4,0), Cửa (4,4); Tường (0,2) để có Dash bị chặn ở bước 2.
        static TurnInputController DashLevel() => NewInput(2,
            "....E",
            ".....",
            "#....",
            ".....",
            "H...X");

        [Test]
        public void DashButton_TogglesDirectionMode_AndCancelKeepsResources()
        {
            TurnInputController input = DashLevel();
            GameState before = input.Session.Current;

            Assert.AreEqual(UiText.DashPrompt, input.PressDash().Feedback);
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode);
            Assert.IsFalse(input.CanWait, "Wait bị vô hiệu hóa khi đang chọn hướng.");

            InputResponse cancel = input.PressDash();
            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(UiText.DashCancelled, cancel.Feedback);
            Assert.AreSame(before, input.Session.Current, "Hủy không mất lượt, không mất Dash.");
            Assert.IsTrue(input.CanWait);
        }

        [Test]
        public void Escape_CancelsDirectionMode()
        {
            TurnInputController input = DashLevel();
            input.PressDash();

            input.Cancel();

            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
        }

        [Test]
        public void WaitIsIgnored_WhileChoosingDirection()
        {
            TurnInputController input = DashLevel();
            input.PressDash();

            InputResponse r = input.PressWait();

            Assert.IsFalse(r.Acted);
            Assert.AreEqual(1, input.Session.Current.Round);
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode);
        }

        [Test]
        public void ClickOnUi_WhileChoosingDirection_DoesNotTouchBoard()
        {
            TurnInputController input = DashLevel();
            input.PressDash();
            GameState before = input.Session.Current;

            InputResponse r = input.PressBoard(pointerOverUi: true, cell: new GridPos(1, 1));

            Assert.IsFalse(r.Acted);
            Assert.IsNull(r.Feedback);
            Assert.AreSame(before, input.Session.Current);
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode);
        }

        [Test]
        public void ClickOnUi_InNormalMode_DoesNotMoveHero()
        {
            TurnInputController input = DashLevel();
            GameState before = input.Session.Current;

            Assert.IsFalse(input.PressBoard(pointerOverUi: true, cell: new GridPos(1, 0)).Acted);
            Assert.AreSame(before, input.Session.Current);
        }

        [Test]
        public void FarTile_OrHeroTile_KeepsDirectionMode_WithoutSpending()
        {
            TurnInputController input = DashLevel();
            input.PressDash();

            Assert.AreEqual(UiText.DashPickAdjacent, input.PressBoard(false, new GridPos(2, 2)).Feedback);
            Assert.AreEqual(UiText.DashPickAdjacent, input.PressBoard(false, new GridPos(0, 0)).Feedback);
            Assert.AreEqual(UiText.DashPickAdjacent, input.PressBoard(false, null).Feedback);
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
            Assert.AreEqual(1, input.Session.Current.Round);
        }

        [Test]
        public void InvalidDashDirection_KeepsDirectionMode_WithoutSpending()
        {
            TurnInputController input = DashLevel();
            input.PressDash();

            // Hướng N: bước 2 vào Tường (0,2).
            InputResponse r = input.PressBoard(false, new GridPos(0, 1));

            Assert.IsFalse(r.Acted);
            Assert.AreEqual(UiText.DashError(ActionError.Wall, 2), r.Feedback);
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode);
            Assert.AreEqual(new GridPos(0, 0), input.Session.Current.HeroPos);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
        }

        [Test]
        public void ValidDash_ClickedTileSetsDirection_ExitsMode_AndSpendsOne()
        {
            TurnInputController input = DashLevel();
            input.PressDash();

            // Bấm ô kề (1,1) = hướng NE; đích cuối là (2,2), không phải ô được bấm.
            InputResponse r = input.PressBoard(false, new GridPos(1, 1));

            Assert.IsTrue(r.Acted);
            Assert.AreEqual(new GridPos(2, 2), input.Session.Current.HeroPos);
            Assert.AreEqual(1, input.Session.Current.DashUsesLeft);
            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(1, r.Turn.Events.Count(e => e.Kind == RuleEventKind.EnemyMoved));
        }

        [Test]
        public void NormalClick_StillMovesOneTile()
        {
            TurnInputController input = DashLevel();

            Assert.IsTrue(input.PressBoard(false, new GridPos(1, 1)).Acted);
            Assert.AreEqual(new GridPos(1, 1), input.Session.Current.HeroPos);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
        }

        [Test]
        public void DashButton_Disabled_WhenNoUsesLeft()
        {
            TurnInputController input = NewInput(1,
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");
            input.PressDash();
            input.PressBoard(false, new GridPos(0, 1)); // Dash N, hết lượt dùng

            Assert.AreEqual(0, input.Session.Current.DashUsesLeft);
            Assert.IsFalse(input.CanPressDash);
            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(UiText.NoDashUsesLeft, input.PressDash().Feedback);
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }

        [Test]
        public void LevelWithoutDash_HasNoDashButton()
        {
            TurnInputController input = NewInput(0,
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");

            Assert.IsFalse(input.LevelHasDash);
            Assert.IsFalse(input.CanPressDash);
            Assert.IsFalse(input.PressDash().Acted);
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }

        [Test]
        public void Restart_ClearsDirectionMode_AndRestoresDash()
        {
            TurnInputController input = DashLevel();
            input.PressDash();
            input.PressBoard(false, new GridPos(1, 1)); // Dash NE
            input.PressDash();
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode);

            input.Restart();

            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(input.Session.InitialState, input.Session.Current);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
        }

        // ---- Stasis ----

        // Hero (0,0), X01 (3,0) cách 3 ô, có đường nhìn; Dash 2, Stasis 1.
        static TurnInputController StasisLevel(int stasis = 1) => NewInputWithStasis(stasis,
            "....E",
            ".....",
            ".....",
            ".....",
            "H..X.");

        static TurnInputController NewInputWithStasis(int stasis, params string[] rows) =>
            new TurnInputController(new GameSession(
                LevelParser.Parse($"id: INPUT_S\ndash: 2\nstasis: {stasis}\ngrid:\n" + string.Join("\n", rows)),
                TestLevels.NewSimulation()));

        [Test]
        public void StasisButton_EntersTargetMode_CancelKeepsCard()
        {
            TurnInputController input = StasisLevel();
            GameState before = input.Session.Current;

            Assert.AreEqual(UiText.StasisPrompt, input.PressStasis().Feedback);
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode);
            Assert.IsFalse(input.CanWait, "Wait bị vô hiệu hóa khi đang chọn mục tiêu.");

            Assert.AreEqual(UiText.StasisCancelled, input.PressStasis().Feedback);
            Assert.AreEqual(InputMode.Normal, input.Mode);

            input.PressStasis();
            Assert.AreEqual(UiText.StasisCancelled, input.Cancel().Feedback, "Escape hủy chọn Stasis.");
            Assert.AreSame(before, input.Session.Current, "Chọn rồi hủy không tiêu hao gì.");
        }

        [Test]
        public void StasisTargetMode_BoardClickOnlyTargets_NeverMovesHero()
        {
            TurnInputController input = StasisLevel();
            input.PressStasis();

            InputResponse r = input.PressBoard(false, new GridPos(1, 0)); // ô kề Hero, không có X01

            Assert.IsFalse(r.Acted);
            Assert.AreEqual(UiText.StasisError(ActionError.NoEnemyAtTarget), r.Feedback);
            Assert.AreEqual(new GridPos(0, 0), input.Session.Current.HeroPos);
            Assert.AreEqual(1, input.Session.Current.StasisUsesLeft);
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode, "Mục tiêu sai giữ chế độ chọn.");
        }

        [Test]
        public void StasisTargetMode_ClickOnUi_DoesNothing()
        {
            TurnInputController input = StasisLevel();
            input.PressStasis();
            GameState before = input.Session.Current;

            InputResponse r = input.PressBoard(pointerOverUi: true, cell: new GridPos(3, 0));

            Assert.IsNull(r.Feedback);
            Assert.AreSame(before, input.Session.Current);
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode);
        }

        [Test]
        public void ClickingX01_AppliesStasis_ThenHeroStillActs_AndEnemySkips()
        {
            TurnInputController input = StasisLevel();
            input.PressStasis();

            InputResponse applied = input.PressBoard(false, new GridPos(3, 0));

            Assert.IsFalse(applied.Acted, "Stasis không phải hành động chính.");
            Assert.AreEqual(UiText.StasisApplied, applied.Feedback);
            Assert.AreEqual(InputMode.Normal, input.Mode);
            GameState s = input.Session.Current;
            Assert.AreEqual(0, s.StasisUsesLeft);
            Assert.IsTrue(s.EnemyLocked);
            Assert.AreEqual(1, s.Round);
            Assert.IsTrue(input.CanWait, "Sau Stasis vẫn được Move/Wait/Dash.");
            Assert.IsTrue(input.CanPressDash);
            Assert.IsFalse(input.CanPressStasis, "Hết thẻ thì nút Stasis bị vô hiệu hóa.");

            InputResponse move = input.PressBoard(false, new GridPos(0, 1));
            Assert.IsTrue(move.Acted);
            Assert.AreEqual(1, move.Turn.Events.Count(e => e.Kind == RuleEventKind.EnemySkipped));
            Assert.AreEqual(new GridPos(3, 0), input.Session.Current.EnemyPos);
        }

        [Test]
        public void OutOfRangeOrBlockedTarget_GivesFeedback_AndKeepsMode()
        {
            TurnInputController far = NewInputWithStasis(1,
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");
            far.PressStasis();
            Assert.AreEqual(UiText.StasisError(ActionError.TargetOutOfRange), far.PressBoard(false, new GridPos(4, 0)).Feedback);
            Assert.AreEqual(InputMode.ChoosingStasisTarget, far.Mode);

            TurnInputController blocked = NewInputWithStasis(1,
                "....E",
                ".....",
                ".....",
                ".....",
                "H#X..");
            blocked.PressStasis();
            Assert.AreEqual(UiText.StasisError(ActionError.NoLineOfSight), blocked.PressBoard(false, new GridPos(2, 0)).Feedback);
            Assert.AreEqual(1, blocked.Session.Current.StasisUsesLeft);
        }

        [Test]
        public void DashAndStasisSelection_AreMutuallyExclusive()
        {
            TurnInputController input = StasisLevel();

            input.PressDash();
            input.PressStasis();
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode, "Chọn Stasis hủy chọn hướng Dash.");

            input.PressDash();
            Assert.AreEqual(InputMode.ChoosingDashDirection, input.Mode, "Chọn Dash hủy chọn Stasis chưa áp dụng.");
            Assert.AreEqual(1, input.Session.Current.StasisUsesLeft);
            Assert.IsFalse(input.Session.Current.EnemyLocked);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
        }

        [Test]
        public void CancellingDashAfterStasis_DoesNotUndoStasis()
        {
            TurnInputController input = StasisLevel();
            input.PressStasis();
            input.PressBoard(false, new GridPos(3, 0));

            input.PressDash();
            input.Cancel();

            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(0, input.Session.Current.StasisUsesLeft);
            Assert.IsTrue(input.Session.Current.EnemyLocked);
        }

        [Test]
        public void StasisButton_Disabled_AfterSupportUsedThisTurn_EvenWithUsesLeft()
        {
            TurnInputController input = StasisLevel(stasis: 2);
            input.PressStasis();
            input.PressBoard(false, new GridPos(3, 0));

            Assert.AreEqual(1, input.Session.Current.StasisUsesLeft);
            Assert.IsFalse(input.CanPressStasis);
            Assert.AreEqual(UiText.SupportAlreadyUsed, input.PressStasis().Feedback);
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }

        [Test]
        public void LevelWithoutStasis_HasNoStasisButton()
        {
            TurnInputController input = DashLevel();

            Assert.IsFalse(input.LevelHasStasis);
            Assert.IsFalse(input.CanPressStasis);
            Assert.IsNull(input.PressStasis().Feedback);
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }

        [Test]
        public void Restart_ClearsStasisModeLockAndResources()
        {
            TurnInputController input = StasisLevel();
            input.PressStasis();
            input.PressBoard(false, new GridPos(3, 0));
            input.PressDash();

            input.Restart();

            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual(input.Session.InitialState, input.Session.Current);
            Assert.AreEqual(1, input.Session.Current.StasisUsesLeft);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
            Assert.IsFalse(input.Session.Current.EnemyLocked);
            Assert.IsFalse(input.Session.Current.SupportUsedThisTurn);

            input.PressStasis();
            input.Restart();
            Assert.AreEqual(InputMode.Normal, input.Mode, "Restart xóa chế độ chọn Stasis.");
        }

        [TestCase(2, 0, "X2")] // bấm X2 ở (2,0)
        [TestCase(0, 2, "X1")] // bấm X1 ở (0,2)
        public void TwoEnemies_ClickedEnemyIsTheOnlyOneLocked(int x, int y, string expectedId)
        {
            TurnInputController input = NewInputWithStasis(1,
                "....E",
                ".....",
                "1....",
                ".....",
                "H.2..");
            input.PressStasis();

            InputResponse r = input.PressBoard(false, new GridPos(x, y));

            Assert.AreEqual(UiText.StasisApplied, r.Feedback);
            foreach (EnemyState enemy in input.Session.Current.Enemies)
                Assert.AreEqual(enemy.Id == expectedId, enemy.Locked, enemy.Id);
        }

        [Test]
        public void TwoEnemies_ClickOnEmptyCell_KeepsTargetMode()
        {
            TurnInputController input = NewInputWithStasis(1,
                "....E",
                ".....",
                "1....",
                ".....",
                "H.2..");
            input.PressStasis();

            InputResponse r = input.PressBoard(false, new GridPos(1, 1));

            Assert.AreEqual(UiText.StasisError(ActionError.NoEnemyAtTarget), r.Feedback);
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode);
            Assert.IsTrue(input.Session.Current.Enemies.All(e => !e.Locked));
            Assert.AreEqual(1, input.Session.Current.StasisUsesLeft);
        }

        [Test]
        public void EnteringLevel_CreatesFreshStateAndInput()
        {
            // GameController tạo GameSession + TurnInputController mới mỗi lần vào màn.
            TurnInputController first = DashLevel();
            first.PressDash();
            first.PressBoard(false, new GridPos(1, 1));
            first.PressDash();

            TurnInputController reentered = DashLevel();

            Assert.AreEqual(InputMode.Normal, reentered.Mode);
            Assert.IsFalse(reentered.Busy);
            Assert.AreEqual(reentered.Session.InitialState, reentered.Session.Current);
            Assert.AreEqual(2, reentered.Session.Current.DashUsesLeft);
            Assert.AreEqual(1, reentered.Session.Current.Round);
        }

        [Test]
        public void Busy_BlocksBoardWaitAndDash()
        {
            TurnInputController input = DashLevel();
            input.Busy = true;

            Assert.IsFalse(input.PressBoard(false, new GridPos(1, 1)).Acted);
            Assert.IsFalse(input.PressWait().Acted);
            Assert.IsFalse(input.PressDash().Acted);
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }

        [Test]
        public void GameOver_BlocksMoveWaitAndDash()
        {
            TurnInputController input = NewInput(2,
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X...");
            input.PressWait(); // X01 bắt Hero

            Assert.AreEqual(GameOutcome.Lost, input.Session.Current.Outcome);
            Assert.IsFalse(input.CanWait);
            Assert.IsFalse(input.CanPressDash);
            Assert.IsFalse(input.PressDash().Acted);
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }
    }
}
