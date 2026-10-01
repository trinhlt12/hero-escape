using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class GridRulesTests
    {
        [Test]
        public void Hero_CanStepInAllEightDirections_OnOpenBoard()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                "..H..",
                ".....",
                "X....");

            foreach (Direction d in Directions.Order)
                Assert.AreEqual(StepCheck.Ok, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, d, s.EnemyPos), d.ToString());
        }

        [Test]
        public void Directions_NorthIsYPlusOne_AndOrderIsFixed()
        {
            Assert.AreEqual(new GridPos(0, 1), Direction.N.ToDelta());
            Assert.AreEqual(new GridPos(1, 1), Direction.NE.ToDelta());
            Assert.AreEqual(new GridPos(-1, -1), Direction.SW.ToDelta());
            CollectionAssert.AreEqual(
                new[] { Direction.N, Direction.NE, Direction.E, Direction.SE, Direction.S, Direction.SW, Direction.W, Direction.NW },
                Directions.Order);
        }

        [Test]
        public void Hero_BlockedByBoardEdge()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                "....X",
                "H....");

            foreach (Direction d in new[] { Direction.S, Direction.SW, Direction.W, Direction.NW, Direction.SE })
                Assert.AreEqual(StepCheck.OutOfBounds, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, d, s.EnemyPos), d.ToString());
        }

        [Test]
        public void Hero_BlockedByWall_AndByEnemyCell()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".#...",
                "XH...",
                ".....");

            Assert.AreEqual(StepCheck.Wall, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.N, s.EnemyPos));
            Assert.AreEqual(StepCheck.Occupied, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.W, s.EnemyPos));
        }

        [TestCase(Direction.NE, "..#..", "..H..")] // ô cạnh phía Bắc là Tường
        [TestCase(Direction.NE, ".....", "..H#.")] // ô cạnh phía Đông là Tường
        public void Hero_DiagonalBlocked_WhenOneOrthogonalSideIsWall(Direction direction, string rowAboveHero, string heroRow)
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                rowAboveHero,
                heroRow,
                "X....");

            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, direction, s.EnemyPos));
        }

        [Test]
        public void Hero_DiagonalBlocked_WhenEnemyIsOnOrthogonalSide()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                "..X..",
                "..H..",
                ".....");

            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.NE, s.EnemyPos));
            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.NW, s.EnemyPos));
            Assert.AreEqual(StepCheck.Ok, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.SE, s.EnemyPos));
        }

        [Test]
        public void Hero_CanPassCornerOfExit_AndEnterExit()
        {
            GameState s = TestLevels.Start(
                ".....",
                ".....",
                ".E...",
                ".H...",
                "....X");

            Assert.AreEqual(StepCheck.Ok, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.NE, s.EnemyPos));
            Assert.AreEqual(StepCheck.Ok, GridRules.CheckStep(s.Level, Mover.Hero, s.HeroPos, Direction.N, s.EnemyPos));
        }

        [Test]
        public void Enemy_CannotEnterExit_AndExitBlocksEnemyCorner()
        {
            GameState s = TestLevels.Start(
                ".....",
                ".....",
                ".E...",
                ".X...",
                "....H");

            Assert.AreEqual(StepCheck.ExitForbidden, GridRules.CheckStep(s.Level, Mover.Enemy, s.EnemyPos, Direction.N, s.HeroPos));
            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Enemy, s.EnemyPos, Direction.NE, s.HeroPos));
            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Enemy, s.EnemyPos, Direction.NW, s.HeroPos));
        }

        [Test]
        public void Enemy_CanStepOntoHero_ButHeroOnSideBlocksEnemyDiagonal()
        {
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                ".H...",
                ".X...");

            Assert.AreEqual(StepCheck.Ok, GridRules.CheckStep(s.Level, Mover.Enemy, s.EnemyPos, Direction.N, s.HeroPos));
            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Enemy, s.EnemyPos, Direction.NE, s.HeroPos));
            Assert.AreEqual(StepCheck.CornerBlocked, GridRules.CheckStep(s.Level, Mover.Enemy, s.EnemyPos, Direction.NW, s.HeroPos));
        }
    }
}
