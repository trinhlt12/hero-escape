using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Search;
using HeroEscape.Unity;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>
    /// Level 06 (Barrier): cấu hình, các lời giải phát lại bằng Simulation thật, và các điều kiện thiết kế được duyệt hết
    /// trạng thái bằng <see cref="LevelReachability"/> (X01 Basic).
    /// </summary>
    public class Level06Tests
    {
        const string Path = "Assets/HeroEscape/Data/Levels/M06_BarrierLevel.txt";

        static LevelData Load() => LevelParser.Parse(File.ReadAllText(Path));
        static Simulation Sim(LevelData level) => EnemyPolicies.CreateSimulation(level);

        [Test]
        public void Config_6x6_OneBasicEnemy_Dash2_Stasis1_Barrier1()
        {
            LevelData level = Load();

            Assert.AreEqual("M06_BARRIER", level.Id);
            Assert.AreEqual(6, level.Width);
            Assert.AreEqual(6, level.Height);
            Assert.AreEqual(1, level.Enemies.Count);
            Assert.AreEqual(AiProfile.Basic, level.AiProfile);
            Assert.AreEqual(2, level.DashMaxUses);
            Assert.AreEqual(1, level.StasisMaxUses);
            Assert.AreEqual(1, level.BarrierMaxUses);
            Assert.AreEqual(new GridPos(2, 0), level.HeroStart);
            Assert.AreEqual(new GridPos(5, 3), level.EnemyStart);
            Assert.AreEqual(new GridPos(4, 5), level.Exit);
        }

        [Test]
        public void EverySolution_WinsWithRealRules()
        {
            LevelData level = Load();
            Assert.GreaterOrEqual(level.ReferenceSolutions.Count, 2);

            foreach (IReadOnlyList<SolutionTurn> solution in level.ReferenceSolutions)
            {
                GameState end = LevelReachability.Replay(GameState.CreateInitial(level), Sim(level), LevelReachability.Turns(solution));
                Assert.IsNotNull(end, $"Có bước bị từ chối: {string.Join(" ", solution)}");
                Assert.AreEqual(GameOutcome.Won, end.Outcome, string.Join(" ", solution));
            }
        }

        [Test]
        public void Solutions_UseResourcesDifferently_AndTakeDifferentRoutes()
        {
            LevelData level = Load();
            IReadOnlyList<SolutionTurn> barrier = level.ReferenceSolutions[0];
            IReadOnlyList<SolutionTurn> stasis = level.ReferenceSolutions[1];

            Assert.AreEqual(1, barrier.Count(t => t.BarrierDirection.HasValue));
            Assert.AreEqual(0, barrier.Count(t => t.StasisTarget != null), "Lời giải 1: Barrier + Dash, không Stasis.");
            Assert.AreEqual(1, stasis.Count(t => t.StasisTarget != null));
            Assert.AreEqual(0, stasis.Count(t => t.BarrierDirection.HasValue), "Lời giải 2: Stasis + Dash, không Barrier.");

            HashSet<GridPos> routeA = HeroCells(level, barrier), routeB = HeroCells(level, stasis);
            int shared = routeA.Intersect(routeB).Count();
            Assert.Less(shared * 2, routeA.Union(routeB).Count(), "Hai lời giải đi qua phần lớn các ô khác nhau.");

            if (level.ReferenceSolutions.Count > 2)
                Assert.AreEqual(0, level.ReferenceSolutions[2].Count(t => t.Main.Kind == HeroActionKind.Dash), "Lời giải 3: không Dash.");
        }

        static HashSet<GridPos> HeroCells(LevelData level, IReadOnlyList<SolutionTurn> solution)
        {
            Simulation sim = Sim(level);
            GameState s = GameState.CreateInitial(level);
            var cells = new HashSet<GridPos> { s.HeroPos };
            foreach (PlannedTurn turn in LevelReachability.Turns(solution))
            {
                s = LevelReachability.Replay(s, sim, new[] { turn });
                cells.Add(s.HeroPos);
            }

            return cells;
        }

        [TestCase(0, 0, 0, ReachVerdict.Unsolvable, TestName = "NoCards_Unsolvable")]
        [TestCase(2, 0, 0, ReachVerdict.Unsolvable, TestName = "DashOnly_Unsolvable")]
        [TestCase(0, 1, 0, ReachVerdict.Unsolvable, TestName = "StasisOnly_Unsolvable")]
        [TestCase(0, 0, 1, ReachVerdict.Unsolvable, TestName = "BarrierOnly_Unsolvable")]
        [TestCase(2, 0, 1, ReachVerdict.Solved, TestName = "DashAndBarrier_Solvable")]
        [TestCase(2, 1, 0, ReachVerdict.Solved, TestName = "DashAndStasis_Solvable")]
        [TestCase(0, 1, 1, ReachVerdict.Solved, TestName = "StasisAndBarrier_Solvable")]
        public void DesignConditions_ExhaustiveSearch(int dash, int stasis, int barrier, ReachVerdict expected)
        {
            LevelData level = Load();
            LevelReachability.Result r = LevelReachability.Explore(GameState.CreateInitial(level), Sim(level),
                maxDashes: dash, maxStasis: stasis, maxBarrier: barrier);

            Assert.AreEqual(expected, r.Verdict, $"Đã duyệt {r.StatesVisited} trạng thái.");
        }

        /// <summary>
        /// Lời giải 1, lượt 5: Hero (0,3) ở góc trái, X01 (1,2) chéo ngay dưới. Cùng hành động Wait:
        /// không Barrier thì X01 lách chéo bắt Hero; Barrier ở (1,3) chặn góc chéo nên X01 phải xuống (0,2).
        /// Trong các ô đặt hợp lệ lúc đó, chỉ ô E (1,3) cứu được Hero.
        /// </summary>
        [Test]
        public void Barrier_ChangesTheEnemyResponse_AtTheKeyMoment()
        {
            LevelData level = Load();
            Simulation sim = Sim(level);
            List<PlannedTurn> turns = LevelReachability.Turns(level.ReferenceSolutions[0]);
            int barrierTurn = turns.FindIndex(t => t.BarrierDirection.HasValue);
            GameState before = LevelReachability.Replay(GameState.CreateInitial(level), sim, turns.Take(barrierTurn));

            Assert.AreEqual(new GridPos(0, 3), before.HeroPos);
            Assert.AreEqual(new GridPos(1, 2), before.Enemies[0].Pos);

            TurnResult without = sim.Resolve(before, HeroAction.Wait);
            Assert.AreEqual(GameOutcome.Lost, without.NextState.Outcome, "Không Barrier: X01 bắt Hero bằng bước chéo.");

            GameState placed = sim.ApplyBarrier(before, new GridPos(1, 3)).NextState;
            TurnResult with = sim.Resolve(placed, HeroAction.Wait);
            Assert.AreEqual(GameOutcome.Playing, with.NextState.Outcome);
            Assert.AreEqual(new GridPos(0, 2), with.NextState.Enemies[0].Pos, "Barrier chặn góc chéo, X01 phải đi vòng.");
            Assert.IsEmpty(with.NextState.Barriers);

            var legal = new List<Direction>();
            var survivors = new List<Direction>();
            var winners = new List<Direction>();
            foreach (Direction d in Directions.Order)
            {
                SupportResult r = sim.ApplyBarrier(before, before.HeroPos + d.ToDelta());
                if (!r.Accepted) continue;
                legal.Add(d);
                GameState after = sim.Resolve(r.NextState, HeroAction.Wait).NextState;
                if (after.Outcome == GameOutcome.Lost) continue;
                survivors.Add(d);
                if (LevelReachability.Explore(after, sim, maxDashes: 2).Verdict == ReachVerdict.Solved) winners.Add(d);
            }

            CollectionAssert.AreEqual(new[] { Direction.N, Direction.E, Direction.S }, legal, "Người chơi có nhiều ô để chọn.");
            CollectionAssert.AreEqual(new[] { Direction.E, Direction.S }, survivors, "Ô N không chặn được bước chéo: bị bắt ngay.");
            CollectionAssert.AreEqual(new[] { Direction.E }, winners, "Trong chiến thuật Barrier + Dash (không Stasis): ô S sống qua pha này nhưng hết đường thắng; chỉ ô E dẫn tới Cửa.");
        }

        [Test]
        public void BarrierTiming_Matters_PlacingAtTheStartDoesNotWin()
        {
            LevelData level = Load();
            Simulation sim = Sim(level);
            GameState start = GameState.CreateInitial(level);

            // Đặt Barrier ngay lượt 1 ở bất kỳ ô nào: không còn Stasis thì Dash còn lại không đủ để thắng.
            foreach (Direction d in Directions.Order)
            {
                SupportResult r = sim.ApplyBarrier(start, start.HeroPos + d.ToDelta());
                if (!r.Accepted) continue;
                foreach (HeroAction action in LevelReachability.AllMainActions)
                {
                    TurnResult t = sim.Resolve(r.NextState, action);
                    if (!t.Accepted || t.NextState.Outcome != GameOutcome.Playing) continue;
                    Assert.AreEqual(ReachVerdict.Unsolvable,
                        LevelReachability.Explore(t.NextState, sim, maxDashes: 2).Verdict, $"BARRIER-{d}+{action}");
                }
            }
        }

        [Test]
        public void Solutions_WinByTappingCardsAndHighlightedTiles()
        {
            LevelData level = Load();
            foreach (IReadOnlyList<SolutionTurn> solution in level.ReferenceSolutions)
            {
                var input = new TurnInputController(new GameSession(level, Sim(level)));
                foreach (SolutionTurn turn in solution)
                {
                    if (turn.StasisTarget != null)
                    {
                        CardHand.Press(input, CardKind.SupportStasis);
                        GridPos enemy = input.Session.Current.Enemies[input.Session.Current.IndexOfEnemy(turn.StasisTarget)].Pos;
                        CollectionAssert.Contains(input.HighlightedCells, enemy);
                        input.PressBoard(false, enemy);
                    }

                    if (turn.BarrierDirection.HasValue)
                    {
                        CardHand.Press(input, CardKind.SupportBarrier);
                        GridPos cell = input.Session.Current.HeroPos + turn.BarrierDirection.Value.ToDelta();
                        CollectionAssert.Contains(input.HighlightedCells, cell, "Ô đặt Barrier hợp lệ được highlight.");
                        input.PressBoard(false, cell);
                        Assert.AreEqual(InputMode.Normal, input.Mode);
                    }

                    if (turn.Main.Kind == HeroActionKind.Wait)
                    {
                        Assert.IsTrue(input.PressWait().Acted);
                        continue;
                    }

                    if (turn.Main.Kind == HeroActionKind.Dash) CardHand.Press(input, CardKind.HeroDash);
                    input.Session.TryGetDestination(turn.Main, out GridPos destination, out _, out _);
                    CollectionAssert.Contains(input.HighlightedCells, destination);
                    Assert.IsTrue(input.PressBoard(false, destination).Acted, $"{turn} trong {string.Join(" ", solution)}");
                }

                Assert.AreEqual(GameOutcome.Won, input.Session.Current.Outcome, string.Join(" ", solution));
            }
        }
    }
}
