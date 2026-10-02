using System.Linq;
using HeroEscape.Core;
using HeroEscape.Search;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>Luật thẻ phụ trợ Barrier trong Core (không cần UnityEngine).</summary>
    public class BarrierTests
    {
        static readonly Simulation Sim = TestLevels.NewSimulation();

        static LevelData Level(int barrier, int stasis, params string[] rows) =>
            LevelParser.Parse($"id: BARRIER_TEST\ndash: 2\nstasis: {stasis}\nbarrier: {barrier}\ngrid:\n" + string.Join("\n", rows));

        static GameState Start(params string[] rows) => GameState.CreateInitial(Level(1, 1, rows));

        // Hero (1,1); Tường (1,2); X01 (0,0).
        static readonly string[] Targets =
        {
            "....E",
            ".....",
            ".#...",
            ".H...",
            "X....",
        };

        // ---------------- Chọn mục tiêu ----------------

        [TestCase(2, 1, ActionError.None)]
        [TestCase(1, 0, ActionError.None)]
        [TestCase(0, 1, ActionError.None)]
        [TestCase(2, 0, ActionError.None, TestName = "DiagonalWithFreeCorner_IsValid")]
        [TestCase(1, 2, ActionError.Wall)]
        [TestCase(2, 2, ActionError.CornerBlocked, TestName = "Diagonal_CornerWall_IsBlocked_NE")]
        [TestCase(0, 2, ActionError.CornerBlocked, TestName = "Diagonal_CornerWall_IsBlocked_NW")]
        [TestCase(0, 0, ActionError.OccupiedByEnemy)]
        [TestCase(3, 1, ActionError.TargetNotAdjacent)]
        [TestCase(1, 3, ActionError.TargetNotAdjacent)]
        [TestCase(1, 1, ActionError.TargetNotAdjacent, TestName = "HeroCell_IsNotATarget")]
        public void Targets_Adjacent8_EmptyFloor_WithCornerRule(int x, int y, ActionError expected)
        {
            GameState start = Start(Targets);

            SupportResult r = Sim.ApplyBarrier(start, new GridPos(x, y));

            Assert.AreEqual(expected, r.Error);
            Assert.AreEqual(expected == ActionError.None, r.Accepted);
            if (!r.Accepted)
            {
                Assert.AreSame(start, r.NextState, "Mục tiêu sai không đổi bất cứ gì.");
                Assert.IsEmpty(r.Events);
            }
        }

        [Test]
        public void Diagonal_CornerOccupiedByEnemy_IsBlocked()
        {
            // Hero (1,1), X01 (1,0): ô SW (0,0) có góc (1,0) là X.
            GameState start = Start("....E", ".....", ".....", ".H...", ".X...");
            Assert.AreEqual(ActionError.CornerBlocked, Sim.ApplyBarrier(start, new GridPos(0, 0)).Error);
        }

        [Test]
        public void ExitAndOutOfBoard_AreNotTargets()
        {
            GameState nearExit = Start("....E", "...H.", ".....", ".....", "X....");
            Assert.AreEqual(ActionError.TargetIsExit, Sim.ApplyBarrier(nearExit, new GridPos(4, 4)).Error);

            GameState atEdge = Start("....E", ".....", ".....", "H....", "....X");
            Assert.AreEqual(ActionError.OutOfBounds, Sim.ApplyBarrier(atEdge, new GridPos(-1, 1)).Error);
        }

        [Test]
        public void CellWithBarrier_IsNotATarget()
        {
            // Trạng thái dựng tay: vật cản còn và đã sang lượt mới (không xảy ra trong gameplay vì vật cản hết ở cuối pha).
            LevelData level = Level(2, 0, Targets);
            GameState state = GameState.CreateInitial(level).WithBarrierPlaced(new GridPos(2, 1)).WithNextRound();

            Assert.AreEqual(ActionError.Barrier, Sim.ApplyBarrier(state, new GridPos(2, 1)).Error);
        }

        [Test]
        public void NoCardInLevel_OrNoUsesLeft_IsRejected()
        {
            GameState none = GameState.CreateInitial(Level(0, 0, Targets));
            Assert.AreEqual(ActionError.BarrierNotAvailable, Sim.ApplyBarrier(none, new GridPos(2, 1)).Error);

            GameState used = Sim.ApplyBarrier(Start(Targets), new GridPos(2, 1)).NextState.WithNextRound();
            Assert.AreEqual(ActionError.NoBarrierUsesLeft, Sim.ApplyBarrier(used, new GridPos(0, 1)).Error);
        }

        // ---------------- Tiêu hao ----------------

        [Test]
        public void ValidUse_ConsumesOne_NoEnemyPhase_NoNewRound_EmitsEvent()
        {
            GameState start = Start(Targets);

            SupportResult r = Sim.ApplyBarrier(start, new GridPos(2, 1));

            Assert.IsTrue(r.Accepted);
            GameState s = r.NextState;
            Assert.AreEqual(0, s.BarrierUsesLeft);
            Assert.IsTrue(s.SupportUsedThisTurn);
            Assert.AreEqual(start.Round, s.Round);
            Assert.AreEqual(start.HeroPos, s.HeroPos);
            CollectionAssert.AreEqual(start.Enemies, s.Enemies, "Không chạy pha địch.");
            CollectionAssert.AreEqual(new[] { new GridPos(2, 1) }, s.Barriers);
            Assert.AreEqual(1, start.BarrierUsesLeft, "Trạng thái cũ bất biến.");
            Assert.IsEmpty(start.Barriers);
            Assert.AreEqual(RuleEventKind.BarrierPlaced, r.Events.Single().Kind);
            Assert.AreEqual(new GridPos(2, 1), r.Events.Single().To);
            Assert.IsTrue(Sim.Resolve(s, HeroAction.Move(Direction.W)).Accepted, "Hero tiếp tục hành động chính.");
        }

        [Test]
        public void OneSupportPerTurn_SharedWithStasis()
        {
            // X01 (2,2) trong tầm Stasis của Hero (1,1).
            string[] rows = { "....E", ".....", "..X..", ".H...", "....." };
            GameState start = Start(rows);

            GameState afterBarrier = Sim.ApplyBarrier(start, new GridPos(1, 0)).NextState;
            Assert.AreEqual(ActionError.SupportAlreadyUsedThisTurn, Sim.ApplyStasis(afterBarrier, "X1").Error);

            GameState afterStasis = Sim.ApplyStasis(start, "X1").NextState;
            Assert.AreEqual(ActionError.SupportAlreadyUsedThisTurn, Sim.ApplyBarrier(afterStasis, new GridPos(1, 0)).Error);
        }

        // ---------------- Chặn ----------------

        static readonly string[] Open = { "....E", ".....", ".....", ".....", "H...X" };

        [Test]
        public void BlocksHeroMove_Dash_AndDiagonalCorner()
        {
            GameState east = Sim.ApplyBarrier(Start(Open), new GridPos(1, 0)).NextState;
            Assert.AreEqual(ActionError.Barrier, Sim.Resolve(east, HeroAction.Move(Direction.E)).Error);
            TurnResult dash = Sim.Resolve(east, HeroAction.Dash(Direction.E));
            Assert.AreEqual(ActionError.Barrier, dash.Error, "Dash không nhảy qua vật cản.");
            Assert.AreEqual(1, dash.FailedDashStep);

            GameState north = Sim.ApplyBarrier(Start(Open), new GridPos(0, 1)).NextState;
            Assert.AreEqual(ActionError.CornerBlocked, Sim.Resolve(north, HeroAction.Move(Direction.NE)).Error, "Vật cản chặn lách góc như Tường.");

            // Vật cản ở bước 2 (trạng thái dựng tay): Dash cũng bị chặn, không dừng ở bước 1.
            GameState step2 = GameState.CreateInitial(Level(1, 0, Open)).WithBarrierPlaced(new GridPos(2, 0)).WithNextRound();
            TurnResult dash2 = Sim.Resolve(step2, HeroAction.Dash(Direction.E));
            Assert.AreEqual(ActionError.Barrier, dash2.Error);
            Assert.AreEqual(2, dash2.FailedDashStep);
        }

        // Hero (1,1); X01 (3,4) phía trên hàng tường, lối duy nhất là (1,2) — kề Hero.
        static readonly string[] Gate = { "...XE", ".....", "#.###", ".H...", "....." };

        [Test]
        public void BlocksEnemyPath_EnemyStays_ThenBarrierExpiresAtPhaseEnd()
        {
            GameState start = Start(Gate);
            TurnResult free = Sim.Resolve(start, HeroAction.Wait);
            Assert.IsTrue(free.Events.Any(e => e.Kind == RuleEventKind.EnemyMoved), "Không có vật cản: X01 tiến tới.");

            GameState blocked = Sim.ApplyBarrier(start, new GridPos(1, 2)).NextState;
            TurnResult r = Sim.Resolve(blocked, HeroAction.Wait);

            Assert.AreEqual(RuleEventKind.EnemyStayed, r.Events.Single(e => e.EnemyId == "X1").Kind, "Không còn đường: X01 đứng yên.");
            Assert.AreEqual(RuleEventKind.BarrierExpired, r.Events.Last().Kind);
            Assert.IsEmpty(r.NextState.Barriers, "Hết hiệu lực ở cuối pha địch.");
            CollectionAssert.AreEqual(new[] { new GridPos(1, 2) }, r.AfterHeroState.Barriers, "Còn nguyên trong hành động chính.");
            Assert.AreEqual(CellType.Floor, r.NextState.Level.GetCell(new GridPos(1, 2)), "Địa hình gốc không đổi.");
        }

        [Test]
        public void PersistsThroughWholeEnemyPhase_WithSeveralEnemies_ExpiresExactlyOnce()
        {
            GameState start = Start("1.2.E", ".....", "#.###", ".H...", ".....");
            GameState blocked = Sim.ApplyBarrier(start, new GridPos(1, 2)).NextState;

            TurnResult r = Sim.Resolve(blocked, HeroAction.Wait);

            Assert.AreEqual(RuleEventKind.EnemyStayed, r.Events.Single(e => e.EnemyId == "X1").Kind);
            Assert.AreEqual(RuleEventKind.EnemyStayed, r.Events.Single(e => e.EnemyId == "X2").Kind, "X sau cũng tính đường với vật cản.");
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == RuleEventKind.BarrierExpired));
            Assert.AreEqual(RuleEventKind.BarrierExpired, r.Events.Last().Kind, "Xóa sau khi mọi X đã hành động.");
            Assert.IsEmpty(r.NextState.Barriers);
            Assert.AreEqual(blocked.Round + 1, r.NextState.Round);
        }

        [Test]
        public void ExpiresEvenWhenEveryEnemyIsLocked()
        {
            GameState start = Start(Gate);
            GameState locked = start.WithEnemy(0, start.Enemies[0].WithLockPhases(1));
            GameState blocked = Sim.ApplyBarrier(locked, new GridPos(1, 0)).NextState;

            TurnResult r = Sim.Resolve(blocked, HeroAction.Wait);

            Assert.AreEqual(RuleEventKind.EnemySkipped, r.Events.Single(e => e.EnemyId == "X1").Kind);
            Assert.IsEmpty(r.NextState.Barriers);
            Assert.AreEqual(1, r.Events.Count(e => e.Kind == RuleEventKind.BarrierExpired));
        }

        [Test]
        public void InvalidMainActionOrTargets_DoNotShortenDuration_CardStaysSpent()
        {
            GameState placed = Sim.ApplyBarrier(Start(Open), new GridPos(1, 0)).NextState;

            TurnResult invalid = Sim.Resolve(placed, HeroAction.Move(Direction.E));
            Assert.IsFalse(invalid.Accepted);
            Assert.AreSame(placed, invalid.NextState, "Vật cản còn, thẻ đã tiêu hao, không hoàn tác.");
            Assert.IsFalse(Sim.ApplyBarrier(placed, new GridPos(0, 1)).Accepted);
            Sim.ApplyStasis(placed, "X1");
            CollectionAssert.AreEqual(new[] { new GridPos(1, 0) }, placed.Barriers);
            Assert.AreEqual(0, placed.BarrierUsesLeft);
        }

        [Test]
        public void HeroWinsDuringBarrier_GameEnds_NoExtraPhaseToClearIt()
        {
            GameState start = Start("....E", "...H.", ".....", ".....", "X....");
            GameState placed = Sim.ApplyBarrier(start, new GridPos(2, 3)).NextState;

            TurnResult r = Sim.Resolve(placed, HeroAction.Move(Direction.NE));

            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.IsFalse(r.EnemyPhaseRan);
            Assert.IsFalse(r.Events.Any(e => e.Kind == RuleEventKind.BarrierExpired));
        }

        [Test]
        public void BlocksLineOfSight_LikeAWall()
        {
            LevelData level = Level(1, 1, "....E", ".....", ".....", ".....", "H..X.");
            var barrier = new[] { new GridPos(1, 0) };
            Assert.IsTrue(GridRules.HasLineOfSight(level, new GridPos(0, 0), new GridPos(3, 0)));
            Assert.IsFalse(GridRules.HasLineOfSight(level, new GridPos(0, 0), new GridPos(3, 0), barrier));

            // Vật cản còn khi sang lượt (dựng tay) chặn đường nhìn của Stasis.
            GameState state = GameState.CreateInitial(level).WithBarrierPlaced(new GridPos(1, 0)).WithNextRound();
            Assert.AreEqual(ActionError.NoLineOfSight, Sim.ApplyStasis(state, "X1").Error);
            Assert.IsTrue(Sim.ApplyStasis(GameState.CreateInitial(level), "X1").Accepted);
        }

        [Test]
        public void EnemyPathfinder_TreatsBarrierAsWall()
        {
            LevelData level = Level(1, 0, Gate);
            Assert.IsNotNull(EnemyPathfinder.FindPathToHero(level, new GridPos(3, 4), new GridPos(1, 1), new GridPos[0]));
            Assert.IsNull(EnemyPathfinder.FindPathToHero(level, new GridPos(3, 4), new GridPos(1, 1), new GridPos[0], new[] { new GridPos(1, 2) }));
        }

        // ---------------- Trạng thái, preview, Restart ----------------

        [Test]
        public void StateEquality_AndHash_IncludeBarriersAndUses()
        {
            GameState start = Start(Open);
            GameState a = Sim.ApplyBarrier(start, new GridPos(1, 0)).NextState;
            GameState b = Sim.ApplyBarrier(start, new GridPos(0, 1)).NextState;
            GameState a2 = Sim.ApplyBarrier(start, new GridPos(1, 0)).NextState;

            Assert.AreEqual(a, a2);
            Assert.AreEqual(a.GetHashCode(), a2.GetHashCode());
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(start, a);
        }

        [Test]
        public void Preview_DoesNotChangeRealState_AndMatchesExecution()
        {
            var session = new GameSession(Level(1, 0, Gate), TestLevels.NewSimulation());
            GameState before = session.Current;

            Assert.IsTrue(session.PreviewBarrier(new GridPos(1, 2)).Accepted);
            Assert.AreSame(before, session.Current, "Preview Barrier không commit.");

            session.ApplyBarrier(new GridPos(1, 2));
            GameState placed = session.Current;
            TurnResult predicted = session.Preview(HeroAction.Wait);
            Assert.AreSame(placed, session.Current, "Preview hành động chính không commit.");
            Assert.AreEqual(predicted.NextState, session.Apply(HeroAction.Wait).NextState, "Preview tính vật cản và thời điểm hết hiệu lực giống thực thi.");
        }

        [Test]
        public void Restart_ClearsBarrierAndRestoresUses()
        {
            var session = new GameSession(Level(1, 0, Gate), TestLevels.NewSimulation());
            session.ApplyBarrier(new GridPos(1, 2));

            session.Restart();

            Assert.IsEmpty(session.Current.Barriers);
            Assert.AreEqual(1, session.Current.BarrierUsesLeft);
            Assert.IsFalse(session.Current.SupportUsedThisTurn);
        }

        // ---------------- Tactical ----------------

        static LevelData TacticalLevel(int barrier) => LevelParser.Parse(
            $"id: TACTICAL_BARRIER\nai: tactical\ndash: 2\nbarrier: {barrier}\ngrid:\n. . . . E\n. . . . .\n. . # . .\n. . . . X\nH . . . .");

        [Test]
        public void Tactical_ConsidersBarrierResponses_OnlyWhenHeroHasTheCard()
        {
            var with = new TacticalChasePolicy();
            TacticalDecision d = with.Decide(GameState.CreateInitial(TacticalLevel(1)), 0);
            Assert.IsFalse(d.UsedFallback);
            Assert.IsTrue(d.Candidates.Any(c => c.BarrierResponses > 0));

            var without = new TacticalChasePolicy();
            TacticalDecision n = without.Decide(GameState.CreateInitial(TacticalLevel(0)), 0);
            Assert.IsTrue(n.Candidates.All(c => c.BarrierResponses == 0));
        }

        [Test]
        public void Tactical_IsDeterministic_AndFallbackUsesBasicFromOriginalState()
        {
            GameState start = GameState.CreateInitial(TacticalLevel(1));
            Direction? first = new TacticalChasePolicy().Decide(start, 0).Chosen;
            Assert.AreEqual(first, new TacticalChasePolicy().Decide(start, 0).Chosen);

            TacticalDecision fallback = new TacticalChasePolicy(transitionBudget: 3).Decide(start, 0);
            Assert.IsTrue(fallback.UsedFallback);
            Assert.IsTrue(new BasicChasePolicy().TryChooseStep(start, 0, out Direction basic));
            Assert.AreEqual(basic, fallback.Chosen);
        }

        // ---------------- Dữ liệu level ----------------

        [Test]
        public void Parser_BarrierKey_DefaultsToZero_AndReadsSolutionPrefix()
        {
            LevelData old = LevelParser.Parse("id: OLD\ngrid:\n....E\n.....\n.....\n.....\nH...X");
            Assert.AreEqual(0, old.BarrierMaxUses);
            Assert.AreEqual(0, GameState.CreateInitial(old).BarrierUsesLeft);

            LevelData level = LevelParser.Parse("id: NEW\nbarrier: 1\nstasis: 1\ngrid:\n....E\n.....\n.....\n.....\nH...X\n" +
                                                "solution: BARRIER-N+E E\nsolution: STASIS+WAIT N");
            Assert.AreEqual(1, level.BarrierMaxUses);
            Assert.AreEqual(Direction.N, level.ReferenceBarrierDirections[0]);
            Assert.AreEqual(2, level.ReferenceSolutions.Count);
            Assert.AreEqual(Direction.N, level.ReferenceSolutions[0][0].BarrierDirection);
            Assert.AreEqual("X1", level.ReferenceSolutions[1][0].StasisTarget);
            Assert.AreEqual(HeroAction.Move(Direction.N).ToString(), level.ReferenceSolutions[1][1].Main.ToString());
        }

        [TestCase("barrier: two")]
        [TestCase("barrier: 10")]
        [TestCase("solution: BARRIER-Q+N")]
        [TestCase("solution: BARRIER-N")]
        public void Parser_RejectsInvalidBarrierData(string line)
        {
            Assert.Throws<LevelFormatException>(() =>
                LevelParser.Parse("id: BAD\ngrid:\n....E\n.....\n.....\n.....\nH...X\n" + line));
        }
    }
}
