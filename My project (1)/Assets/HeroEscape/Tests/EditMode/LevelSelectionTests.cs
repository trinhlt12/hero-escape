using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Unity;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class LevelSelectionTests
    {
        [SetUp]
        public void SetUp() => LevelSelection.Clear();

        [TearDown]
        public void TearDown() => LevelSelection.Clear();

        [Test]
        public void PendingId_IsReadOnce_ThenCleared()
        {
            LevelSelection.Request("M05_TACTICAL");

            Assert.AreEqual("M05_TACTICAL", LevelSelection.TakePending());
            Assert.IsNull(LevelSelection.TakePending(), "Lần đọc sau (ví dụ Restart scene/Play lại) không còn lựa chọn cũ.");
        }

        [Test]
        public void NoSelection_MeansDefaultLevel()
        {
            Assert.IsNull(LevelSelection.TakePending());
        }

        [Test]
        public void Clear_RemovesSelection_AsDoneAtStartOfEachPlay()
        {
            LevelSelection.Request("M03_STASIS");
            LevelSelection.Clear();

            Assert.IsNull(LevelSelection.PendingLevelId);
        }

        /// <summary>
        /// Mỗi lần chọn màn, gameplay tạo GameSession mới từ file level: vị trí, số X, profile AI, Dash/Stasis
        /// đúng dữ liệu; không khóa, đang chơi, vòng 1.
        /// </summary>
        [TestCase("Assets/HeroEscape/Data/Levels/M01_TestLevel.txt", "M01_TEST", 1, AiProfile.Basic, 0, 0)]
        [TestCase("Assets/HeroEscape/Data/Levels/M02_DashLevel.txt", "M02_DASH", 1, AiProfile.Basic, 2, 0)]
        [TestCase("Assets/HeroEscape/Data/Levels/M03_StasisLevel.txt", "M03_STASIS", 1, AiProfile.Basic, 2, 1)]
        [TestCase("Assets/HeroEscape/Data/Levels/M04_TwoEnemiesLevel.txt", "M04_TWO_ENEMIES", 2, AiProfile.Basic, 2, 1)]
        [TestCase("Assets/HeroEscape/Data/Levels/M05_TacticalLevel.txt", "M05_TACTICAL", 1, AiProfile.Tactical, 2, 1)]
        public void SelectingALevel_StartsAFreshSessionFromItsData(string path, string id, int enemies, AiProfile profile, int dash, int stasis)
        {
            LevelData level = LevelParser.Parse(File.ReadAllText(path));
            var session = new GameSession(level, Search.EnemyPolicies.CreateSimulation(level));
            GameState s = session.Current;

            Assert.AreEqual(id, level.Id);
            Assert.AreEqual(profile, level.AiProfile);
            Assert.AreEqual(enemies, s.Enemies.Count);
            Assert.AreEqual(level.HeroStart, s.HeroPos);
            CollectionAssert.AreEqual(level.Enemies.Select(e => e.Cell), s.Enemies.Select(e => e.Pos));
            Assert.AreEqual(dash, s.DashUsesLeft);
            Assert.AreEqual(stasis, s.StasisUsesLeft);
            Assert.IsTrue(s.Enemies.All(e => !e.Locked));
            Assert.IsFalse(s.SupportUsedThisTurn);
            Assert.AreEqual(GameOutcome.Playing, s.Outcome);
            Assert.AreEqual(1, s.Round);
            Assert.AreEqual(0, session.Revision);
        }
    }
}
