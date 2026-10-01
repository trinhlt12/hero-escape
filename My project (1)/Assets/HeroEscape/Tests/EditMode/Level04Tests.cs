using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Search;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>Kiểm chứng file level thật của Level 04 (hai X01) theo yêu cầu thiết kế.</summary>
    public class Level04Tests
    {
        public const string Level04Path = "Assets/HeroEscape/Data/Levels/M04_TwoEnemiesLevel.txt";

        static LevelData Load() => LevelParser.Parse(File.ReadAllText(Level04Path));

        static GameState Start() => GameState.CreateInitial(Load());

        static readonly Simulation Sim = TestLevels.NewSimulation();

        /// <summary>Trạng thái sau nước đầu N: Hero (0,1), X1 (3,0), X2 (0,4); còn Stasis.</summary>
        static GameState AfterFirstNorth()
        {
            TurnResult r = Sim.Resolve(Start(), HeroAction.Move(Direction.N));
            Assert.IsTrue(r.Accepted);
            return r.NextState;
        }

        [Test]
        public void Layout_Is6x6_WithTwoX01_TwoDashesOneStasis()
        {
            LevelData level = Load();

            Assert.AreEqual("M04_TWO_ENEMIES", level.Id);
            Assert.AreEqual(6, level.Width);
            Assert.AreEqual(6, level.Height);
            Assert.AreEqual(new GridPos(0, 0), level.HeroStart);
            Assert.AreEqual(new GridPos(5, 5), level.Exit);
            CollectionAssert.AreEqual(new[] { "X1", "X2" }, level.Enemies.Select(e => e.Id));
            CollectionAssert.AreEqual(new[] { new GridPos(4, 1), new GridPos(0, 5) }, level.Enemies.Select(e => e.Cell));
            Assert.AreEqual(2, level.DashMaxUses);
            Assert.AreEqual(1, level.StasisMaxUses);
        }

        [Test]
        public void ReferenceSolution_WinsWithRealRules_StasisOnX2()
        {
            LevelData level = Load();

            Assert.AreEqual("X2", level.ReferenceStasisTargets[1]);
            Assert.AreEqual(GameOutcome.Won, LevelReachability.Replay(Start(), Sim, LevelReachability.ReferenceTurns(level))?.Outcome);
        }

        [Test]
        public void Solvable_WithGivenResources_AndFoundSolutionReplays()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), Sim, maxDashes: 2, maxStasis: 1);

            Assert.AreEqual(ReachVerdict.Solved, r.Verdict);
            Assert.AreEqual(GameOutcome.Won, LevelReachability.Replay(Start(), Sim, r.Solution)?.Outcome);
        }

        [Test]
        public void Unsolvable_WithoutDash_ExhaustiveSearch()
        {
            LevelReachability.Result r = LevelReachability.Explore(Start(), Sim, maxDashes: 0, maxStasis: 1);

            Assert.AreEqual(ReachVerdict.Unsolvable, r.Verdict, $"Đã duyệt {r.StatesVisited} trạng thái.");
        }

        [Test]
        public void NoImmediateWin_OnFirstTurn()
        {
            GameState start = Start();

            foreach ((string target, GameState beforeMain) in LevelReachability.SupportOptions(start, Sim, start, maxStasis: 1))
            foreach (HeroAction action in LevelReachability.AllMainActions)
                Assert.AreNotEqual(GameOutcome.Won, Sim.Resolve(beforeMain, action).NextState.Outcome,
                    (target != null ? $"STASIS({target})+" : "") + action);
        }

        [Test]
        public void BothEnemies_CanChase_AndMoveDuringTheReferenceSolution()
        {
            GameState start = Start();
            for (int i = 0; i < start.Enemies.Count; i++)
                Assert.IsNotNull(EnemyPathfinder.FindPathToHero(start.Level, start.Enemies[i].Pos, start.HeroPos, start.EnemyCells(i)),
                    $"{start.Enemies[i].Id} không có đường tới Hero lúc đầu.");

            var moved = new System.Collections.Generic.HashSet<string>();
            GameState s = start;
            LevelData level = Load();
            foreach (PlannedTurn turn in LevelReachability.ReferenceTurns(level))
            {
                if (turn.UseStasis) s = Sim.ApplyStasis(s, turn.ResolveTarget(s)).NextState;
                TurnResult r = Sim.Resolve(s, turn.Main);
                foreach (RuleEvent e in r.Events.Where(e => e.Kind == RuleEventKind.EnemyMoved)) moved.Add(e.EnemyId);
                s = r.NextState;
            }

            CollectionAssert.AreEquivalent(new[] { "X1", "X2" }, moved);
        }

        [Test]
        public void AfterFirstNorth_BothEnemiesAreValidStasisTargets()
        {
            GameState s = AfterFirstNorth();

            Assert.AreEqual(new GridPos(0, 1), s.HeroPos);
            Assert.AreEqual(new GridPos(3, 0), s.Enemies[0].Pos);
            Assert.AreEqual(new GridPos(0, 4), s.Enemies[1].Pos);
            Assert.IsTrue(Sim.ApplyStasis(s, "X1").Accepted);
            Assert.IsTrue(Sim.ApplyStasis(s, "X2").Accepted);
        }

        [TestCase(Direction.N, "X2", "X1")]
        [TestCase(Direction.E, "X1", "X2")]
        public void StasisTargetChoice_SameDash_OneTargetSurvivesAndCanWin_OtherIsCaught(Direction dash, string goodTarget, string badTarget)
        {
            GameState s = AfterFirstNorth();

            TurnResult good = Sim.Resolve(Sim.ApplyStasis(s, goodTarget).NextState, HeroAction.Dash(dash));
            TurnResult bad = Sim.Resolve(Sim.ApplyStasis(s, badTarget).NextState, HeroAction.Dash(dash));

            Assert.IsTrue(good.Accepted && bad.Accepted);
            Assert.AreEqual(GameOutcome.Playing, good.NextState.Outcome, $"Khóa {goodTarget} rồi Dash {dash} phải sống qua pha địch.");
            Assert.AreEqual(ReachVerdict.Solved,
                LevelReachability.Explore(good.NextState, Sim, maxDashes: 2, maxStasis: 1).Verdict, "Và vẫn còn đường thắng.");
            Assert.AreEqual(GameOutcome.Lost, bad.NextState.Outcome, $"Khóa {badTarget} rồi Dash {dash} phải bị bắt ngay trong pha địch đó.");
        }
    }
}
