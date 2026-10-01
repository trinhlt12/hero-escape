using System.Linq;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class DashTests
    {
        /// <summary>Lưới 5x5 có Dash 2 lần. Hàng đầu là hàng trên cùng (y = 4).</summary>
        static GameState Start(params string[] rows) =>
            GameState.CreateInitial(LevelParser.Parse("id: DASH_TEST\ndash: 2\ngrid:\n" + string.Join("\n", rows)));

        static TurnResult Dash(GameState s, Direction d) => TestLevels.NewSimulation().Resolve(s, HeroAction.Dash(d));

        static void AssertRejectedUnchanged(GameState s, TurnResult r, ActionError error, int step)
        {
            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(error, r.Error);
            Assert.AreEqual(step, r.FailedDashStep);
            Assert.AreSame(s, r.NextState, "Dash sai không được đổi trạng thái.");
            Assert.AreEqual(s.HeroPos, r.NextState.HeroPos, "Hero không được di chuyển một phần.");
            Assert.AreEqual(s.DashUsesLeft, r.NextState.DashUsesLeft, "Dash sai không được trừ lượt dùng.");
            Assert.AreEqual(s.Round, r.NextState.Round);
            Assert.IsFalse(r.EnemyPhaseRan, "Dash sai không được chạy X01.");
        }

        [Test]
        public void Dash_Straight_MovesTwoTiles_UsesOne_ThenOneEnemyStep()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");

            TurnResult r = Dash(s, Direction.N);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(new GridPos(0, 2), r.NextState.HeroPos);
            Assert.AreEqual(1, r.NextState.DashUsesLeft);
            Assert.AreEqual(s.Round + 1, r.NextState.Round);
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == RuleEventKind.HeroDashed));
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == RuleEventKind.EnemyMoved), "Chỉ đúng một lượt X01 sau Dash.");
            Assert.AreEqual(new GridPos(0, 2), r.AfterHeroState.HeroPos);
            Assert.AreEqual(s.EnemyPos, r.AfterHeroState.EnemyPos, "X01 không được đi giữa hai bước Dash.");
        }

        [Test]
        public void Dash_Diagonal_MovesTwoTiles()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");

            TurnResult r = Dash(s, Direction.NE);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(new GridPos(2, 2), r.NextState.HeroPos);
        }

        [Test]
        public void Dash_Step1BlockedByWall_IsRejected()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                "#....",
                "H...X");

            AssertRejectedUnchanged(s, Dash(s, Direction.N), ActionError.Wall, 1);
        }

        [Test]
        public void Dash_Step2BlockedByWall_IsRejected_WithoutPartialMove()
        {
            GameState s = Start(
                "....E",
                ".....",
                "#....",
                ".....",
                "H...X");

            AssertRejectedUnchanged(s, Dash(s, Direction.N), ActionError.Wall, 2);
        }

        [Test]
        public void Dash_Step2OutOfBounds_IsRejected()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                "H....",
                "....X");

            AssertRejectedUnchanged(s, Dash(s, Direction.S), ActionError.OutOfBounds, 2);
        }

        [Test]
        public void Dash_CornerRule_CheckedOnStep1()
        {
            // NE từ (0,0): bước 1 tới (1,1), ô cạnh (1,0) là Tường.
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H#..X");

            AssertRejectedUnchanged(s, Dash(s, Direction.NE), ActionError.CornerBlocked, 1);
        }

        [Test]
        public void Dash_CornerRule_CheckedOnStep2()
        {
            // NE từ (0,0): bước 1 (1,1) hợp lệ; bước 2 tới (2,2), ô cạnh (2,1) là Tường.
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                "..#..",
                "H...X");

            AssertRejectedUnchanged(s, Dash(s, Direction.NE), ActionError.CornerBlocked, 2);
        }

        [Test]
        public void Dash_CornerRule_EnemyOnSideBlocksStep2()
        {
            // NE từ (0,0): bước 2 từ (1,1) tới (2,2), X01 đứng ở ô cạnh (1,2).
            GameState s = Start(
                "....E",
                ".....",
                ".X...",
                ".....",
                "H....");

            AssertRejectedUnchanged(s, Dash(s, Direction.NE), ActionError.CornerBlocked, 2);
        }

        [Test]
        public void Dash_CannotJumpOverEnemy()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                "X....",
                "H....");

            AssertRejectedUnchanged(s, Dash(s, Direction.N), ActionError.OccupiedByEnemy, 1);
        }

        [Test]
        public void Dash_CannotLandOnEnemy()
        {
            GameState s = Start(
                "....E",
                ".....",
                "X....",
                ".....",
                "H....");

            AssertRejectedUnchanged(s, Dash(s, Direction.N), ActionError.OccupiedByEnemy, 2);
        }

        [Test]
        public void Dash_Step1OntoExit_WinsEvenIfStep2WouldLeaveBoard()
        {
            GameState s = Start(
                "E....",
                "H....",
                ".....",
                ".....",
                "....X");

            TurnResult r = Dash(s, Direction.N);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.AreEqual(s.Level.Exit, r.NextState.HeroPos);
            Assert.IsFalse(r.EnemyPhaseRan);
            Assert.AreEqual(s.EnemyPos, r.NextState.EnemyPos);
        }

        [Test]
        public void Dash_Step1OntoExit_WinsEvenIfStep2IsWall()
        {
            GameState s = Start(
                ".....",
                "..#..",
                "..E..",
                "..H..",
                "....X");

            TurnResult r = Dash(s, Direction.N);

            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.AreEqual(new GridPos(2, 2), r.NextState.HeroPos);
        }

        [Test]
        public void Dash_Step2OntoExit_Wins_EnemyDoesNotAct()
        {
            GameState s = Start(
                ".....",
                "..E..",
                ".....",
                "..H..",
                ".X...");

            TurnResult r = Dash(s, Direction.N);

            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.AreEqual(new GridPos(2, 3), r.NextState.HeroPos);
            Assert.IsFalse(r.EnemyPhaseRan);
            Assert.AreEqual(s.EnemyPos, r.NextState.EnemyPos);
            Assert.AreEqual(s.Round, r.NextState.Round);
        }

        [Test]
        public void Dash_NoUsesLeft_IsRejected()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");
            Simulation sim = TestLevels.NewSimulation();

            s = sim.Resolve(s, HeroAction.Dash(Direction.N)).NextState; // (0,2), còn 1
            s = sim.Resolve(s, HeroAction.Dash(Direction.E)).NextState; // (2,2), còn 0
            Assert.AreEqual(0, s.DashUsesLeft);
            Assert.AreEqual(GameOutcome.Playing, s.Outcome);

            AssertRejectedUnchanged(s, sim.Resolve(s, HeroAction.Dash(Direction.N)), ActionError.NoDashUsesLeft, 0);
        }

        [Test]
        public void Dash_InLevelWithoutDash_IsRejected()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");

            AssertRejectedUnchanged(s, TestLevels.NewSimulation().Resolve(s, HeroAction.Dash(Direction.N)), ActionError.DashNotAvailable, 0);
        }

        [Test]
        public void Dash_AfterGameOver_IsRejected()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X...");
            Simulation sim = TestLevels.NewSimulation();
            GameState lost = sim.Resolve(s, HeroAction.Wait).NextState;
            Assert.AreEqual(GameOutcome.Lost, lost.Outcome);

            Assert.AreEqual(ActionError.GameOver, sim.Resolve(lost, HeroAction.Dash(Direction.N)).Error);
        }

        [Test]
        public void Restart_RestoresDashUses()
        {
            var session = new GameSession(LevelParser.Parse("id: R\ndash: 2\ngrid:\n....E\n.....\n.....\n.....\nH...X"),
                TestLevels.NewSimulation());

            session.Apply(HeroAction.Dash(Direction.N));
            Assert.AreEqual(1, session.Current.DashUsesLeft);
            session.Restart();

            Assert.AreEqual(2, session.Current.DashUsesLeft);
            Assert.AreEqual(session.InitialState, session.Current);
        }

        [Test]
        public void Parser_ReadsDashCount_AndDashSolutionTokens()
        {
            LevelData level = LevelParser.Parse("id: P\ndash: 2\ngrid:\n....E\n.....\n.....\n.....\nH...X\nsolution: N DASH-NE WAIT");

            Assert.AreEqual(2, level.DashMaxUses);
            CollectionAssert.AreEqual(
                new[] { HeroAction.Move(Direction.N), HeroAction.Dash(Direction.NE), HeroAction.Wait },
                level.ReferenceSolution);
            Assert.AreEqual(0, TestLevels.Parse("....E", ".....", ".....", ".....", "H...X").DashMaxUses);
            Assert.Throws<LevelFormatException>(() => LevelParser.Parse("id: P\ndash: x\ngrid:\n....E\n.....\n.....\n.....\nH...X"));
            Assert.Throws<LevelFormatException>(() => LevelParser.Parse("id: P\ndash: -1\ngrid:\n....E\n.....\n.....\n.....\nH...X"));
        }
    }
}
