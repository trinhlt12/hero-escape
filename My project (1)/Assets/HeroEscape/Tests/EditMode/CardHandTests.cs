using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeroEscape.Core;
using HeroEscape.Unity;
using NUnit.Framework;

namespace HeroEscape.Tests
{
    /// <summary>
    /// Tích hợp tay bài với luồng input/gameplay (không cần UnityEngine): tay bài suy ra từ GameState, chọn/đổi/bỏ chọn
    /// chỉ đổi chế độ, tiêu hao chỉ khi Core chấp nhận, thẻ hết lượt rời tay, Restart/đổi màn khôi phục đúng.
    /// </summary>
    public class CardHandTests
    {
        const string Levels = "Assets/HeroEscape/Data/Levels/";

        static TurnInputController Open(string file)
        {
            LevelData level = LevelParser.Parse(File.ReadAllText(Levels + file));
            return new TurnInputController(new GameSession(level, Search.EnemyPolicies.CreateSimulation(level)));
        }

        static TurnInputController NewInput(int dash, int stasis, params string[] rows) =>
            new TurnInputController(new GameSession(
                LevelParser.Parse($"id: HAND\ndash: {dash}\nstasis: {stasis}\ngrid:\n" + string.Join("\n", rows)),
                TestLevels.NewSimulation()));

        // Hero (0,0), X01 (2,2) trong tầm Stasis, Cửa (4,4) xa.
        static TurnInputController Arena(int dash = 2, int stasis = 1) => NewInput(dash, stasis,
            "....E",
            ".....",
            "..X..",
            ".....",
            "H....");

        static string Describe(IEnumerable<CardState> cards) =>
            string.Join(",", cards.Select(c => $"{c.Kind}:{c.UsesLeft}/{c.MaxUses}{(c.Selected ? "*" : "")}"));

        // ---------------- Dữ liệu ----------------

        [TestCase("M01_TestLevel.txt", "")]
        [TestCase("M02_DashLevel.txt", "HeroDash:2/2")]
        [TestCase("M03_StasisLevel.txt", "HeroDash:2/2,SupportStasis:1/1")]
        [TestCase("M04_TwoEnemiesLevel.txt", "HeroDash:2/2,SupportStasis:1/1")]
        [TestCase("M05_TacticalLevel.txt", "HeroDash:2/2,SupportStasis:1/1")]
        [TestCase("M06_BarrierLevel.txt", "HeroDash:2/2,SupportStasis:1/1,SupportBarrier:1/1")]
        public void LevelGrantsExactlyItsConfiguredCards(string file, string expected)
        {
            TurnInputController input = Open(file);

            List<CardState> hand = CardHand.From(input);

            Assert.AreEqual(expected, Describe(hand), "Một thẻ Hero cho mọi lần Dash; không thẻ giả, không tài nguyên thêm.");
            Assert.AreEqual(expected != "", CardHand.LevelHasCards(input.Session.Level));
            Assert.IsTrue(hand.All(c => c.Available && !c.Selected));
        }

        // ---------------- Chọn / đổi / bỏ chọn ----------------

        [Test]
        public void SelectSwitchDeselect_OnlyChangesMode_NeverGameplayState()
        {
            TurnInputController input = Arena();
            GameState before = input.Session.Current;
            int revision = input.Session.Revision;

            CardHand.Press(input, CardKind.HeroDash);
            Assert.AreEqual("HeroDash:2/2*,SupportStasis:1/1", Describe(CardHand.From(input)));
            Assert.AreEqual(InputMode.ChoosingDashTarget, input.Mode);

            CardHand.Press(input, CardKind.SupportStasis);
            Assert.AreEqual("HeroDash:2/2,SupportStasis:1/1*", Describe(CardHand.From(input)), "Chỉ một thẻ được chọn.");
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode);

            CardHand.Press(input, CardKind.SupportStasis);
            Assert.AreEqual("HeroDash:2/2,SupportStasis:1/1", Describe(CardHand.From(input)), "Chạm lại thẻ đang chọn → về Move.");
            Assert.AreEqual(InputMode.Normal, input.Mode);

            Assert.AreSame(before, input.Session.Current, "Không tiêu hao, không tăng lượt, không chạy pha địch.");
            Assert.AreEqual(revision, input.Session.Revision);
        }

