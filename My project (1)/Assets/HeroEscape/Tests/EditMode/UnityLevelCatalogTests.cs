using System.Collections.Generic;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Unity;
using NUnit.Framework;
using UnityEditor;

namespace HeroEscape.Tests
{
    /// <summary>
    /// Kiểm tra LevelCatalog asset và Build Settings (cần Unity Editor; chạy trong Test Runner, EditMode).
    /// Catalog và scene LevelSelect được tạo bởi menu Hero Escape > Milestone 01 > Dựng scene Level Select.
    /// </summary>
    public class UnityLevelCatalogTests
    {
        const string CatalogPath = "Assets/HeroEscape/Data/LevelCatalog.asset";
        const string LevelSelectScene = "Assets/HeroEscape/Scenes/LevelSelect.unity";
        const string GameplayScene = "Assets/HeroEscape/Scenes/M01_TestLevel.unity";

        static LevelCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null) Assert.Ignore("Chưa có LevelCatalog: chạy menu Hero Escape > Milestone 01 > Dựng scene Level Select.");
            return catalog;
        }

        [Test]
        public void Catalog_HasFiveLevels_WithStableIdsMatchingTheirLevelFiles()
        {
            LevelCatalog catalog = LoadCatalog();

            CollectionAssert.AreEqual(new[] { "Level 01", "Level 02", "Level 03", "Level 04", "Level 05" }, catalog.Levels.Select(e => e.DisplayName));
            CollectionAssert.AreEqual(new[] { "Basic Movement", "Dash", "Stasis", "Two Enemies", "Tactical Enemy" }, catalog.Levels.Select(e => e.Description));
            CollectionAssert.AllItemsAreUnique(catalog.Levels.Select(e => e.Id));

            var profiles = new List<AiProfile>();
            foreach (LevelEntry entry in catalog.Levels)
            {
                Assert.IsNotNull(entry.LevelFile, entry.Id);
                LevelData level = LevelParser.Parse(entry.LevelFile.text);
                Assert.AreEqual(entry.Id, level.Id, "ID trong catalog phải trùng 'id:' của file level.");
                profiles.Add(level.AiProfile);
            }

            CollectionAssert.AreEqual(new[] { AiProfile.Basic, AiProfile.Basic, AiProfile.Basic, AiProfile.Basic, AiProfile.Tactical }, profiles);
            Assert.AreEqual(0, catalog.DefaultIndex, "Mở thẳng gameplay scene thì vào Level 01.");
            Assert.AreEqual(2, catalog.IndexOf("M03_STASIS"));
            Assert.AreEqual(-1, catalog.IndexOf("Level 03"), "Không dùng tên hiển thị làm định danh.");
        }

        [Test]
        public void BuildSettings_LevelSelectComesBeforeGameplay_BothEnabled()
        {
            LoadCatalog();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int select = System.Array.FindIndex(scenes, s => s.path == LevelSelectScene);
            int gameplay = System.Array.FindIndex(scenes, s => s.path == GameplayScene);

            Assert.GreaterOrEqual(select, 0, "LevelSelect chưa có trong Build Settings.");
            Assert.Greater(gameplay, select);
            Assert.IsTrue(scenes[select].enabled && scenes[gameplay].enabled);
        }
    }
}
