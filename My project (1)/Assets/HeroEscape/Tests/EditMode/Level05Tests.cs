using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Search;
using HeroEscape.Unity;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>Kiểm chứng Level 05 (X01 Tactical) bằng Tactical thật, cùng ngân sách và fallback với gameplay.</summary>
    public class Level05Tests
    {
        public const string Level05Path = "Assets/HeroEscape/Data/Levels/M05_TacticalLevel.txt";

        public static LevelData Load() => LevelParser.Parse(File.ReadAllText(Level05Path));

        public static GameState Start() => GameState.CreateInitial(Load());

        /// <summary>Sau nước đầu N: Hero (0,1), X01 (1,2), đầu pha địch (chưa tới lượt X01).</summary>
        public static GameState AfterFirstNorth()
        {
            TurnResult r = new Simulation(new BasicChasePolicy()).Resolve(Start(), HeroAction.Move(Direction.N));
            return r.AfterHeroState;
        }

        static Simulation TacticalSim(LevelData level) => EnemyPolicies.CreateSimulation(level);

        [Test]
        public void Layout_Is6x6_OneTacticalX01_TwoDashesOneStasis()
        {
            LevelData level = Load();

            Assert.AreEqual("M05_TACTICAL", level.Id);
            Assert.AreEqual(6, level.Width);
            Assert.AreEqual(6, level.Height);
            Assert.AreEqual(AiProfile.Tactical, level.AiProfile);
            Assert.AreEqual(1, level.Enemies.Count);
            Assert.AreEqual(new GridPos(0, 0), level.HeroStart);
            Assert.AreEqual(new GridPos(1, 2), level.EnemyStart);
            Assert.AreEqual(new GridPos(5, 5), level.Exit);
            Assert.AreEqual(2, level.DashMaxUses);
            Assert.AreEqual(1, level.StasisMaxUses);
        }

        [Test]
        public void ReferenceSolution_WinsAgainstRealTactical()
        {
            LevelData level = Load();
            Assert.AreEqual(GameOutcome.Won,
                LevelReachability.Replay(GameState.CreateInitial(level), TacticalSim(level), LevelReachability.ReferenceTurns(level))?.Outcome);
        }

        [Test]
        public void Solvable_WithTacticalAndGivenResources_FoundSolutionReplays()
        {
            LevelData level = Load();
            LevelReachability.Result r = LevelReachability.Explore(GameState.CreateInitial(level), TacticalSim(level), maxDashes: 2, maxStasis: 1);

            Assert.AreEqual(ReachVerdict.Solved, r.Verdict);
            Assert.AreEqual(GameOutcome.Won, LevelReachability.Replay(GameState.CreateInitial(level), TacticalSim(level), r.Solution)?.Outcome);
        }

        [Test]
        public void Tactical_MakesWalkingAloneFail_WhileBasicWouldLetHeroWalkOut()
        {
            LevelData tactical = Load();
            LevelData basic = LevelParser.Parse(File.ReadAllText(Level05Path).Replace("ai: tactical", "ai: basic"));

            Assert.AreEqual(ReachVerdict.Unsolvable,
                LevelReachability.Explore(GameState.CreateInitial(tactical), TacticalSim(tactical), maxDashes: 0, maxStasis: 0).Verdict,
                "Tactical: chỉ Move/Wait thì vô nghiệm (duyệt hết).");
            Assert.AreEqual(ReachVerdict.Solved,
                LevelReachability.Explore(GameState.CreateInitial(basic), TacticalSim(basic), maxDashes: 0, maxStasis: 0).Verdict,
                "Cùng bố cục với Basic: đi bộ là thắng.");
        }

        [Test]
        public void NoImmediateWin_OnFirstTurn()
        {
            LevelData level = Load();
            GameState start = GameState.CreateInitial(level);
            Simulation sim = TacticalSim(level);

            foreach ((string target, GameState beforeMain) in LevelReachability.SupportOptions(start, sim, start, maxStasis: 1))
            foreach (HeroAction action in LevelReachability.AllMainActions)
                Assert.AreNotEqual(GameOutcome.Won, sim.Resolve(beforeMain, action).NextState.Outcome);
        }

        [Test]
        public void TacticalDiffersFromBasic_ForTacticalReasons_AfterFirstNorth()
        {
            GameState s = AfterFirstNorth();
            TacticalDecision tactical = new TacticalChasePolicy().Decide(s, 0);
            Direction? basic = new BasicChasePolicy().TryChooseStep(s, 0, out Direction bd) ? bd : (Direction?)null;

            Assert.IsFalse(tactical.UsedFallback, "Ví dụ phải là Tactical thật, không phải fallback.");
            Assert.IsFalse(tactical.ImmediateCapture);
            Assert.IsNull(tactical.Chosen, "Tactical chọn Wait (đứng sau góc tường).");
            Assert.AreEqual(Direction.W, basic, "Basic tiến sang (0,2).");

            TacticalCandidate wait = tactical.Candidates.Single(c => c.Step == null);
            TacticalCandidate west = tactical.Candidates.Single(c => c.Step == Direction.W);
            Assert.IsFalse(wait.HeroCanWin || west.HeroCanWin);
            Assert.AreEqual(2, wait.SafeResponses, "Sau Wait, Hero chỉ còn 2 phản ứng an toàn.");
            Assert.AreEqual(3, west.SafeResponses, "Sau nước Basic, Hero có 3 (thêm cách dùng Stasis).");
            Assert.AreEqual(0, wait.StasisResponses, "Góc tường chắn đường nhìn nên Hero không Stasis được X01.");
            Assert.Greater(west.StasisResponses, 0);
        }

        // ---------------- Preview khớp thực thi ----------------

        static TurnInputController NewInput(LevelData level, Simulation simulation = null) =>
            new TurnInputController(new GameSession(level, simulation ?? TacticalSim(level)));

        static void AssertPreviewMatchesExecution(LevelData level, System.Func<Simulation> makeSim, System.Action<TurnInputController> prefix)
        {
            var probe = NewInput(level, makeSim());
            prefix(probe);
            foreach (InputMode mode in new[] { InputMode.Normal, InputMode.ChoosingDashTarget })
            {
                if (mode == InputMode.ChoosingDashTarget)
                {
                    if (probe.Session.Current.DashUsesLeft == 0) continue;
                    probe.PressDash();
                }

                foreach (GridPos cell in probe.HighlightedCells.ToList())
                {
                    TurnPreview preview = probe.PreviewBoardHover(cell);
                    var executor = NewInput(level, makeSim());
                    prefix(executor);
                    if (mode == InputMode.ChoosingDashTarget) executor.PressDash();
                    InputResponse done = executor.PressBoard(false, cell);

                    Assert.IsTrue(done.Acted);
                    Assert.AreEqual(done.Turn.NextState, preview.Turn.NextState, $"{mode} {cell}");
                }

                probe.Cancel();
            }

            TurnPreview wait = probe.PreviewWaitHover();
            var waiter = NewInput(level, makeSim());
            prefix(waiter);
            Assert.AreEqual(waiter.PressWait().Turn.NextState, wait.Turn.NextState, "Wait");
        }

        [Test]
        public void Preview_MatchesTacticalExecution_MoveDashWait_FromStart()
        {
            LevelData level = Load();
            AssertPreviewMatchesExecution(level, () => TacticalSim(level), _ => { });
        }

        [Test]
        public void Preview_MatchesTacticalExecution_AfterStasis()
        {
            LevelData level = Load();

            // Tactical thường đứng ngoài tầm/đường nhìn Stasis (trên lời giải tham chiếu không lúc nào Stasis hợp lệ),
            // nên tìm bằng BFS ngắn trên Simulation thật một trạng thái đạt tới được mà Stasis lên X01 hợp lệ.
            List<PlannedTurn> prefix = FindPrefixWhereStasisIsValid(level);

            Assert.IsNotNull(prefix, "Không tìm thấy trạng thái đạt tới được có Stasis hợp lệ.");
            AssertPreviewMatchesExecution(level, () => TacticalSim(level), input =>
            {
                ReplayInto(input, prefix);
                input.PressStasis();
                Assert.AreEqual(UiText.StasisApplied, input.PressBoard(false, input.Session.Current.EnemyPos).Feedback);
            });
        }

        static List<PlannedTurn> FindPrefixWhereStasisIsValid(LevelData level)
        {
            Simulation sim = TacticalSim(level);
            var queue = new Queue<(GameState state, List<PlannedTurn> path)>();
            var seen = new HashSet<GameState>();
            queue.Enqueue((GameState.CreateInitial(level), new List<PlannedTurn>()));
            while (queue.Count > 0 && seen.Count < 2000)
            {
                (GameState s, List<PlannedTurn> path) = queue.Dequeue();
                if (sim.ApplyStasis(s, "X1").Accepted) return path;
                foreach (HeroAction action in LevelReachability.AllMainActions)
                {
                    TurnResult r = sim.Resolve(s, action);
                    if (r.Accepted && r.NextState.Outcome == GameOutcome.Playing && seen.Add(r.NextState))
                        queue.Enqueue((r.NextState, path.Append(new PlannedTurn((string)null, action)).ToList()));
                }
            }

            return null;
        }

        [Test]
        public void Preview_MatchesExecution_EvenWhenTacticalFallsBack()
        {
            LevelData level = Load();
            AssertPreviewMatchesExecution(level, () => new Simulation(new TacticalChasePolicy(transitionBudget: 5)), _ => { });
        }

        [Test]
        public void Hovering_DoesNotChangeRealState_OrTacticalOutcome()
        {
            LevelData level = Load();
            var input = NewInput(level);
            GameState before = input.Session.Current;

            for (int repeat = 0; repeat < 3; repeat++)
                for (int x = 0; x < 6; x++)
                for (int y = 0; y < 6; y++)
                    input.PreviewBoardHover(new GridPos(x, y));

            Assert.AreSame(before, input.Session.Current);
            Assert.AreEqual(0, input.Session.Revision);
            var fresh = NewInput(level);
            Assert.AreEqual(fresh.PressBoard(false, new GridPos(0, 1)).Turn.NextState, input.PressBoard(false, new GridPos(0, 1)).Turn.NextState,
                "Rê chuột nhiều lần không đổi quyết định của X.");
        }

        [Test]
        public void Restart_DoesNotReuseStaleCachedPreview()
        {
            LevelData level = Load();
            var input = NewInput(level);
            var cell = new GridPos(1, 0);
            input.PreviewBoardHover(cell);
            input.PressBoard(false, new GridPos(0, 1));

            input.Restart();
            TurnPreview afterRestart = input.PreviewBoardHover(cell);

            Assert.AreEqual(NewInput(level).PreviewBoardHover(cell).Turn.NextState, afterRestart.Turn.NextState);
        }

        [Test]
        public void ReferenceSolution_WinsByClickingHighlightedTiles()
        {
            LevelData level = Load();
            var input = ReplayInput(level, LevelReachability.ReferenceTurns(level));
            Assert.AreEqual(GameOutcome.Won, input.Session.Current.Outcome);
        }

        static TurnInputController ReplayInput(LevelData level, IEnumerable<PlannedTurn> turns)
        {
            var input = NewInput(level);
            ReplayInto(input, turns);
            return input;
        }

        static void ReplayInto(TurnInputController input, IEnumerable<PlannedTurn> turns)
        {
            foreach (PlannedTurn turn in turns)
            {
                if (turn.UseStasis)
                {
                    input.PressStasis();
                    input.PressBoard(false, input.Session.Current.EnemyPos);
                }

                if (turn.Main.Kind == HeroActionKind.Wait)
                {
                    Assert.IsTrue(input.PressWait().Acted);
                    continue;
                }

                Assert.IsTrue(input.Session.TryGetDestination(turn.Main, out GridPos destination, out _, out _));
                if (turn.Main.Kind == HeroActionKind.Dash) input.PressDash();
                Assert.IsTrue(input.PressBoard(false, destination).Acted, turn.ToString());
            }
        }
    }
}
