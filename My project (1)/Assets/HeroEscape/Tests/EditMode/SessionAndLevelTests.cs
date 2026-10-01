using System.IO;
using HeroEscape.Core;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    public class GameSessionTests
    {
        [Test]
        public void InvalidAction_DoesNotChangeStateOrRevision()
        {
            var session = new GameSession(TestLevels.Parse("....E", ".....", ".....", ".....", "H...X"), TestLevels.NewSimulation());

            TurnResult r = session.Apply(HeroAction.Move(Direction.W));

            Assert.IsFalse(r.Accepted);
            Assert.AreSame(session.InitialState, session.Current);
            Assert.AreEqual(0, session.Revision);
        }

        [Test]
        public void Restart_RestoresInitialState_WhilePlayingAndAfterLoss()
        {
            var session = new GameSession(TestLevels.Parse("....E", ".....", ".....", ".H...", ".X..."), TestLevels.NewSimulation());
            GameState initial = session.Current;

            session.Apply(HeroAction.Move(Direction.N));
            Assert.AreNotEqual(initial, session.Current);
            session.Restart();
            Assert.AreEqual(initial, session.Current);

            session.Apply(HeroAction.Wait); // X01 bắt Hero
            Assert.AreEqual(GameOutcome.Lost, session.Current.Outcome);
            int revisionBefore = session.Revision;
            session.Restart();
            Assert.AreEqual(initial, session.Current);
            Assert.AreEqual(GameOutcome.Playing, session.Current.Outcome);
            Assert.AreEqual(1, session.Current.Round);
            Assert.Greater(session.Revision, revisionBefore);
        }

        [Test]
        public void Restart_AfterWin_AllowsPlayingAgain()
        {
            var session = new GameSession(TestLevels.Parse(".....", ".....", "...E.", "..H..", ".X..."), TestLevels.NewSimulation());

            session.Apply(HeroAction.Move(Direction.NE));
            Assert.AreEqual(GameOutcome.Won, session.Current.Outcome);
            session.Restart();

            Assert.AreEqual(session.InitialState, session.Current);
            Assert.IsTrue(session.Apply(HeroAction.Wait).Accepted);
        }
    }

    public class LevelParserTests
    {
        [Test]
        public void Parse_MapsTopRowToHighestY()
        {
            LevelData level = TestLevels.Parse(
                "H...E",
                ".....",
                ".....",
                ".....",
                "#...X");

            Assert.AreEqual(new GridPos(0, 4), level.HeroStart);
            Assert.AreEqual(new GridPos(4, 0), level.EnemyStart);
            Assert.AreEqual(new GridPos(4, 4), level.Exit);
            Assert.AreEqual(CellType.Wall, level.GetCell(new GridPos(0, 0)));
        }

        [Test]
        public void Parse_RejectsInvalidData()
        {
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", ".....", "..?..", ".....", "H...X"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", ".....", ".....", ".....", "....X"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", "X....", ".....", ".....", "H...X"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("....E", ".....", ".....", "....", "H...X"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse(".....", ".....", ".....", ".....", "H...X"));
            Assert.Throws<LevelFormatException>(() => TestLevels.Parse("...E", "....", "....", "H..X"));
        }
    }

    /// <summary>Kiểm chứng file level thật dùng trong scene chơi thử.</summary>
    public class M01TestLevelTests
    {
        static LevelData LoadTestLevel() => LevelParser.Parse(File.ReadAllText(TestLevels.TestLevelPath));

        [Test]
        public void TestLevel_HasExpectedLayout()
        {
            LevelData level = LoadTestLevel();

            Assert.AreEqual("M01_TEST", level.Id);
            Assert.AreEqual(5, level.Width);
            Assert.AreEqual(5, level.Height);
            Assert.AreEqual(new GridPos(0, 0), level.HeroStart);
            Assert.AreEqual(new GridPos(1, 2), level.EnemyStart);
            Assert.AreEqual(new GridPos(4, 4), level.Exit);
        }

        [Test]
        public void TestLevel_ReferenceSolution_ReachesExitAndWins()
        {
            LevelData level = LoadTestLevel();
            var session = new GameSession(level, TestLevels.NewSimulation());
            Assert.Greater(level.ReferenceSolution.Count, 0, "Level thiếu lời giải tham chiếu.");

            for (int i = 0; i < level.ReferenceSolution.Count; i++)
            {
                HeroAction action = level.ReferenceSolution[i];
                Assert.AreEqual(GameOutcome.Playing, session.Current.Outcome, $"Trận đã kết thúc trước bước {i + 1}.");
                TurnResult r = session.Apply(action);
                Assert.IsTrue(r.Accepted, $"Bước {i + 1} ({action}) bị từ chối: {r.Error}.");
            }

            Assert.AreEqual(GameOutcome.Won, session.Current.Outcome);
            Assert.AreEqual(level.Exit, session.Current.HeroPos);
        }

        [Test]
        public void TestLevel_RushingNorthFirst_IsCaught()
        {
            // Bẫy thiết kế: đi N ở nước đầu thì X01 bắt ngay.
            var session = new GameSession(LoadTestLevel(), TestLevels.NewSimulation());

            session.Apply(HeroAction.Move(Direction.N));

            Assert.AreEqual(GameOutcome.Lost, session.Current.Outcome);
        }
    }
}
