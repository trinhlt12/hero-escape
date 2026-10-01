using System.Linq;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class SimulationTests
    {
        static readonly string[] OpenBoard =
        {
            "....E",
            ".....",
            ".....",
            ".....",
            "H...X",
        };

        [Test]
        public void ValidMove_MovesHero_ThenEnemyTakesOneStep()
        {
            GameState s = GameState.CreateInitial(TestLevels.Parse(OpenBoard));

            TurnResult r = TestLevels.NewSimulation().Resolve(s, HeroAction.Move(Direction.N));

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(new GridPos(0, 1), r.NextState.HeroPos);
            Assert.AreEqual(new GridPos(3, 0), r.NextState.EnemyPos); // X01 (4,0) -> Hero (0,1): bước đầu W theo phá hòa
            Assert.AreEqual(2, r.NextState.Round);
            Assert.IsTrue(r.EnemyPhaseRan);
        }

        [TestCase(Direction.S)]  // ra ngoài bàn
        [TestCase(Direction.SW)] // ra ngoài bàn
        public void InvalidMove_ChangesNothing_AndDoesNotRunEnemy(Direction direction)
        {
            GameState s = GameState.CreateInitial(TestLevels.Parse(OpenBoard));

            TurnResult r = TestLevels.NewSimulation().Resolve(s, HeroAction.Move(direction));

            Assert.IsFalse(r.Accepted);
            Assert.AreEqual(ActionError.OutOfBounds, r.Error);
            Assert.AreSame(s, r.NextState);
            Assert.IsFalse(r.EnemyPhaseRan);
            Assert.AreEqual(0, r.Events.Count);
        }

        [Test]
        public void MoveIntoWall_OrCutBlockedCorner_IsRejected()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                "#....",
                "H#..X");
            Simulation sim = TestLevels.NewSimulation();

            Assert.AreEqual(ActionError.Wall, sim.Resolve(s, HeroAction.Move(Direction.N)).Error);
            Assert.AreEqual(ActionError.Wall, sim.Resolve(s, HeroAction.Move(Direction.E)).Error);
            // Ô đích (1,1) trống nhưng ô cạnh (0,1) là Tường.
            Assert.AreEqual(ActionError.CornerBlocked, sim.Resolve(
                TestLevels.Start("....E", ".....", ".....", "#....", "H...X"), HeroAction.Move(Direction.NE)).Error);
        }

        [Test]
        public void Wait_RunsExactlyOneEnemyStep()
        {
            GameState s = GameState.CreateInitial(TestLevels.Parse(OpenBoard));

            TurnResult r = TestLevels.NewSimulation().Resolve(s, HeroAction.Wait);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(s.HeroPos, r.NextState.HeroPos);
            Assert.AreEqual(new GridPos(3, 0), r.NextState.EnemyPos);
            Assert.AreEqual(s.Round + 1, r.NextState.Round);
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == RuleEventKind.EnemyMoved));
        }

        [Test]
        public void HeroReachesExit_WinsImmediately_EnemyDoesNotAct()
        {
            // X01 đứng kề ô Hero sẽ đi tới; nếu pha X01 chạy thì vị trí X01 sẽ đổi.
            GameState s = TestLevels.Start(
                ".....",
                ".....",
                "...E.",
                "..H..",
                ".X...");

            TurnResult r = TestLevels.NewSimulation().Resolve(s, HeroAction.Move(Direction.NE));

            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.AreEqual(s.Level.Exit, r.NextState.HeroPos);
            Assert.AreEqual(s.EnemyPos, r.NextState.EnemyPos);
            Assert.AreEqual(s.Round, r.NextState.Round);
            Assert.IsFalse(r.EnemyPhaseRan);
        }

        [Test]
        public void EnemyStepsOntoHero_Loses()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X...");

            TurnResult r = TestLevels.NewSimulation().Resolve(s, HeroAction.Wait);

            Assert.AreEqual(GameOutcome.Lost, r.NextState.Outcome);
            Assert.AreEqual(r.NextState.HeroPos, r.NextState.EnemyPos);
        }

        [Test]
        public void NoPath_EnemyStaysPut()
        {
            GameState s = TestLevels.Start(
                ".#...",
                ".#...",
                "XEH..",
                ".#...",
                ".#...");

            TurnResult r = TestLevels.NewSimulation().Resolve(s, HeroAction.Wait);

            Assert.IsTrue(r.Accepted);
            Assert.AreEqual(s.EnemyPos, r.NextState.EnemyPos);
            Assert.AreEqual(GameOutcome.Playing, r.NextState.Outcome);
            Assert.IsTrue(r.Events.Any(e => e.Kind == RuleEventKind.EnemyStayed));
        }

        [Test]
        public void AfterGameOver_ActionsAreRejected()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X...");
            Simulation sim = TestLevels.NewSimulation();
            GameState lost = sim.Resolve(s, HeroAction.Wait).NextState;

            Assert.AreEqual(ActionError.GameOver, sim.Resolve(lost, HeroAction.Wait).Error);
            Assert.AreEqual(ActionError.GameOver, sim.Resolve(lost, HeroAction.Move(Direction.N)).Error);
        }

        [Test]
        public void SameStateAndAction_GiveSameResult()
        {
            GameState s = GameState.CreateInitial(TestLevels.Parse(OpenBoard));

            TurnResult a = TestLevels.NewSimulation().Resolve(s, HeroAction.Move(Direction.NE));
            TurnResult b = TestLevels.NewSimulation().Resolve(s, HeroAction.Move(Direction.NE));

            Assert.AreEqual(a.NextState, b.NextState);
            CollectionAssert.AreEqual(a.Events, b.Events);
        }
    }
}