        [Test]
        public void Escape_DeselectsCard_WithoutCost()
        {
            TurnInputController input = Arena();
            GameState before = input.Session.Current;
            CardHand.Press(input, CardKind.HeroDash);

            input.Cancel();

            Assert.IsFalse(CardHand.From(input).Any(c => c.Selected));
            Assert.AreSame(before, input.Session.Current);
        }

        [Test]
        public void RapidSwitching_EndsInOneConsistentSelection_AndMatchingHighlights()
        {
            TurnInputController input = Arena();
            GameState before = input.Session.Current;
            var presses = new[]
            {
                CardKind.HeroDash, CardKind.SupportStasis, CardKind.HeroDash, CardKind.HeroDash, CardKind.SupportStasis,
                CardKind.HeroDash, CardKind.SupportStasis, CardKind.SupportStasis, CardKind.HeroDash,
            };

            foreach (CardKind kind in presses)
            {
                CardHand.Press(input, kind);
                List<CardState> hand = CardHand.From(input);
                Assert.LessOrEqual(hand.Count(c => c.Selected), 1);

                // Highlight luôn khớp đúng chế độ của thẻ đang chọn (cùng bộ kiểm tra của Core).
                IEnumerable<GridPos> expected = input.Mode switch
                {
                    InputMode.ChoosingDashTarget => input.Session.LegalTargets(HeroActionKind.Dash).Select(t => t.destination),
                    InputMode.ChoosingStasisTarget => input.Session.Current.Enemies.Where(e => input.Session.PreviewStasis(e.Id).Accepted).Select(e => e.Pos),
                    _ => input.Session.LegalTargets(HeroActionKind.Move).Select(t => t.destination),
                };
                CollectionAssert.AreEqual(expected.ToList(), input.HighlightedCells.ToList());
            }

            Assert.AreEqual(InputMode.ChoosingDashTarget, input.Mode, "Lần chạm cuối là Dash (đang ở Move).");
            Assert.AreSame(before, input.Session.Current);
        }

        // ---------------- Mục tiêu và tiêu hao ----------------

        [Test]
        public void InvalidTargets_DoNotConsume_AndKeepSelection()
        {
            TurnInputController input = Arena();
            GameState before = input.Session.Current;

            CardHand.Press(input, CardKind.HeroDash);
            input.PressBoard(false, new GridPos(1, 0)); // ô kề: không phải ô đích Dash
            input.PressBoard(false, new GridPos(3, 3)); // không thẳng hàng 2 ô
            input.PressBoard(false, null);              // ngoài bàn
            Assert.AreEqual(InputMode.ChoosingDashTarget, input.Mode);
            Assert.AreEqual("HeroDash:2/2*,SupportStasis:1/1", Describe(CardHand.From(input)));

            CardHand.Press(input, CardKind.SupportStasis);
            input.PressBoard(false, new GridPos(1, 1)); // ô trống
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode);
            Assert.AreEqual("HeroDash:2/2,SupportStasis:1/1*", Describe(CardHand.From(input)));

