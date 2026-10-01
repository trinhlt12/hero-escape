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
