using System.IO;
using System.Linq;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>Kiểm chứng file level thật của Level 02 (Dash) theo yêu cầu thiết kế.</summary>
    public class Level02Tests
    {
        public const string Level02Path = "Assets/HeroEscape/Data/Levels/M02_DashLevel.txt";

        static LevelData Load() => LevelParser.Parse(File.ReadAllText(Level02Path));

        static GameState Start() => GameState.CreateInitial(Load());

        [Test]
        public void Layout_Is5x5_WithTwoDashes()
        {
            LevelData level = Load();

            Assert.AreEqual("M02_DASH", level.Id);
            Assert.AreEqual(5, level.Width);
            Assert.AreEqual(5, level.Height);
            Assert.AreEqual(new GridPos(0, 0), level.HeroStart);
            Assert.AreEqual(new GridPos(2, 2), level.EnemyStart);
            Assert.AreEqual(new GridPos(4, 4), level.Exit);
            Assert.AreEqual(2, level.DashMaxUses);
        }

        [Test]
        public void ReferenceSolution_WinsWithRealRules_AndUsesDash()
        {
            LevelData level = Load();
            var session = new GameSession(level, TestLevels.NewSimulation());

            for (int i = 0; i < level.ReferenceSolution.Count; i++)
            {
                Assert.AreEqual(GameOutcome.Playing, session.Current.Outcome, $"Trận kết thúc trước bước {i + 1}.");
                TurnResult r = session.Apply(level.ReferenceSolution[i]);
                Assert.IsTrue(r.Accepted, $"Bước {i + 1} ({level.ReferenceSolution[i]}) bị từ chối: {r.Error}.");
            }

            Assert.AreEqual(GameOutcome.Won, session.Current.Outcome);
            int dashes = level.ReferenceSolution.Count(a => a.Kind == HeroActionKind.Dash);
            Assert.That(dashes, Is.InRange(1, 2));
        }

        [Test]
        public void Solvable_WithAtMostTwoDashes()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), TestLevels.NewSimulation(), maxDashes: 2);

            Assert.AreEqual(ReachVerdict.Solved, r.Verdict);
            Assert.That(r.Solution.Count(a => a.Kind == HeroActionKind.Dash), Is.InRange(1, 2));
        }

        [Test]
        public void Solvable_WithOneDash_SoUsingBothIsNotRequired()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), TestLevels.NewSimulation(), maxDashes: 1);

            Assert.AreEqual(ReachVerdict.Solved, r.Verdict);
        }

        [Test]
        public void Unsolvable_WithOnlyMoveAndWait_ExhaustiveSearch()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), TestLevels.NewSimulation(), maxDashes: 0);

            // Unknown (hết giới hạn) sẽ làm test thất bại: chỉ chấp nhận kết luận sau khi duyệt hết.
            Assert.AreEqual(ReachVerdict.Unsolvable, r.Verdict, $"Đã duyệt {r.StatesVisited} trạng thái.");
        }

        [Test]
        public void NoDashFromStart_WinsImmediately()
        {
            GameState start = Start();
            Simulation sim = TestLevels.NewSimulation();

            foreach (Direction d in Directions.Order)
                Assert.AreNotEqual(GameOutcome.Won, sim.Resolve(start, HeroAction.Dash(d)).NextState.Outcome, d.ToString());
        }

        [Test]
        public void TrapDash_WaitThenDashEast_IsValidButCaughtImmediately()
        {
            var session = new GameSession(Load(), TestLevels.NewSimulation());

            session.Apply(HeroAction.Wait);
            TurnResult dash = session.Apply(HeroAction.Dash(Direction.E));

            Assert.IsTrue(dash.Accepted, "Dash này hợp lệ theo luật.");
            Assert.AreEqual(1, session.Current.DashUsesLeft);
            Assert.AreEqual(GameOutcome.Lost, session.Current.Outcome, "X01 bắt Hero ngay trong lượt địch kế tiếp.");
        }
    }

    public class Level01RegressionTests
    {
        static LevelData Load() => LevelParser.Parse(File.ReadAllText(TestLevels.TestLevelPath));

        [Test]
        public void Level01_HasNoDash_AndRejectsEveryDash()
        {
            GameState start = GameState.CreateInitial(Load());
            Simulation sim = TestLevels.NewSimulation();

            Assert.AreEqual(0, start.Level.DashMaxUses);
            foreach (Direction d in Directions.Order)
            {
                TurnResult r = sim.Resolve(start, HeroAction.Dash(d));
                Assert.IsFalse(r.Accepted, d.ToString());
                Assert.AreEqual(ActionError.DashNotAvailable, r.Error);
                Assert.AreSame(start, r.NextState);
            }
        }

        [Test]
        public void Level01_ReferenceSolution_GivesSameStatesAsMilestone01()
        {
            // Trạng thái sau mỗi lượt của chuỗi E E E NE N N N, ghi lại từ Milestone 01.
            var expected = new[]
            {
                (hero: new GridPos(1, 0), enemy: new GridPos(1, 1)),
                (hero: new GridPos(2, 0), enemy: new GridPos(1, 0)),
                (hero: new GridPos(3, 0), enemy: new GridPos(2, 0)),
                (hero: new GridPos(4, 1), enemy: new GridPos(3, 0)),
                (hero: new GridPos(4, 2), enemy: new GridPos(4, 1)),
                (hero: new GridPos(4, 3), enemy: new GridPos(4, 2)),
                (hero: new GridPos(4, 4), enemy: new GridPos(4, 2)),
            };
            LevelData level = Load();
            var session = new GameSession(level, TestLevels.NewSimulation());

            Assert.AreEqual(expected.Length, level.ReferenceSolution.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                session.Apply(level.ReferenceSolution[i]);
                Assert.AreEqual(expected[i].hero, session.Current.HeroPos, $"Hero sau lượt {i + 1}");
                Assert.AreEqual(expected[i].enemy, session.Current.EnemyPos, $"X01 sau lượt {i + 1}");
            }

            Assert.AreEqual(GameOutcome.Won, session.Current.Outcome);
        }
    }
}