            Assert.AreSame(before, input.Session.Current, "Không trừ tài nguyên, không kết thúc lượt.");
        }

        [Test]
        public void ClickOnCardArea_DoesNotReachBoard()
        {
            TurnInputController input = Arena();
            CardHand.Press(input, CardKind.HeroDash);
            GameState before = input.Session.Current;

            // BoardInput báo pointerOverUi = true khi con trỏ nằm trên thẻ (thẻ là graphic nhận raycast).
            InputResponse r = input.PressBoard(pointerOverUi: true, cell: new GridPos(2, 0));

            Assert.IsFalse(r.Acted);
            Assert.AreSame(before, input.Session.Current);
            Assert.AreEqual(InputMode.ChoosingDashTarget, input.Mode);
        }

        [Test]
        public void ValidDash_ConsumesExactlyOne_AndDeselects()
        {
            TurnInputController input = Arena();
            CardHand.Press(input, CardKind.HeroDash);

            InputResponse r = input.PressBoard(false, new GridPos(2, 0));

            Assert.IsTrue(r.Acted);
            Assert.AreEqual(1, input.Session.Current.DashUsesLeft);
            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual("HeroDash:1/2,SupportStasis:1/1", Describe(CardHand.From(input)));

            // Chạm lại ô đó sau khi đã về Move không phải Dash nữa.
            input.PressBoard(false, new GridPos(2, 0));
            Assert.AreEqual(1, input.Session.Current.DashUsesLeft);
        }

        [Test]
        public void LastDash_RemovesHeroCard_HeroStillMovesAndWaits()
        {
            TurnInputController input = NewInput(1, 0,
                "....E",
                ".....",
                ".....",
                ".....",
                "H...X");
            CardHand.Press(input, CardKind.HeroDash);
            Assert.IsTrue(input.PressBoard(false, new GridPos(0, 2)).Acted);

            Assert.AreEqual("", Describe(CardHand.From(input)), "Hết Dash: thẻ Hero rời tay.");
            Assert.AreEqual(InputMode.Normal, input.Mode);

            CardHand.Press(input, CardKind.HeroDash); // chạm trễ vào thẻ đang biến mất (nếu lọt qua) bị từ chối
            Assert.AreEqual(InputMode.Normal, input.Mode);

            Assert.IsTrue(input.PressBoard(false, new GridPos(0, 3)).Acted, "Hero trên bàn vẫn Move được.");
            Assert.IsTrue(input.PressWait().Acted, "Và vẫn Wait được.");
        }

        [Test]
        public void Stasis_RemovesSupportCard_NoEnemyPhase_MainActionStillAvailable()
        {
            TurnInputController input = Arena();
            GameState before = input.Session.Current;
            CardHand.Press(input, CardKind.SupportStasis);

            InputResponse r = input.PressBoard(false, new GridPos(2, 2));

            Assert.IsFalse(r.Acted, "Stasis là phụ trợ: không phải hành động chính.");
            GameState after = input.Session.Current;
            Assert.AreEqual(before.Round, after.Round, "Không tăng lượt.");
            Assert.AreEqual(before.EnemyPos, after.EnemyPos, "Không chạy pha địch.");
            Assert.AreEqual(0, after.StasisUsesLeft);
            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual("HeroDash:2/2", Describe(CardHand.From(input)));

            Assert.IsTrue(input.PressBoard(false, new GridPos(1, 0)).Acted, "Người chơi tiếp tục hành động chính.");
        }

        [Test]
        public void SupportUsedThisTurn_CardStaysButUnavailable_UntilNextRound()
        {
            TurnInputController input = Arena(dash: 0, stasis: 2);
            CardHand.Press(input, CardKind.SupportStasis);
            input.PressBoard(false, new GridPos(2, 2));

            CardState stasis = CardHand.From(input).Single();
            Assert.AreEqual(1, stasis.UsesLeft);
            Assert.IsFalse(stasis.Available, "Mỗi lượt chỉ một phụ trợ.");
            CardHand.Press(input, CardKind.SupportStasis);
            Assert.AreEqual(InputMode.Normal, input.Mode);

            input.PressWait();
            Assert.IsTrue(CardHand.From(input).Single().Available);
        }

        [Test]
        public void EnemyPhaseAndGameOver_BlockCards()
        {
            TurnInputController input = Arena();
            input.Busy = true;
            CardHand.Press(input, CardKind.HeroDash);
            CardHand.Press(input, CardKind.SupportStasis);
            Assert.AreEqual(InputMode.Normal, input.Mode, "Không dùng thẻ trong pha địch.");
            input.Busy = false;

            // Thua: X01 ngay cạnh, Hero chờ.
            TurnInputController lose = NewInput(2, 1, "....E", ".....", ".....", ".X...", "H....");
            lose.PressWait();
            Assert.AreEqual(GameOutcome.Lost, lose.Session.Current.Outcome);
            CardHand.Press(lose, CardKind.HeroDash);
            Assert.AreEqual(InputMode.Normal, lose.Mode, "Sau thắng/thua không bật lại thao tác.");
            Assert.IsTrue(CardHand.From(lose).All(c => !c.Available && !c.Selected));
        }

        // ---------------- Restart, đổi màn ----------------

        [Test]
        public void Restart_RestoresLevelHand_AndClearsSelection()
        {
            TurnInputController input = Open("M03_StasisLevel.txt");
            List<CardState> initial = CardHand.From(input);
            CardHand.Press(input, CardKind.HeroDash);
            Assert.IsTrue(input.PressBoard(false, input.Session.LegalTargets(HeroActionKind.Dash)[0].destination).Acted);
            CardHand.Press(input, CardKind.SupportStasis);

            input.Restart();

            Assert.AreEqual(Describe(initial), Describe(CardHand.From(input)));
            Assert.AreEqual(InputMode.Normal, input.Mode);
        }

        [Test]
        public void SwitchingLevel_DoesNotCarryCardsOrCounters()
        {
            TurnInputController level3 = Open("M03_StasisLevel.txt");
            CardHand.Press(level3, CardKind.HeroDash);
            level3.PressBoard(false, level3.Session.LegalTargets(HeroActionKind.Dash)[0].destination);

            // GameController tạo GameSession + TurnInputController mới cho màn kế tiếp.
            Assert.AreEqual("HeroDash:2/2", Describe(CardHand.From(Open("M02_DashLevel.txt"))));
            Assert.AreEqual("", Describe(CardHand.From(Open("M01_TestLevel.txt"))));
        }

        // ---------------- Preview khớp thực thi ----------------

        [TestCase("M02_DashLevel.txt")]
        [TestCase("M05_TacticalLevel.txt")]
        public void DashCardPreview_MatchesExecution_ForEveryTarget(string file)
        {
            foreach ((HeroAction _, GridPos target) in Open(file).Session.LegalTargets(HeroActionKind.Dash))
            {
                TurnInputController input = Open(file);
                CardHand.Press(input, CardKind.HeroDash);
                TurnPreview preview = input.PreviewBoardHover(target);

                InputResponse r = input.PressBoard(false, target);

                Assert.AreEqual(PreviewKind.MainAction, preview.Kind);
                Assert.AreEqual(preview.Turn.NextState, r.Turn.NextState, $"Preview và thực thi cùng luật (đích {target}).");
            }
        }

        // ---------------- Barrier ----------------

        static TurnInputController BarrierArena() => new TurnInputController(new GameSession(
            LevelParser.Parse("id: HAND_BARRIER\ndash: 2\nstasis: 1\nbarrier: 1\ngrid:\n....E\n.....\n.#X..\n.H...\n....."),
            TestLevels.NewSimulation()));

        [Test]
        public void BarrierCard_HighlightsExactlyTheLegalTiles_InvalidTapKeepsSelection()
        {
            TurnInputController input = BarrierArena();
            GameState before = input.Session.Current;
            CardHand.Press(input, CardKind.SupportBarrier);

            var expected = Directions.Order.Select(d => before.HeroPos + d.ToDelta())
                .Where(c => input.Session.PreviewBarrier(c).Accepted).ToList();
            CollectionAssert.AreEqual(expected, input.HighlightedCells);
            CollectionAssert.DoesNotContain(input.HighlightedCells, new GridPos(1, 2), "Tường.");
            CollectionAssert.DoesNotContain(input.HighlightedCells, new GridPos(2, 2), "X01.");
            CollectionAssert.DoesNotContain(input.HighlightedCells, new GridPos(0, 2), "Góc chéo bị Tường chặn.");

            input.PressBoard(false, new GridPos(3, 1)); // không kề Hero
            input.PressBoard(false, new GridPos(1, 2)); // Tường
            Assert.AreEqual(InputMode.ChoosingBarrierTarget, input.Mode);
            Assert.AreSame(before, input.Session.Current, "Mục tiêu sai không tiêu hao.");
        }

        [Test]
        public void BarrierHover_PreviewsOnlyThePlacement_NoEnemyPhase()
        {
            TurnInputController input = BarrierArena();
            CardHand.Press(input, CardKind.SupportBarrier);

            TurnPreview preview = input.PreviewBoardHover(new GridPos(1, 0));

            Assert.AreEqual(PreviewKind.BarrierTarget, preview.Kind);
            Assert.AreEqual(new GridPos(1, 0), preview.BarrierCell);
            Assert.IsNull(preview.Turn, "Không đoán hành động chính, không chạy pha địch.");
            Assert.AreEqual(PreviewKind.None, input.PreviewBoardHover(new GridPos(1, 2)).Kind);
            Assert.IsEmpty(input.Session.Current.Barriers);
        }

        [Test]
        public void BarrierPlaced_CardLeaves_HeroStillActs_PreviewSeesBarrier()
        {
            TurnInputController input = BarrierArena();
            CardHand.Press(input, CardKind.SupportBarrier);

            InputResponse r = input.PressBoard(false, new GridPos(1, 0));

            Assert.IsFalse(r.Acted, "Phụ trợ không phải hành động chính.");
            Assert.AreEqual(UiText.BarrierPlaced, r.Feedback);
            Assert.AreEqual(InputMode.Normal, input.Mode);
            Assert.AreEqual("HeroDash:2/2,SupportStasis:1/1", Describe(CardHand.From(input)));
            Assert.IsTrue(CardHand.From(input).All(c => c.IsHero ? c.Available : !c.Available), "Đã dùng phụ trợ lượt này.");
            CollectionAssert.DoesNotContain(input.HighlightedCells, new GridPos(1, 0), "Highlight Move tính vật cản.");

            TurnPreview wait = input.PreviewWaitHover();
            Assert.AreEqual(wait.Turn.NextState, input.PressWait().Turn.NextState, "Preview khớp thực thi khi có vật cản.");
            Assert.IsEmpty(input.Session.Current.Barriers);
        }

        [Test]
        public void BarrierAndStasis_SwitchLikeOtherCards_RestartRestoresBoth()
        {
            TurnInputController input = BarrierArena();
            CardHand.Press(input, CardKind.SupportBarrier);
            CardHand.Press(input, CardKind.SupportStasis);
            Assert.AreEqual(InputMode.ChoosingStasisTarget, input.Mode);
            CardHand.Press(input, CardKind.SupportBarrier);
            Assert.AreEqual(InputMode.ChoosingBarrierTarget, input.Mode);
            input.Cancel();
            Assert.AreEqual(InputMode.Normal, input.Mode);

            CardHand.Press(input, CardKind.SupportBarrier);
            input.PressBoard(false, new GridPos(1, 0));
            input.Restart();

            Assert.AreEqual("HeroDash:2/2,SupportStasis:1/1,SupportBarrier:1/1", Describe(CardHand.From(input)));
            Assert.IsEmpty(input.Session.Current.Barriers);
        }

        [Test]
        public void Layout_HeroCentered_StasisRight_BarrierLeft()
        {
            CardPose[] p = HandLayout.Compute(new[] { Card(CardKind.HeroDash), Card(CardKind.SupportStasis), Card(CardKind.SupportBarrier) },
                HandLayoutSettings.Default);

            Assert.AreEqual(0f, p[0].X);
            Assert.Greater(p[1].X, 0f);
            Assert.Less(p[2].X, 0f);
            Assert.AreEqual(-p[1].X, p[2].X, 1e-4);
            Assert.AreEqual(-p[1].Angle, p[2].Angle, 1e-4, "Hai phụ trợ nghiêng ra ngoài đối xứng.");
        }

        // ---------------- Bố cục ----------------

        static CardState Card(CardKind kind, bool selected = false) => new CardState(kind, 1, 1, selected, true);

        [Test]
        public void Layout_HeroAlone_CenteredStraightAndLarger()
        {
            CardPose[] p = HandLayout.Compute(new[] { Card(CardKind.HeroDash) }, HandLayoutSettings.Default);

            Assert.AreEqual(0f, p[0].X);
            Assert.AreEqual(0f, p[0].Angle);
            Assert.AreEqual(1.15f, p[0].Scale, 1e-4);
            Assert.Less(p[0].Y, 0f, "Thẻ nghỉ thụt một phần xuống dưới mép.");
        }

        [Test]
        public void Layout_HeroAndSupport_HeroStaysCentered_SupportBesideTiltedOutward()
        {
            CardPose[] p = HandLayout.Compute(new[] { Card(CardKind.HeroDash), Card(CardKind.SupportStasis) }, HandLayoutSettings.Default);

            Assert.AreEqual(0f, p[0].X, "Không dịch Hero để căn giữa cả cặp.");
            Assert.AreEqual(0f, p[0].Angle);
            Assert.Greater(p[1].X, 0f);
            Assert.Less(p[1].Angle, 0f, "Thẻ bên phải nghiêng ra ngoài (chiều kim đồng hồ).");
            Assert.AreEqual(1f, p[1].Scale);
            Assert.Less(p[1].Y, p[0].Y, "Thẻ hai bên thấp hơn theo đường cong quạt.");
        }

        [Test]
        public void Layout_SingleSupport_CenteredAndStraight()
        {
            CardPose[] p = HandLayout.Compute(new[] { Card(CardKind.SupportStasis) }, HandLayoutSettings.Default);

            Assert.AreEqual(new CardPose(0f, HandLayoutSettings.Default.RestY, 0f, 1f), p[0]);
        }

        [Test]
        public void Layout_EmptyHand_HasNoCards()
        {
            Assert.IsEmpty(HandLayout.Compute(new CardState[0], HandLayoutSettings.Default));
            Assert.IsEmpty(HandLayout.DrawOrder(new CardState[0]));
        }

        [Test]
        public void Layout_SupportsWithoutHero_AreCenteredSymmetrically()
        {
            CardPose[] p = HandLayout.Compute(new[] { Card(CardKind.SupportStasis), Card(CardKind.SupportStasis) }, HandLayoutSettings.Default);

            Assert.AreEqual(-p[0].X, p[1].X, 1e-4);
            Assert.AreEqual(-p[0].Angle, p[1].Angle, 1e-4);
            Assert.AreEqual(p[0].Y, p[1].Y, 1e-4);
        }

        [Test]
        public void Layout_SelectedSupport_LiftsAlongItsTilt_HeroLiftsStraightUp()
        {
            HandLayoutSettings s = HandLayoutSettings.Default;
            CardPose[] rest = HandLayout.Compute(new[] { Card(CardKind.HeroDash), Card(CardKind.SupportStasis) }, s);
            CardPose[] support = HandLayout.Compute(new[] { Card(CardKind.HeroDash), Card(CardKind.SupportStasis, selected: true) }, s);
            CardPose[] hero = HandLayout.Compute(new[] { Card(CardKind.HeroDash, selected: true), Card(CardKind.SupportStasis) }, s);

            Assert.AreEqual(rest[1].Angle, support[1].Angle, "Phụ trợ giữ góc nghiêng khi nâng.");
            Assert.Greater(support[1].Y, rest[1].Y);
            Assert.Greater(support[1].X, rest[1].X, "Nâng theo trục của thẻ nghiêng phải.");
            Assert.AreEqual(rest[0], support[0], "Thẻ khác giữ vị trí khi chỉ đổi lựa chọn.");

            Assert.AreEqual(0f, hero[0].X, 1e-4);
            Assert.AreEqual(rest[0].Y + s.LiftHeight, hero[0].Y, 1e-3);
            Assert.AreEqual(rest[1], hero[1]);
            Assert.GreaterOrEqual(hero[0].Y, 0f, "Thẻ Hero nâng lên hiện trọn (không còn thụt dưới mép).");
        }

        [Test]
        public void Layout_IsPure_RepeatedSwitchingNeverAccumulates()
        {
            CardState[] restHand = { Card(CardKind.HeroDash), Card(CardKind.SupportStasis) };
            CardPose[] expected = HandLayout.Compute(restHand, HandLayoutSettings.Default);

            for (int i = 0; i < 20; i++)
            {
                HandLayout.Compute(new[] { Card(CardKind.HeroDash, i % 2 == 0), Card(CardKind.SupportStasis, i % 2 == 1) }, HandLayoutSettings.Default);
                CollectionAssert.AreEqual(expected, HandLayout.Compute(restHand, HandLayoutSettings.Default));
            }
        }

        [Test]
        public void DrawOrder_HeroAboveSupports_SelectedOnTop()
        {
            CardState[] rest = { Card(CardKind.HeroDash), Card(CardKind.SupportStasis) };
            CardState[] supportSelected = { Card(CardKind.HeroDash), Card(CardKind.SupportStasis, selected: true) };

            CollectionAssert.AreEqual(new[] { 1, 0 }, HandLayout.DrawOrder(rest));
            CollectionAssert.AreEqual(new[] { 0, 1 }, HandLayout.DrawOrder(supportSelected));
        }
    }
}
