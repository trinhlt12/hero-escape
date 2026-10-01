using HeroEscape.Core;
using HeroEscape.Search;

namespace HeroEscape.Tests
{
    /// <summary>Dựng level nhỏ cho test từ các hàng lưới (hàng đầu là hàng trên cùng).</summary>
    static class TestLevels
    {
        public const string TestLevelPath = "Assets/HeroEscape/Data/Levels/M01_TestLevel.txt";

        public static LevelData Parse(params string[] rows) =>
            LevelParser.Parse("id: TEST\ngrid:\n" + string.Join("\n", rows));

        public static Simulation NewSimulation() => new Simulation(new BasicChasePolicy());

        public static GameState Start(params string[] rows) => GameState.CreateInitial(Parse(rows));
    }
}
