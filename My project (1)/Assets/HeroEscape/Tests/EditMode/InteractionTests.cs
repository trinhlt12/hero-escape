using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Unity;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>Highlight lựa chọn hợp lệ, hover preview và click thực hiện ngay (không Confirm/Cancel/Undo).</summary>
    public class InteractionTests
    {
        static LevelData Parse(int dash, int stasis, params string[] rows) =>
            LevelParser.Parse($"id: UI\ndash: {dash}\nstasis: {stasis}\ngrid:\n" + string.Join("\n", rows));

        static TurnInputController NewInput(LevelData level) => new TurnInputController(new GameSession(level, TestLevels.NewSimulation()));

        static LevelData LoadLevel(string path) => LevelParser.Parse(File.ReadAllText(path));

        static readonly string[] LevelPaths =
        {
            TestLevels.TestLevelPath, Level02Tests.Level02Path, Level03Tests.Level03Path, Level04Tests.Level04Path,
        };

        /// <summary>Chơi một lượt bằng thao tác người chơi: (Stasis: bấm X) rồi bấm ô đích Move/Dash hoặc bấm Wait.</summary>
        static void PlayTurnByClicks(TurnInputController input, PlannedTurn turn)
        {
            if (turn.UseStasis)
            {
                string id = turn.ResolveTarget(input.Session.Current);
                Assert.AreEqual(UiText.StasisPrompt, input.PressStasis().Feedback);
                GridPos target = input.Session.Current.Enemies[input.Session.Current.IndexOfEnemy(id)].Pos;
                Assert.AreEqual(UiText.StasisApplied, input.PressBoard(false, target).Feedback, $"Stasis lên {id}");
            }

            if (turn.Main.Kind == HeroActionKind.Wait)
            {
                Assert.IsTrue(input.PressWait().Acted);
                return;
            }

            Assert.IsTrue(input.Session.TryGetDestination(turn.Main, out GridPos destination, out ActionError error, out _), $"{turn.Main}: {error}");
            if (turn.Main.Kind == HeroActionKind.Dash) input.PressDash();
            CollectionAssert.Contains(input.HighlightedCells, destination, $"{turn.Main} phải được highlight.");
            Assert.IsTrue(input.PressBoard(false, destination).Acted, $"{turn.Main} không thực hiện được.");
        }

        // ---------------- Highlight ----------------

        [Test]
        public void MoveHighlights_RespectBoundsWallsEnemiesAndCorners()
        {
            TurnInputController input = NewInput(Parse(0, 0,
                "....E",
                ".....",
                "..#..",
                ".HX..",
                "#...."));

            // Hero (1,1): NE là Tường, E là X01, SE bị X01 chặn góc, SW là Tường; NW hợp lệ vì hai ô cạnh trống.
            CollectionAssert.AreEquivalent(
                new[] { new GridPos(1, 2), new GridPos(1, 0), new GridPos(0, 1), new GridPos(0, 2) },
                input.HighlightedCells);
            CollectionAssert.DoesNotContain(input.HighlightedCells, input.Session.Current.HeroPos);
        }

        [Test]
        public void MoveHighlights_DoNotLeaveTheBoard_AndIncludeLosingMoves()
        {
            TurnInputController input = NewInput(Parse(0, 0,
                "....E",
                ".....",
                ".....",
                "..X..",
                "H...."));

            // Đi E tới (1,0) sẽ bị X01 bắt, nhưng vẫn là nước hợp lệ nên vẫn được highlight.
            CollectionAssert.AreEquivalent(new[] { new GridPos(0, 1), new GridPos(1, 1), new GridPos(1, 0) }, input.HighlightedCells);
            Assert.IsTrue(input.PreviewBoardHover(new GridPos(1, 0)).WillLose);
        }

        [Test]
        public void DashHighlights_AreOnlyLandingTiles()
        {
            TurnInputController input = NewInput(Parse(2, 0,
                "....E",
                ".....",
                "#....",
                ".....",
                "H...X"));
            input.PressDash();

            // N bị Tường ở bước 2; NE → (2,2); E → (2,0). Ô kề không bao giờ là đích Dash.
            CollectionAssert.AreEquivalent(new[] { new GridPos(2, 2), new GridPos(2, 0) }, input.HighlightedCells);
        }

        [Test]
        public void DashHighlights_RespectEnemiesAndCornersOnBothSteps()
        {
            TurnInputController input = NewInput(Parse(2, 0,
                "....E",
                ".....",
                ".X...",
                ".....",
                "H.#.."));
            input.PressDash();

            // N: (0,2) hợp lệ. NE: bước 2 (1,1)→(2,2) bị X01 (1,2) chặn góc. E: bước 2 vào Tường (2,0).
            CollectionAssert.AreEquivalent(new[] { new GridPos(0, 2) }, input.HighlightedCells);
        }

        [Test]
        public void StasisHighlights_OnlyValidTargets_ByRangeAndLineOfSight()
        {
            TurnInputController input = NewInput(Parse(2, 1,
                "2...E",
                ".....",
                ".....",
                ".....",
                "H.1.."));
            input.PressStasis();

            // X1 (2,0): tầm 2, có đường nhìn. X2 (0,4): tầm 4, ngoài tầm.
            CollectionAssert.AreEquivalent(new[] { new GridPos(2, 0) }, input.HighlightedCells);

            TurnInputController blocked = NewInput(Parse(2, 1,
                "....E",
                ".....",
                "2....",
                ".....",
                "H#1.."));
            blocked.PressStasis();

            // X1 bị Tường (1,0) chặn đường nhìn; X2 (0,2) tầm 2, có đường nhìn.
            CollectionAssert.AreEquivalent(new[] { new GridPos(0, 2) }, blocked.HighlightedCells);
        }

        [Test]
        public void ModeChanges_SwapHighlightSets_WithoutSpending()
        {
            TurnInputController input = NewInput(Parse(2, 1,
                "....E",
                ".....",
                ".....",
                ".....",
                "H..1."));
            GameState before = input.Session.Current;
            IReadOnlyList<GridPos> moves = input.HighlightedCells.ToList();

            input.PressDash();
            IReadOnlyList<GridPos> dashes = input.HighlightedCells.ToList();
            input.PressStasis();
            IReadOnlyList<GridPos> targets = input.HighlightedCells.ToList();
            input.Cancel();

            CollectionAssert.AreNotEquivalent(moves, dashes);
            CollectionAssert.AreEquivalent(new[] { new GridPos(3, 0) }, targets);
            CollectionAssert.AreEquivalent(moves, input.HighlightedCells, "Escape trở về Move và highlight Move.");
            Assert.AreSame(before, input.Session.Current, "Đổi chế độ không tiêu hao gì.");
        }

        [Test]
        public void NoHighlights_DuringEnemyPhase_OrAfterGameOver()
        {
            TurnInputController input = NewInput(Parse(0, 0,
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X..."));
            input.Busy = true;
            Assert.IsEmpty(input.HighlightedCells);
            Assert.AreEqual(PreviewKind.None, input.PreviewBoardHover(new GridPos(1, 2)).Kind);
            input.Busy = false;

            input.PressWait(); // X01 bắt Hero
            Assert.AreEqual(GameOutcome.Lost, input.Session.Current.Outcome);
            Assert.IsEmpty(input.HighlightedCells);
            Assert.AreEqual(PreviewKind.None, input.PreviewWaitHover().Kind);
        }

        [Test]
        public void AfterStasis_BackToMove_HighlightsFromLockedState()
        {
            TurnInputController input = NewInput(Parse(2, 1,
                "....E",
                ".....",
                ".....",
                ".....",
                "H..1."));
            input.PressStasis();
            input.PressBoard(false, new GridPos(3, 0));

            Assert.AreEqual(InputMode.Normal, input.Mode);
            CollectionAssert.AreEquivalent(input.Session.LegalTargets(HeroActionKind.Move).Select(t => t.destination), input.HighlightedCells);
        }

        // ---------------- Hover preview ----------------

        [Test]
        public void Hovering_NeverChangesRealState()
        {
            LevelData level = LoadLevel(Level04Tests.Level04Path);
            TurnInputController input = NewInput(level);
            GameState before = input.Session.Current;
            int revision = input.Session.Revision;

            foreach (InputMode mode in new[] { InputMode.Normal, InputMode.ChoosingDashTarget, InputMode.ChoosingStasisTarget })
            {
                if (mode == InputMode.ChoosingDashTarget) input.PressDash();
                if (mode == InputMode.ChoosingStasisTarget) input.PressStasis();
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    for (int x = -1; x <= level.Width; x++)
                    for (int y = -1; y <= level.Height; y++)
                        input.PreviewBoardHover(new GridPos(x, y));
                    input.PreviewBoardHover(null);
                    input.PreviewWaitHover();
                }
            }

            Assert.AreSame(before, input.Session.Current);
            Assert.AreEqual(GameState.CreateInitial(level), input.Session.Current);
            Assert.AreEqual(revision, input.Session.Revision);
            Assert.AreEqual(2, input.Session.Current.DashUsesLeft);
            Assert.AreEqual(1, input.Session.Current.StasisUsesLeft);
            Assert.IsTrue(input.Session.Current.Enemies.All(e => !e.Locked));
        }

        [TestCase(0)] // Level 01: một X, Move
        [TestCase(1)] // Level 02: một X, Move/Dash
        [TestCase(3)] // Level 04: hai X, Move/Dash
        public void Preview_MatchesExecution_ForEveryHighlightedMoveAndDash_AndWait(int levelIndex)
        {
            LevelData level = LoadLevel(LevelPaths[levelIndex]);
            var probe = NewInput(level);

            foreach (InputMode mode in new[] { InputMode.Normal, InputMode.ChoosingDashTarget })
            {
                if (mode == InputMode.ChoosingDashTarget && !probe.LevelHasDash) continue;
                if (mode == InputMode.ChoosingDashTarget) probe.PressDash();

                foreach (GridPos cell in probe.HighlightedCells.ToList())
                {
                    TurnPreview preview = probe.PreviewBoardHover(cell);
                    Assert.AreEqual(PreviewKind.MainAction, preview.Kind);

                    var executor = NewInput(level);
                    if (mode == InputMode.ChoosingDashTarget) executor.PressDash();
                    InputResponse done = executor.PressBoard(false, cell);

                    Assert.IsTrue(done.Acted);
                    Assert.AreEqual(done.Turn.NextState, preview.Turn.NextState, $"{mode} {cell}");
                    CollectionAssert.AreEqual(done.Turn.Events, preview.Turn.Events, $"{mode} {cell}");
                }

                probe.Cancel();
            }

            TurnPreview wait = probe.PreviewWaitHover();
            var waiter = NewInput(level);
            Assert.AreEqual(waiter.PressWait().Turn.NextState, wait.Turn.NextState);
        }

        [Test]
        public void PreviewAfterStasis_SkipsExactlyTheLockedEnemy_AndMatchesExecution()
        {
            LevelData level = LoadLevel(Level04Tests.Level04Path);
            TurnInputController input = NewInput(level);
            input.PressBoard(false, new GridPos(0, 1)); // N
            input.PressStasis();
            input.PressBoard(false, new GridPos(0, 4)); // khóa X2
            input.PressDash();

            TurnPreview preview = input.PreviewBoardHover(new GridPos(0, 3)); // DASH-N

            GameState predicted = preview.Turn.NextState;
            Assert.AreEqual(GameOutcome.Playing, predicted.Outcome);
            Assert.AreEqual(new GridPos(0, 4), predicted.Enemies[1].Pos, "X2 bị khóa đứng yên.");
            Assert.AreEqual(0, predicted.Enemies[1].LockPhases, "Khóa hết sau đúng một pha.");
            Assert.AreNotEqual(input.Session.Current.Enemies[0].Pos, predicted.Enemies[0].Pos, "X1 vẫn hành động.");
            Assert.IsTrue(input.Session.Current.Enemies[1].Locked, "Preview không làm hết khóa thật.");

            Assert.AreEqual(predicted, input.PressBoard(false, new GridPos(0, 3)).Turn.NextState);
        }

        [Test]
        public void Preview_StopsAtWin_NoEnemySimulated()
        {
            TurnInputController input = NewInput(Parse(0, 0,
                ".....",
                ".....",
                "..E..",
                ".2H..",
                "...1."));

            TurnPreview preview = input.PreviewBoardHover(new GridPos(2, 2));

            Assert.IsTrue(preview.WillWin);
            Assert.IsFalse(preview.Turn.Events.Any(e => e.EnemyId != null));
        }

        [Test]
        public void Preview_StopsWhenFirstEnemyCatches_SecondNotSimulated()
        {
            LevelData level = LoadLevel(Level04Tests.Level04Path);
            TurnInputController input = NewInput(level);
            input.PressBoard(false, new GridPos(0, 1)); // N
            input.PressStasis();
            input.PressBoard(false, new GridPos(0, 4)); // khóa X2
            input.PressDash();

            TurnPreview preview = input.PreviewBoardHover(new GridPos(2, 1)); // DASH-E: X1 bắt Hero

            Assert.IsTrue(preview.WillLose);
            CollectionAssert.AreEqual(new[] { "X1" }, preview.Turn.Events.Where(e => e.EnemyId != null).Select(e => e.EnemyId));
        }

        [Test]
        public void StasisHover_PreviewsTarget_WithoutSimulatingEnemyPhase()
        {
            LevelData level = LoadLevel(Level04Tests.Level04Path);
            TurnInputController input = NewInput(level);
            input.PressBoard(false, new GridPos(0, 1)); // N: cả hai X trong tầm
            input.PressStasis();

            TurnPreview onX2 = input.PreviewBoardHover(new GridPos(0, 4));
            TurnPreview onEmpty = input.PreviewBoardHover(new GridPos(1, 1));

            Assert.AreEqual(PreviewKind.StasisTarget, onX2.Kind);
            Assert.AreEqual(1, onX2.StasisEnemyIndex);
            Assert.IsNull(onX2.Turn);
            Assert.AreEqual(PreviewKind.None, onEmpty.Kind);
            Assert.IsTrue(input.Session.Current.Enemies.All(e => !e.Locked), "Hover không áp thẻ.");
        }

        [Test]
        public void PreviewCache_NoRecomputeWhenNothingChanges_RecomputesWhenStateChangesUnderSameCell()
        {
            TurnInputController input = NewInput(Parse(0, 0,
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X"));
            var cell = new GridPos(0, 1);

            input.PreviewBoardHover(cell);
            int evaluations = input.PreviewEvaluations;
            for (int i = 0; i < 10; i++) input.PreviewBoardHover(cell);
            Assert.AreEqual(evaluations, input.PreviewEvaluations, "Cùng revision, chế độ và ô: không mô phỏng lại.");

            input.PressBoard(false, new GridPos(1, 0)); // trạng thái đổi, chuột vẫn ở (0,1)
            TurnPreview updated = input.PreviewBoardHover(cell);
            Assert.Greater(input.PreviewEvaluations, evaluations, "Trạng thái đổi: preview được tính lại.");
            Assert.AreEqual(input.Session.Current.HeroPos + new GridPos(-1, 1), cell);
            Assert.AreEqual(PreviewKind.MainAction, updated.Kind);
        }

        [Test]
        public void Restart_RebuildsHighlightsFromInitialState()
        {
            TurnInputController input = NewInput(LoadLevel(Level04Tests.Level04Path));
            IReadOnlyList<GridPos> initial = input.HighlightedCells.ToList();
            input.PressBoard(false, new GridPos(0, 1));
            input.PressDash();

            input.Restart();

            Assert.AreEqual(InputMode.Normal, input.Mode);
            CollectionAssert.AreEquivalent(initial, input.HighlightedCells);
        }

        [Test]
        public void MoveClick_ExecutesExactlyOnce()
        {
            TurnInputController input = NewInput(Parse(0, 0,
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X"));

            Assert.IsTrue(input.PressBoard(false, new GridPos(1, 1)).Acted);

            Assert.AreEqual(1, input.Session.Revision);
            Assert.AreEqual(new GridPos(1, 1), input.Session.Current.HeroPos);
        }

        // ---------------- Hồi quy: Level 01–04 chơi được bằng thao tác mới ----------------

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ReferenceSolutions_WinByClickingHighlightedTiles(int levelIndex)
        {
            LevelData level = LoadLevel(LevelPaths[levelIndex]);
            TurnInputController input = NewInput(level);

            foreach (PlannedTurn turn in LevelReachability.ReferenceTurns(level))
            {
                Assert.AreEqual(GameOutcome.Playing, input.Session.Current.Outcome);
                PlayTurnByClicks(input, turn);
            }

            Assert.AreEqual(GameOutcome.Won, input.Session.Current.Outcome);
            Assert.AreEqual(LevelReachability.Replay(GameState.CreateInitial(level), TestLevels.NewSimulation(),
                LevelReachability.ReferenceTurns(level)), input.Session.Current, "Cùng kết quả với chuỗi đã kiểm chứng.");
        }
    }
}
