using System.Linq;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class StasisTests
    {
        /// <summary>Lưới 5x5 có Dash 2, Stasis <paramref name="stasis"/>. Hàng đầu là hàng trên cùng (y = 4).</summary>
        static GameState Start(int stasis, params string[] rows) =>
            GameState.CreateInitial(LevelParser.Parse($"id: STASIS_TEST\ndash: 2\nstasis: {stasis}\ngrid:\n" + string.Join("\n", rows)));

        static GameState Start(params string[] rows) => Start(1, rows);

        static readonly Simulation Sim = TestLevels.NewSimulation();

        static GameState ApplyOk(GameState s)
        {
            SupportResult r = Sim.ApplyStasis(s, s.EnemyPos);
            Assert.IsTrue(r.Accepted, $"Stasis bị từ chối: {r.Error}");
            return r.NextState;
        }

        static void AssertRejectedUnchanged(GameState s, SupportResult r, ActionError error)
        {
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(error, r.Error);
            Assert.AreSame(s, r.NextState, "Stasis sai không được đổi trạng thái hay tiêu hao thẻ.");
        }

        [Test]
        public void InRange3_WithLineOfSight_Applies_WithoutEndingTurn()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H..X.");

            GameState after = ApplyOk(s);

            Assert.AreEqual(0, after.StasisUsesLeft);
            Assert.IsTrue(after.SupportUsedThisTurn);
            Assert.AreEqual(Simulation.StasisLockPhases, after.EnemyLockPhases);
            Assert.AreEqual(s.HeroPos, after.HeroPos);
            Assert.AreEqual(s.EnemyPos, after.EnemyPos, "Stasis không được chạy X01.");
            Assert.AreEqual(s.Round, after.Round, "Stasis không kết thúc lượt Hero.");
            Assert.AreEqual(s.DashUsesLeft, after.DashUsesLeft);
            Assert.AreEqual(GameOutcome.Playing, after.Outcome);
        }

        [Test]
        public void DiagonalRange3_IsInRange()
        {
            GameState s = Start(
                "....E",
                "...X.",
                ".....",
                ".....",
                "H....");

            Assert.IsTrue(Sim.ApplyStasis(s, s.EnemyPos).Accepted);
        }

        [Test]
        public void Range4_IsRejected()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");

            AssertRejectedUnchanged(s, Sim.ApplyStasis(s, s.EnemyPos), ActionError.TargetOutOfRange);
        }

        [Test]
        public void WallBetween_BlocksLineOfSight()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H#X..");

            AssertRejectedUnchanged(s, Sim.ApplyStasis(s, s.EnemyPos), ActionError.NoLineOfSight);
        }

        [TestCase(".....", "H#...")] // ô cạnh (1,0) tại góc đầu tiên là Tường
        [TestCase("#....", "H....")] // ô cạnh (0,1) tại góc đầu tiên là Tường
        public void LineThroughCorner_IsBlocked_IfEitherSideCellIsWall(string row1, string row0)
        {
            // Tia (0,0) -> (2,2) đi đúng qua góc chung của (0,0), (1,0), (0,1), (1,1).
            GameState s = Start(
                "....E",
                ".....",
                "..X..",
                row1,
                row0);

            AssertRejectedUnchanged(s, Sim.ApplyStasis(s, s.EnemyPos), ActionError.NoLineOfSight);
        }

        [Test]
        public void LineThroughCorner_IsClear_WhenBothSideCellsAreOpen()
        {
            GameState s = Start(
                "....E",
                ".....",
                "..X..",
                ".....",
                "H....");

            Assert.IsTrue(Sim.ApplyStasis(s, s.EnemyPos).Accepted);
            CollectionAssert.IsSubsetOf(
                new[] { new GridPos(1, 0), new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(1, 2) },
                GridRules.SupercoverCells(new GridPos(0, 0), new GridPos(2, 2)),
                "Supercover phải gồm cả hai ô cạnh tại mỗi góc.");
        }

        [Test]
        public void ExitDoesNotBlockLineOfSight()
        {
            GameState s = Start(
                ".....",
                ".....",
                ".....",
                ".....",
                "HEX..");

            Assert.IsTrue(Sim.ApplyStasis(s, s.EnemyPos).Accepted);
        }

        [Test]
        public void WrongTarget_IsRejected()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H.X..");

            AssertRejectedUnchanged(s, Sim.ApplyStasis(s, new GridPos(1, 0)), ActionError.NoEnemyAtTarget);
            AssertRejectedUnchanged(s, Sim.ApplyStasis(s, s.HeroPos), ActionError.NoEnemyAtTarget);
        }

        [Test]
        public void CannotUseTwiceInOneTurn_EvenWithUsesLeft_AndLockedTargetStaysLocked()
        {
            GameState s = Start(2,
                "....E",
                ".....",
                ".....",
                ".....",
                "H.X..");
            GameState once = ApplyOk(s);

            SupportResult again = Sim.ApplyStasis(once, once.EnemyPos);

            AssertRejectedUnchanged(once, again, ActionError.SupportAlreadyUsedThisTurn);
            Assert.AreEqual(1, once.StasisUsesLeft);
            Assert.AreEqual(1, once.EnemyLockPhases);
        }

        [Test]
        public void CannotUseAfterUsesRunOut_EvenCallingCoreDirectly()
        {
            GameState afterStasis = ApplyOk(Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H..X."));
            GameState nextTurn = Sim.Resolve(afterStasis, HeroAction.Wait).NextState;

            Assert.AreEqual(0, nextTurn.StasisUsesLeft);
            Assert.IsFalse(nextTurn.SupportUsedThisTurn);
            AssertRejectedUnchanged(nextTurn, Sim.ApplyStasis(nextTurn, nextTurn.EnemyPos), ActionError.NoStasisUsesLeft);
        }

        [TestCase(HeroActionKind.Move)]
        [TestCase(HeroActionKind.Wait)]
        [TestCase(HeroActionKind.Dash)]
        public void StasisThenMainAction_EnemySkipsExactlyOnePhase_ThenActsAgain(HeroActionKind kind)
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H..X.");
            HeroAction main = kind == HeroActionKind.Move ? HeroAction.Move(Direction.N)
                : kind == HeroActionKind.Dash ? HeroAction.Dash(Direction.N) : HeroAction.Wait;

            TurnResult first = Sim.Resolve(ApplyOk(s), main);

            Assert.IsTrue(first.Accepted);
            Assert.AreEqual(s.EnemyPos, first.NextState.EnemyPos, "X01 bị khóa không được di chuyển.");
            Assert.AreEqual(1, first.Events.Count(e => e.Kind == RuleEventKind.EnemySkipped));
            Assert.AreEqual(0, first.Events.Count(e => e.Kind == RuleEventKind.EnemyMoved));
            Assert.AreEqual(0, first.NextState.EnemyLockPhases, "Khóa giảm từ 1 về 0 sau pha bị bỏ qua.");
            Assert.IsFalse(first.NextState.SupportUsedThisTurn, "Lượt Hero mới xóa cờ đã dùng phụ trợ.");
            Assert.AreEqual(s.Round + 1, first.NextState.Round);

            TurnResult second = Sim.Resolve(first.NextState, HeroAction.Wait);
            Assert.AreEqual(1, second.Events.Count(e => e.Kind == RuleEventKind.EnemyMoved), "Pha sau X01 hoạt động bình thường.");
        }

        [Test]
        public void InvalidMainActionAfterStasis_KeepsStasisAndLock()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                "#....",
                "H..X.");
            GameState afterStasis = ApplyOk(s);

            TurnResult badMove = Sim.Resolve(afterStasis, HeroAction.Move(Direction.N));
            TurnResult badDash = Sim.Resolve(afterStasis, HeroAction.Dash(Direction.W));

            Assert.IsFalse(badMove.Accepted);
            Assert.IsFalse(badDash.Accepted);
            Assert.AreSame(afterStasis, badMove.NextState);
            Assert.AreEqual(1, afterStasis.EnemyLockPhases, "Hành động sai không làm hết khóa.");
            Assert.AreEqual(0, afterStasis.StasisUsesLeft, "Không hoàn thẻ.");
            Assert.IsTrue(afterStasis.SupportUsedThisTurn, "Không kết thúc lượt.");

            TurnResult good = Sim.Resolve(afterStasis, HeroAction.Move(Direction.E));
            Assert.AreEqual(1, good.Events.Count(e => e.Kind == RuleEventKind.EnemySkipped));
        }

        [Test]
        public void LockedEnemy_StillBlocksMoveDashAndCorners()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".X...",
                "H....");
            GameState locked = ApplyOk(s);

            Assert.AreEqual(ActionError.OccupiedByEnemy, Sim.Resolve(locked, HeroAction.Move(Direction.NE)).Error);
            Assert.AreEqual(ActionError.OccupiedByEnemy, Sim.Resolve(locked, HeroAction.Dash(Direction.NE)).Error, "Không Dash xuyên X01.");

            GameState lockedSide = ApplyOk(Start(
                "....E",
                ".....",
                ".....",
                "X....",
                "H...."));
            Assert.AreEqual(ActionError.OccupiedByEnemy, Sim.Resolve(lockedSide, HeroAction.Dash(Direction.N)).Error);
            Assert.AreEqual(ActionError.CornerBlocked, Sim.Resolve(lockedSide, HeroAction.Move(Direction.NE)).Error,
                "X01 bị khóa vẫn chặn góc.");
        }

        [Test]
        public void WinAfterStasis_DoesNotRunEnemyPhase()
        {
            GameState s = Start(
                ".....",
                ".....",
                "..E..",
                "..H..",
                ".X...");

            TurnResult r = Sim.Resolve(ApplyOk(s), HeroAction.Move(Direction.N));

            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.IsFalse(r.EnemyPhaseRan);
            Assert.AreEqual(0, r.Events.Count(e => e.Kind == RuleEventKind.EnemySkipped));
        }

        [Test]
        public void LevelWithoutStasis_AndGameOver_AreRejected()
        {
            GameState noStasis = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H.X..");
            AssertRejectedUnchanged(noStasis, Sim.ApplyStasis(noStasis, noStasis.EnemyPos), ActionError.StasisNotAvailable);

            GameState lost = Sim.Resolve(Start(
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X..."), HeroAction.Wait).NextState;
            Assert.AreEqual(GameOutcome.Lost, lost.Outcome);
            AssertRejectedUnchanged(lost, Sim.ApplyStasis(lost, lost.EnemyPos), ActionError.GameOver);
        }

        [Test]
        public void Restart_RestoresStasisLockAndSupportFlag()
        {
            var session = new GameSession(LevelParser.Parse("id: R\ndash: 2\nstasis: 1\ngrid:\n....E\n.....\n.....\n.....\nH..X."),
                TestLevels.NewSimulation());

            Assert.IsTrue(session.ApplyStasis(session.Current.EnemyPos).Accepted);
            Assert.IsTrue(session.Current.EnemyLocked);
            session.Restart();

            Assert.AreEqual(session.InitialState, session.Current);
            Assert.AreEqual(1, session.Current.StasisUsesLeft);
            Assert.AreEqual(0, session.Current.EnemyLockPhases);
            Assert.IsFalse(session.Current.SupportUsedThisTurn);
        }

        [Test]
        public void Parser_ReadsStasis_DefaultsToZero_AndStasisSolutionPrefix()
        {
            LevelData level = LevelParser.Parse("id: P\nstasis: 1\ngrid:\n....E\n.....\n.....\n.....\nH...X\nsolution: N STASIS+DASH-E WAIT");

            Assert.AreEqual(1, level.StasisMaxUses);
            CollectionAssert.AreEqual(new[] { 1 }, level.ReferenceStasisTurns);
            CollectionAssert.AreEqual(new[] { HeroAction.Move(Direction.N), HeroAction.Dash(Direction.E), HeroAction.Wait }, level.ReferenceSolution);
            Assert.AreEqual(0, TestLevels.Parse("....E", ".....", ".....", ".....", "H...X").StasisMaxUses);
            Assert.Throws<LevelFormatException>(() => LevelParser.Parse("id: P\nstasis: x\ngrid:\n....E\n.....\n.....\n.....\nH...X"));
        }
    }
}
