using System.Linq;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class MultiEnemyTests
    {
        static readonly Simulation Sim = TestLevels.NewSimulation();

        /// <summary>Lưới 5x5, Dash 2, Stasis 1. '1'/'2' = X01 ID X1/X2 hành động theo thứ tự đó.</summary>
        static GameState Start(params string[] rows) =>
            GameState.CreateInitial(LevelParser.Parse("id: MULTI\ndash: 2\nstasis: 1\ngrid:\n" + string.Join("\n", rows)));

        static string[] EnemyEventOrder(TurnResult r) =>
            r.Events.Where(e => e.EnemyId != null).Select(e => e.EnemyId).ToArray();

        [Test]
        public void EnemiesAreOrderedBySpawnIndex_NotByGridPosition()
        {
            // X2 nằm ở hàng trên cùng (đọc trước trong lưới) nhưng vẫn hành động sau X1.
            GameState s = Start(
                "2...E",
                ".....",
                ".....",
                ".....",
                "H...1");

            CollectionAssert.AreEqual(new[] { "X1", "X2" }, s.Enemies.Select(e => e.Id));
            CollectionAssert.AreEqual(new[] { 1, 2 }, s.Enemies.Select(e => e.SpawnIndex));

            TurnResult r = Sim.Resolve(s, HeroAction.Wait);
            CollectionAssert.AreEqual(new[] { "X1", "X2" }, EnemyEventOrder(r));
            CollectionAssert.AreEqual(new[] { "X1", "X2" }, r.NextState.Enemies.Select(e => e.Id), "ID và thứ tự không đổi khi X di chuyển.");
        }

        [Test]
        public void SecondEnemy_RecomputesAfterFirst_AndCanUseVacatedCell()
        {
            // X2 (1,0) nằm trong túi, lối ra duy nhất là ô X1 (1,1). X1 đi chéo NE tới (2,2) để đuổi Hero (3,3).
            GameState s = Start(
                "....E",
                "...H.",
                ".....",
                "#1.##",
                "#2###");
            Assert.IsFalse(new Search.BasicChasePolicy().TryChooseStep(s, 1, out _),
                "Nếu tính từ đầu pha (X1 còn ở (1,1)), X2 không có bước nào.");

            TurnResult r = Sim.Resolve(s, HeroAction.Wait);

            Assert.AreEqual(new GridPos(2, 2), r.NextState.Enemies[0].Pos);
            Assert.AreEqual(new GridPos(1, 1), r.NextState.Enemies[1].Pos, "X2 tính lại sau X1 và đi vào ô X1 vừa rời.");
            CollectionAssert.AreEqual(new[] { "X1", "X2" }, EnemyEventOrder(r));
        }

        [Test]
        public void SecondEnemy_DoesNotPlanThroughFirstEnemysNewCell()
        {
            // Hành lang một ô: sau khi X1 tiến lên (2,0), X1 chặn đường duy nhất nên X2 đứng yên.
            GameState s = Start(
                "####E",
                "#####",
                "#####",
                "#####",
                "H..12");

            TurnResult r = Sim.Resolve(s, HeroAction.Wait);

            Assert.AreEqual(new GridPos(2, 0), r.NextState.Enemies[0].Pos);
            Assert.AreEqual(new GridPos(4, 0), r.NextState.Enemies[1].Pos);
            Assert.IsTrue(r.Events.Any(e => e.Kind == RuleEventKind.EnemyStayed && e.EnemyId == "X2"));
        }

        [Test]
        public void BlockedEnemyStays_OtherEnemyStillActs_NoOverlapOrPassThrough()
        {
            // X1 (3,0) đứng sau X2 (2,0) trong hành lang: không xuyên được X2 nên đứng yên; X2 vẫn đi.
            GameState s = Start(
                "####E",
                "#####",
                "#####",
                "#####",
                "H.21#");

            TurnResult r = Sim.Resolve(s, HeroAction.Wait);

            Assert.AreEqual(new GridPos(3, 0), r.NextState.Enemies[0].Pos);
            Assert.IsTrue(r.Events.Any(e => e.Kind == RuleEventKind.EnemyStayed && e.EnemyId == "X1"));
            Assert.AreEqual(new GridPos(1, 0), r.NextState.Enemies[1].Pos);
            Assert.AreNotEqual(r.NextState.Enemies[0].Pos, r.NextState.Enemies[1].Pos);
        }

        [Test]
        public void EnemyCannotEnterOrCutCornerAroundAnotherEnemy()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".12..",
                "H....");
            GridPos x1 = s.Enemies[0].Pos;

            Assert.AreEqual(StepCheck.Occupied,
                GridRules.CheckStep(s.Level, Mover.Enemy, x1, Direction.E, s.HeroPos, s.EnemyCells(0)), "Không vào ô X khác.");
            Assert.AreEqual(StepCheck.CornerBlocked,
                GridRules.CheckStep(s.Level, Mover.Enemy, x1, Direction.NE, s.HeroPos, s.EnemyCells(0)), "X khác chặn góc.");
            Assert.AreEqual(StepCheck.CornerBlocked,
                GridRules.CheckStep(s.Level, Mover.Enemy, x1, Direction.SE, s.HeroPos, s.EnemyCells(0)));
        }

        [Test]
        public void FirstEnemyCaptures_SecondEnemyIsNotProcessed()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "H12..");

            TurnResult r = Sim.Resolve(s, HeroAction.Wait);

            Assert.AreEqual(GameOutcome.Lost, r.NextState.Outcome);
            CollectionAssert.AreEqual(new[] { "X1" }, EnemyEventOrder(r), "X2 không được xử lý sau khi X1 bắt Hero.");
            Assert.AreEqual(new GridPos(2, 0), r.NextState.Enemies[1].Pos);
        }

        [Test]
        public void SecondEnemyCaptures_IsALoss()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                "2....",
                "H...1");

            TurnResult r = Sim.Resolve(s, HeroAction.Wait);

            Assert.AreEqual(GameOutcome.Lost, r.NextState.Outcome);
            CollectionAssert.AreEqual(new[] { "X1", "X2" }, EnemyEventOrder(r));
            Assert.AreEqual(r.NextState.HeroPos, r.NextState.Enemies[1].Pos);
        }

        [TestCase("X1", 0, 1)]
        [TestCase("X2", 1, 0)]
        public void LockingOneEnemy_DoesNotLockTheOther_AndExpiresAfterItsSkippedPhase(string targetId, int lockedIndex, int freeIndex)
        {
            GameState s = Start(
                "....E",
                ".....",
                "2....",
                ".....",
                "H..1.");

            SupportResult stasis = Sim.ApplyStasis(s, targetId);
            Assert.IsTrue(stasis.Accepted);
            Assert.AreEqual(1, stasis.NextState.Enemies[lockedIndex].LockPhases);
            Assert.AreEqual(0, stasis.NextState.Enemies[freeIndex].LockPhases, "Khóa thuộc riêng từng X.");

            TurnResult first = Sim.Resolve(stasis.NextState, HeroAction.Wait);
            Assert.AreEqual(s.Enemies[lockedIndex].Pos, first.NextState.Enemies[lockedIndex].Pos);
            Assert.AreNotEqual(s.Enemies[freeIndex].Pos, first.NextState.Enemies[freeIndex].Pos, "X không bị chọn vẫn hoạt động.");
            Assert.IsTrue(first.Events.Any(e => e.Kind == RuleEventKind.EnemySkipped && e.EnemyId == targetId));
            Assert.AreEqual(0, first.NextState.Enemies[lockedIndex].LockPhases, "Khóa hết sau đúng pha bị bỏ qua.");

            TurnResult second = Sim.Resolve(first.NextState, HeroAction.Wait);
            Assert.IsTrue(second.Events.Any(e => e.Kind == RuleEventKind.EnemyMoved && e.EnemyId == targetId), "Pha sau X hoạt động lại.");
        }

        [Test]
        public void Stasis_TargetsExactIdOrCell_NeverDefaultsToFirstOrNearest()
        {
            GameState s = Start(
                "....E",
                ".....",
                ".....",
                ".2...",
                "H..1.");

            GameState byId = Sim.ApplyStasis(s, "X1").NextState;
            Assert.IsTrue(byId.Enemies[0].Locked);
            Assert.IsFalse(byId.Enemies[1].Locked, "X2 gần Hero hơn nhưng không bị khóa.");

            GameState byCell = Sim.ApplyStasis(s, s.Enemies[1].Pos).NextState;
            Assert.IsFalse(byCell.Enemies[0].Locked);
            Assert.IsTrue(byCell.Enemies[1].Locked);

            Assert.AreEqual(ActionError.NoEnemyAtTarget, Sim.ApplyStasis(s, "X9").Error);
            Assert.AreEqual(ActionError.NoEnemyAtTarget, Sim.ApplyStasis(s, new GridPos(2, 2)).Error);
        }

        [Test]
        public void Hero_CannotMoveOrDashIntoOrThroughAnyEnemy_EvenLocked()
        {
            GameState s = Start(
                "....E",
                ".....",
                "1....",
                "2....",
                "H....");

            Assert.AreEqual(ActionError.OccupiedByEnemy, Sim.Resolve(s, HeroAction.Move(Direction.N)).Error);
            Assert.AreEqual(ActionError.OccupiedByEnemy, Sim.Resolve(s, HeroAction.Dash(Direction.N)).Error);
            Assert.AreEqual(ActionError.CornerBlocked, Sim.Resolve(s, HeroAction.Move(Direction.NE)).Error);

            GameState locked = Sim.ApplyStasis(s, "X2").NextState;
            TurnResult blocked = Sim.Resolve(locked, HeroAction.Dash(Direction.N));
            Assert.AreEqual(ActionError.OccupiedByEnemy, blocked.Error);
            Assert.AreEqual(1, blocked.FailedDashStep);
            Assert.AreSame(locked, blocked.NextState, "Dash sai không di chuyển một phần, không mất tài nguyên.");

            GameState secondStep = Start(
                "....E",
                ".....",
                "1....",
                ".....",
                "H...2");
            TurnResult step2 = Sim.Resolve(secondStep, HeroAction.Dash(Direction.N));
            Assert.AreEqual(ActionError.OccupiedByEnemy, step2.Error);
            Assert.AreEqual(2, step2.FailedDashStep);
        }

        [Test]
        public void HeroReachesExit_NoEnemyActs()
        {
            GameState s = Start(
                ".....",
                ".....",
                "..E..",
                ".2H..",
                "...1.");

            TurnResult r = Sim.Resolve(s, HeroAction.Move(Direction.N));

            Assert.AreEqual(GameOutcome.Won, r.NextState.Outcome);
            Assert.IsEmpty(EnemyEventOrder(r));
            CollectionAssert.AreEqual(s.Enemies, r.NextState.Enemies);
        }

        [Test]
        public void Restart_RestoresBothEnemiesIdsOrderResourcesAndLocks()
        {
            LevelData level = LevelParser.Parse("id: R\ndash: 2\nstasis: 1\ngrid:\n....E\n.....\n2....\n.....\nH..1.");
            var session = new GameSession(level, TestLevels.NewSimulation());

            session.ApplyStasis("X2");
            session.Apply(HeroAction.Dash(Direction.E));
            session.Apply(HeroAction.Wait);
            Assert.AreNotEqual(session.InitialState, session.Current);

            session.Restart();

            Assert.AreEqual(session.InitialState, session.Current);
            CollectionAssert.AreEqual(new[] { "X1", "X2" }, session.Current.Enemies.Select(e => e.Id));
            CollectionAssert.AreEqual(level.Enemies.Select(e => e.Cell), session.Current.Enemies.Select(e => e.Pos));
            Assert.IsTrue(session.Current.Enemies.All(e => !e.Locked));
            Assert.AreEqual(2, session.Current.DashUsesLeft);
            Assert.AreEqual(1, session.Current.StasisUsesLeft);
            Assert.AreEqual(GameOutcome.Playing, session.Current.Outcome);
            Assert.AreEqual(1, session.Current.Round);
        }

        [Test]
        public void OldSingleXFormat_LoadsAsOneElementList()
        {
            LevelData level = TestLevels.Parse("....E", ".....", ".....", ".....", "H...X");

            Assert.AreEqual(1, level.Enemies.Count);
            Assert.AreEqual("X1", level.Enemies[0].Id);
            Assert.AreEqual(1, level.Enemies[0].SpawnIndex);
            Assert.AreEqual(new GridPos(4, 0), level.EnemyStart);
        }

        [Test]
        public void InvalidEnemyData_IsReported_NotSilentlyFixed()
        {
            // Trùng thứ tự trong lưới.
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", ".....", ".....", "X....", "H...1"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", ".....", ".....", "X....", "H...X"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", ".....", ".....", "2....", "H...2"));

            // Kiểm tra ở LevelData (dữ liệu dựng bằng code).
            var cells = new CellType[25];
            cells[24] = CellType.Exit;
            cells[12] = CellType.Wall;
            var hero = new GridPos(0, 0);
            LevelData Make(params EnemySpawn[] enemies) =>
                new LevelData("BAD", 5, 5, cells, hero, enemies, null, 0, 0, null);

            Assert.Throws<LevelFormatException>(() => Make(new EnemySpawn("A", 1, new GridPos(1, 0)), new EnemySpawn("A", 2, new GridPos(2, 0))), "Trùng ID");
            Assert.Throws<LevelFormatException>(() => Make(new EnemySpawn("A", 1, new GridPos(1, 0)), new EnemySpawn("B", 1, new GridPos(2, 0))), "Trùng spawnIndex");
            Assert.Throws<LevelFormatException>(() => Make(new EnemySpawn("A", 1, new GridPos(1, 0)), new EnemySpawn("B", 2, new GridPos(1, 0))), "Trùng ô");
            Assert.Throws<LevelFormatException>(() => Make(new EnemySpawn("A", 1, new GridPos(2, 2))), "Trên Tường");
            Assert.Throws<LevelFormatException>(() => Make(new EnemySpawn("A", 1, new GridPos(4, 4))), "Trên Cửa");
            Assert.Throws<LevelFormatException>(() => Make(new EnemySpawn("A", 1, hero)), "Chồng Hero");
            Assert.Throws<LevelFormatException>(() => Make(), "Không có X01");
            Assert.DoesNotThrow(() => Make(new EnemySpawn("A", 2, new GridPos(1, 0)), new EnemySpawn("B", 1, new GridPos(2, 0))));
        }

        [Test]
        public void SolutionStasisTarget_MustBeExplicitWithSeveralEnemies()
        {
            const string grid = "id: S\ndash: 2\nstasis: 1\ngrid:\n....E\n.....\n2....\n.....\nH..1.\n";

            LevelData ok = LevelParser.Parse(grid + "solution: STASIS(X2)+N");
            Assert.AreEqual("X2", ok.ReferenceStasisTargets[0]);
            Assert.Throws<LevelFormatException>(() => LevelParser.Parse(grid + "solution: STASIS+N"));
            Assert.Throws<LevelFormatException>(() => LevelParser.Parse(grid + "solution: STASIS(X9)+N"));
        }
    }
}
