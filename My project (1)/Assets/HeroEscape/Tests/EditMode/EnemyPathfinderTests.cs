using System.Collections.Generic;
using HeroEscape.Core;
using HeroEscape.Search;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class EnemyPathfinderTests
    {
        [Test]
        public void Bfs_FindsShortestPath_WithEqualCostDiagonals()
        {
            GameState s = TestLevels.Start(
                "...H.",
                ".....",
                ".....",
                ".....",
                "X...E");

            List<GridPos> path = EnemyPathfinder.FindPathToHero(s.Level, s.EnemyPos, s.HeroPos);

            Assert.IsNotNull(path);
            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(s.HeroPos, path[path.Count - 1]);
        }

        [Test]
        public void Bfs_TieBreak_FollowsFixedDirectionOrder()
        {
            // Hai đường dài 2 đến Hero ở (2,0): qua NE (1,1) hoặc qua E (1,0). NE đứng trước E trong thứ tự.
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                ".....",
                "X.H..");
            var policy = new BasicChasePolicy();

            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(policy.TryChooseStep(s, out Direction d));
                Assert.AreEqual(Direction.NE, d, "Lần gọi " + i);
            }
        }

        [Test]
        public void Bfs_TieBreak_PrefersNorthOverNorthEast()
        {
            // Đến Hero ở (0,2): N-N hoặc NE-NW. N đứng trước NE.
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                "H....",
                ".....",
                "X....");

            Assert.IsTrue(new BasicChasePolicy().TryChooseStep(s, out Direction d));
            Assert.AreEqual(Direction.N, d);
        }

        [Test]
        public void Bfs_RoutesAroundWall_RespectingCornerRule()
        {
            // Bước chéo NE từ (0,0) tới (1,1) bị chặn góc bởi Tường (1,0) nên X01 phải đi N trước.
            GameState s = TestLevels.Start(
                "....E",
                ".....",
                ".....",
                "..H..",
                "X#...");

            Assert.IsTrue(new BasicChasePolicy().TryChooseStep(s, out Direction d));
            Assert.AreEqual(Direction.N, d);
        }

        [Test]
        public void Bfs_NeverPassesThroughExit_AndReturnsNullWhenOnlyRouteIsExit()
        {
            // Cột x = 1 toàn Tường trừ Cửa ở (1,2): X01 không có đường sang phía Hero.
            GameState s = TestLevels.Start(
                ".#...",
                ".#...",
                "XEH..",
                ".#...",
                ".#...");

            Assert.IsNull(EnemyPathfinder.FindPathToHero(s.Level, s.EnemyPos, s.HeroPos));
            Assert.IsFalse(new BasicChasePolicy().TryChooseStep(s, out _));
        }
    }
}
