using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Search;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class TacticalTests
    {
        static LevelData Parse(int dash, int stasis, params string[] rows) =>
            LevelParser.Parse($"id: T\nai: tactical\ndash: {dash}\nstasis: {stasis}\ngrid:\n" + string.Join("\n", rows));

        [Test]
        public void Profile_DefaultsToBasic_AndParsesTactical()
        {
            Assert.AreEqual(AiProfile.Basic, TestLevels.Parse("....E", ".....", ".....", ".....", "H...X").AiProfile);
            Assert.AreEqual(AiProfile.Tactical, Parse(0, 0, "....E", ".....", ".....", ".....", "H...X").AiProfile);
            Assert.Throws<LevelFormatException>(() => LevelParser.Parse("id: T\nai: smart\ngrid:\n....E\n.....\n.....\n.....\nH...X"));

            foreach (string path in new[] { TestLevels.TestLevelPath, Level02Tests.Level02Path, Level03Tests.Level03Path, Level04Tests.Level04Path })
            {
                LevelData level = LevelParser.Parse(File.ReadAllText(path));
                Assert.AreEqual(AiProfile.Basic, level.AiProfile, path);
                Assert.IsInstanceOf<BasicChasePolicy>(EnemyPolicies.ForProfile(level.AiProfile));
            }

            Assert.IsInstanceOf<TacticalChasePolicy>(EnemyPolicies.ForProfile(AiProfile.Tactical));
        }

        [Test]
        public void CapturesImmediately_WithoutSearching()
        {
            GameState s = GameState.CreateInitial(Parse(2, 1,
                "....E",
                ".....",
                ".....",
                ".X...",
                "H...."));
            var policy = new TacticalChasePolicy();

            TacticalDecision d = policy.Decide(s, 0);

            Assert.IsTrue(d.ImmediateCapture);
            Assert.AreEqual(Direction.SW, d.Chosen);
            Assert.AreEqual(0, d.Transitions);
        }

        [Test]
        public void HeroResponses_IncludeDashAndStasis_WithRealResourcesOnly()
        {
            string[] rows =
            {
                "....E",
                ".....",
                ".....",
                ".....",
                "H..X.",
            };
            TacticalDecision withSkills = new TacticalChasePolicy().Decide(GameState.CreateInitial(Parse(2, 1, rows)), 0);
            TacticalDecision without = new TacticalChasePolicy().Decide(GameState.CreateInitial(Parse(0, 0, rows)), 0);

            Assert.IsTrue(withSkills.Candidates.Any(c => c.DashResponses > 0), "Có xét phản ứng Dash.");
            Assert.IsTrue(withSkills.Candidates.Any(c => c.StasisResponses > 0), "Có xét phản ứng Stasis + hành động chính.");
            Assert.IsTrue(without.Candidates.All(c => c.DashResponses == 0 && c.StasisResponses == 0), "Không có tài nguyên thì không xét.");
        }

        [Test]
        public void HeroWinOnlyByDash_IsSeen_AndBranchEndsAtTheWin()
        {
            // Hero (4,2): Move N tới (4,3) chưa thắng; chỉ Dash N mới tới Cửa (4,4). X01 ở xa không chặn được.
            string[] rows =
            {
                "....E",
                ".....",
                "....H",
                ".....",
                "X....",
            };
            TacticalDecision withDash = new TacticalChasePolicy().Decide(GameState.CreateInitial(Parse(1, 0, rows)), 0);
            TacticalDecision noDash = new TacticalChasePolicy().Decide(GameState.CreateInitial(Parse(0, 0, rows)), 0);

            Assert.IsTrue(withDash.Candidates.All(c => c.HeroCanWin));
            Assert.IsTrue(withDash.Candidates.All(c => c.MaxSafeDistance == TacticalCandidate.InfiniteDistance), "Nhánh thắng dừng ngay (khoảng cách vô hạn).");
            Assert.IsTrue(noDash.Candidates.All(c => !c.HeroCanWin));
        }

        [Test]
        public void LockedEnemy_SkipsTurn_WithoutRunningTactical()
        {
            LevelData level = Parse(2, 1,
                "....E",
                ".....",
                ".....",
                ".....",
                "H..X.");
            var policy = new TacticalChasePolicy();
            var sim = new Simulation(policy);
            GameState locked = sim.ApplyStasis(GameState.CreateInitial(level), "X1").NextState;
            int decisions = policy.DecisionCount;

            TurnResult r = sim.Resolve(locked, HeroAction.Wait);

            Assert.IsTrue(r.Events.Any(e => e.Kind == RuleEventKind.EnemySkipped));
            Assert.AreEqual(decisions, policy.DecisionCount, "X bị khóa không gọi Tactical.");
        }

        [Test]
        public void SameStateAndConfig_GiveSameDecision()
        {
            GameState s = Level05Tests.AfterFirstNorth();
            TacticalDecision a = new TacticalChasePolicy().Decide(s, 0);
            var reused = new TacticalChasePolicy();
            reused.Decide(Level05Tests.Start(), 0); // lịch sử khác không ảnh hưởng
            TacticalDecision b = reused.Decide(s, 0);

            Assert.AreEqual(a.Chosen, b.Chosen);
            Assert.AreEqual(a.Transitions, b.Transitions);
            CollectionAssert.AreEqual(a.Candidates.Select(c => c.ToString()), b.Candidates.Select(c => c.ToString()));
        }

        [Test]
        public void BudgetExhausted_FallsBackToBasicFromOriginalState()
        {
            GameState s = Level05Tests.AfterFirstNorth();
            var tiny = new TacticalChasePolicy(transitionBudget: 5);

            TacticalDecision d = tiny.Decide(s, 0);

            Assert.IsTrue(d.UsedFallback);
            Assert.AreEqual(5, d.Transitions);
            Assert.IsEmpty(d.Candidates, "Bỏ kết quả đánh giá dở dang.");
            Direction? basic = new BasicChasePolicy().TryChooseStep(s, 0, out Direction bd) ? bd : (Direction?)null;
            Assert.AreEqual(basic, d.Chosen);
            Assert.AreEqual(1, tiny.FallbackCount);
        }

        [Test]
        public void DefaultBudget_IsEnough_ForEveryDecisionOnLevel05ReferenceSolution()
        {
            LevelData level = Level05Tests.Load();
            var policy = new TacticalChasePolicy();
            GameState end = LevelReachability.Replay(GameState.CreateInitial(level), new Simulation(policy), LevelReachability.ReferenceTurns(level));

            Assert.AreEqual(GameOutcome.Won, end?.Outcome);
            Assert.Greater(policy.DecisionCount, 0);
            Assert.AreEqual(0, policy.FallbackCount, "Không fallback trên lời giải đã kiểm chứng.");
            Assert.AreEqual(TacticalChasePolicy.DefaultTransitionBudget, 20000);
        }
    }
}
