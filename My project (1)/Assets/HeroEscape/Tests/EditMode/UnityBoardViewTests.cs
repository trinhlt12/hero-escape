using System.Reflection;
using HeroEscape.Core;
using HeroEscape.Unity;
using NUnit.Framework;
using UnityEngine;

namespace HeroEscape.Tests
{
    /// <summary>
    /// Kiểm tra hiển thị highlight của BoardView (cần UnityEngine; chạy trong Unity Test Runner, EditMode).
    /// Dùng sprite tạo tạm cùng kích thước rect với tile_floor (301x292) và tile_highlight (484x455).
    /// </summary>
    public class UnityBoardViewTests
    {
        GameObject root;
        BoardView board;
        Sprite floor, highlight, exit, actor;

        static Sprite MakeSprite(int width, int height) =>
            Sprite.Create(new Texture2D(width, height), new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);

        [SetUp]
        public void SetUp()
        {
            floor = MakeSprite(301, 292);
            highlight = MakeSprite(484, 455);
            exit = MakeSprite(320, 304);
            actor = MakeSprite(342, 422);

            root = new GameObject("BoardViewTest");
            board = root.AddComponent<BoardView>();
            Set("floorSprite", floor);
            Set("wallSprite", floor);
            Set("exitSprite", exit);
            Set("heroSprite", actor);
            Set("enemySprite", actor);
            Set("highlightSprite", highlight);

            board.Build(LevelParser.Parse("id: V\ngrid:\n....E\n.....\n.....\n.....\nH...X"));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        void Set(string field, Object value) =>
            typeof(BoardView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(board, value);

        SpriteRenderer Renderer(string name) => root.transform.Find("BoardContent/" + name).GetComponent<SpriteRenderer>();

        [Test]
        public void Highlight_ReplacesFloorSprite_KeepsCellFootprint_AndRestores()
        {
            var cell = new GridPos(1, 1);
            SpriteRenderer floorRenderer = Renderer($"Floor {cell}");
            Bounds before = floorRenderer.bounds;

            board.SetHighlights(new[] { cell });

            Assert.AreSame(highlight, board.FloorSpriteAt(cell), "Thay sprite sàn, không tint tile_floor.");
            Assert.AreEqual(BoardView.FloorOrder, floorRenderer.sortingOrder);
            Assert.AreEqual(before.center.x, floorRenderer.bounds.center.x, 1e-4f, "Tâm ô không lệch.");
            Assert.AreEqual(before.center.y, floorRenderer.bounds.center.y, 1e-4f);
            Assert.AreEqual(before.size.x, floorRenderer.bounds.size.x, 1e-3f, "Phần hình khớp kích thước tile_floor.");
            Assert.AreEqual(before.size.y, floorRenderer.bounds.size.y, 1e-3f);

            board.SetHighlights(new GridPos[0]);

            Assert.AreSame(floor, board.FloorSpriteAt(cell), "Hết highlight: khôi phục sprite sàn.");
            Assert.AreEqual(before.size.x, floorRenderer.bounds.size.x, 1e-4f);
            Assert.AreEqual(before.size.y, floorRenderer.bounds.size.y, 1e-4f);
        }

        [Test]
        public void HighlightUnderExitAndActors_StaysBelowThem()
        {
            board.SetHighlights(new[] { new GridPos(4, 4), new GridPos(4, 0), new GridPos(0, 0) });

            Assert.IsTrue(board.IsHighlighted(new GridPos(4, 4)));
            Assert.Less(Renderer("Floor (4,4)").sortingOrder, Renderer("Exit (4,4)").sortingOrder);
            Assert.Less(Renderer("Floor (4,0)").sortingOrder, Renderer("X01 X1").sortingOrder);
            Assert.Less(Renderer("Floor (0,0)").sortingOrder, Renderer("Hero H01").sortingOrder);
            Assert.AreSame(exit, Renderer("Exit (4,4)").sprite, "Cửa giữ nguyên sprite.");
        }

        [Test]
        public void ChangingHighlightSet_RestoresCellsThatLeftTheSet()
        {
            board.SetHighlights(new[] { new GridPos(0, 1), new GridPos(1, 1) });
            board.SetHighlights(new[] { new GridPos(1, 1), new GridPos(2, 2) });

            Assert.AreSame(floor, board.FloorSpriteAt(new GridPos(0, 1)));
            Assert.AreSame(highlight, board.FloorSpriteAt(new GridPos(1, 1)));
            Assert.AreSame(highlight, board.FloorSpriteAt(new GridPos(2, 2)));
        }

        [Test]
        public void Ghosts_AreBelowRealActors_AndHiddenAfterPreview()
        {
            LevelData level = LevelParser.Parse("id: V\ngrid:\n....E\n.....\n.....\n.....\nH...X");
            GameState actual = GameState.CreateInitial(level);
            GameState predicted = TestLevels.NewSimulation().Resolve(actual, HeroAction.Move(Direction.N)).NextState;

            board.ShowPreview(actual, predicted);

            SpriteRenderer heroGhost = Renderer("Hero H01 (preview)");
            Assert.IsTrue(heroGhost.gameObject.activeSelf);
            Assert.Less(heroGhost.sortingOrder, Renderer("Hero H01").sortingOrder);
            Assert.Less(heroGhost.color.a, 1f);

            board.HidePreview();
            Assert.IsFalse(heroGhost.gameObject.activeSelf);
        }
    }
}
