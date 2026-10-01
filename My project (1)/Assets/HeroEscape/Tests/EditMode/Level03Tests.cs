using System.IO;
using System.Linq;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>Kiểm chứng file level thật của Level 03 (Stasis + Dash) theo yêu cầu thiết kế.</summary>
    public class Level03Tests
    {
        public const string Level03Path = "Assets/HeroEscape/Data/Levels/M03_StasisLevel.txt";

        static LevelData Load() => LevelParser.Parse(File.ReadAllText(Level03Path));

        static GameState Start() => GameState.CreateInitial(Load());

        static readonly Simulation Sim = TestLevels.NewSimulation();

        [Test]
        public void Layout_Is5x5_WithTwoDashesAndOneStasis()
        {
            LevelData level = Load();

            Assert.AreEqual("M03_STASIS", level.Id);
            Assert.AreEqual(5, level.Width);
            Assert.AreEqual(5, level.Height);
            Assert.AreEqual(new GridPos(2, 0), level.HeroStart);
            Assert.AreEqual(new GridPos(2, 2), level.EnemyStart);
            Assert.AreEqual(new GridPos(4, 4), level.Exit);
            Assert.AreEqual(2, level.DashMaxUses);
            Assert.AreEqual(1, level.StasisMaxUses);
        }

        [Test]
        public void ReferenceSolution_WinsWithRealRules_AndUsesStasisOnX01()
        {
            LevelData level = Load();
            var turns = level.ReferenceSolution.Select((a, i) => new PlannedTurn(level.ReferenceStasisTurns.Contains(i), a)).ToList();

            Assert.Greater(level.ReferenceStasisTurns.Count, 0, "Lời giải tham chiếu phải dùng Stasis.");
            GameState end = LevelReachability.Replay(Start(), Sim, turns);

            Assert.IsNotNull(end, "Một bước của lời giải tham chiếu bị từ chối.");
            Assert.AreEqual(GameOutcome.Won, end.Outcome);
        }

        [Test]
        public void Solvable_WithTwoDashesAndOneStasis_AndFoundSolutionReplays()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), Sim, maxDashes: 2, maxStasis: 1);

            Assert.AreEqual(ReachVerdict.Solved, r.Verdict);
            Assert.IsTrue(r.Solution.Any(t => t.UseStasis), "Lời giải phải có ít nhất một lần dùng Stasis.");
            Assert.AreEqual(GameOutcome.Won, LevelReachability.Replay(Start(), Sim, r.Solution)?.Outcome);
        }

        [Test]
        public void Unsolvable_WithoutStasis_EvenWithTwoDashes_ExhaustiveSearch()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), Sim, maxDashes: 2, maxStasis: 0);

            // Unknown (chạm giới hạn) làm test thất bại: chỉ chấp nhận kết luận sau khi duyệt hết.
            Assert.AreEqual(ReachVerdict.Unsolvable, r.Verdict, $"Đã duyệt {r.StatesVisited} trạng thái.");
        }

        [Test]
        public void NoImmediateWin_OnFirstTurn_WithAnyMainActionOrStasisCombo()
        {
            GameState start = Start();

            foreach ((string stasisTarget, GameState beforeMain) in LevelReachability.SupportOptions(start, Sim, start, maxStasis: 1))
            foreach (HeroAction action in Directions.Order.Select(HeroAction.Move).Concat(Directions.Order.Select(HeroAction.Dash)).Append(HeroAction.Wait))
                Assert.AreNotEqual(GameOutcome.Won, Sim.Resolve(beforeMain, action).NextState.Outcome,
                    (stasisTarget != null ? $"STASIS({stasisTarget})+" : "") + action);
        }

        [Test]
        public void Timing_StasisOnFirstTurnIsValid_ButLosesTheWin()
        {
            SupportResult early = Sim.ApplyStasis(Start(), Start().EnemyPos);
            Assert.IsTrue(early.Accepted, "Stasis ở lượt 1 phải hợp lệ (trong tầm, có đường nhìn).");

            LevelReachability.Result afterEarly = LevelReachability.Explore(early.NextState, Sim, maxDashes: 2, maxStasis: 0);

            Assert.AreEqual(ReachVerdict.Unsolvable, afterEarly.Verdict, "Dùng Stasis ngay lượt 1 phải làm mất mọi đường thắng.");
        }

        [Test]
        public void Timing_HoldingStasisForTurnTwo_Wins()
        {
            // Lượt 1 đi W (giữ thẻ), lượt 2 khóa X01 rồi Dash N.
            GameState end = LevelReachability.Replay(Start(), Sim, new[]
            {
                new PlannedTurn(false, HeroAction.Move(Direction.W)),
                new PlannedTurn(true, HeroAction.Dash(Direction.N)),
                new PlannedTurn(false, HeroAction.Move(Direction.W)),
                new PlannedTurn(false, HeroAction.Move(Direction.N)),
                new PlannedTurn(false, HeroAction.Move(Direction.N)),
                new PlannedTurn(false, HeroAction.Move(Direction.E)),
                new PlannedTurn(false, HeroAction.Move(Direction.E)),
                new PlannedTurn(false, HeroAction.Dash(Direction.E)),
            });

            Assert.AreEqual(GameOutcome.Won, end?.Outcome);
        }
    }

    public class Level02RegressionTests
    {
        [Test]
        public void Levels01And02_HaveNoStasis()
        {
            LevelData level01 = LevelParser.Parse(File.ReadAllText(TestLevels.TestLevelPath));
            LevelData level02 = LevelParser.Parse(File.ReadAllText(Level02Tests.Level02Path));

            Assert.AreEqual(0, level01.StasisMaxUses);
            Assert.AreEqual(0, level02.StasisMaxUses);
            Assert.AreEqual(ActionError.StasisNotAvailable,
                TestLevels.NewSimulation().ApplyStasis(GameState.CreateInitial(level02), level02.EnemyStart).Error);
        }

        [Test]
        public void Level02_ReferenceSolution_GivesSameStatesAsMilestone02()
        {
            // Trạng thái sau mỗi lượt của E DASH-E E N N N N, ghi lại từ Milestone 02.
            var expected = new[]
            {
                (hero: new GridPos(1, 0), enemy: new GridPos(2, 1)),
                (hero: new GridPos(3, 0), enemy: new GridPos(2, 0)),
                (hero: new GridPos(4, 0), enemy: new GridPos(3, 0)),
                (hero: new GridPos(4, 1), enemy: new GridPos(4, 0)),
                (hero: new GridPos(4, 2), enemy: new GridPos(4, 1)),
                (hero: new GridPos(4, 3), enemy: new GridPos(4, 2)),
                (hero: new GridPos(4, 4), enemy: new GridPos(4, 2)),
            };
            LevelData level = LevelParser.Parse(File.ReadAllText(Level02Tests.Level02Path));
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
