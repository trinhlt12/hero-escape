using System.Reflection;
using HeroEscape.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace HeroEscape.Tests
{
    /// <summary>
    /// Trình bày tay bài của HandView (cần UnityEngine; chạy trong Unity Test Runner, EditMode).
    /// Ngoài Play Mode HandView đặt thẳng vào bố cục (không tween), nên kiểm tra được vị trí cuối, thứ tự vẽ và raycast.
    /// </summary>
    public class UnityHandViewTests
    {
        GameObject root;
        HandView hand;
        CardView hero, stasis, barrier;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("HandTest", typeof(RectTransform), typeof(Canvas));
            var handGo = new GameObject("Hand", typeof(RectTransform));
            handGo.transform.SetParent(root.transform, false);
            hand = handGo.AddComponent<HandView>();
            hero = MakeCard("HeroCard", CardKind.HeroDash);
            stasis = MakeCard("StasisCard", CardKind.SupportStasis);
            barrier = MakeCard("BarrierCard", CardKind.SupportBarrier);
            Set(hand, "cards", new[] { hero, stasis, barrier });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        CardView MakeCard(string name, CardKind kind)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(hand.transform, false);
            var image = go.AddComponent<Image>();
            var view = go.AddComponent<CardView>();
            Set(view, "kind", kind);
            Set(view, "border", image);
            return view;
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        static CardState State(CardKind kind, bool selected = false, int uses = 1) => new CardState(kind, uses, 2, selected, true);

        static Vector2 Pos(CardView card) => ((RectTransform)card.transform).anchoredPosition;

        [Test]
        public void HeroAndSupport_HeroCenteredOnTop_BothRaycastable()
        {
            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: true);

            Assert.IsTrue(hero.gameObject.activeSelf && stasis.gameObject.activeSelf);
            Assert.AreEqual(0f, Pos(hero).x, 1e-3);
            Assert.Greater(Pos(stasis).x, 0f);
            Assert.AreEqual(1.15f, hero.transform.localScale.x, 1e-3);
            Assert.Greater(hero.transform.GetSiblingIndex(), stasis.transform.GetSiblingIndex(), "Hero vẽ (và nhận chạm) trên phụ trợ.");
            Assert.IsTrue(hero.Group.blocksRaycasts && stasis.Group.blocksRaycasts);
        }

        [Test]
        public void SelectingSupport_LiftsItOnTop_OtherCardKeepsPlace()
        {
            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: true);
            Vector2 heroRest = Pos(hero);
            Vector2 stasisRest = Pos(stasis);
            float angle = stasis.transform.localEulerAngles.z;

            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis, selected: true) }, reset: false);

            Assert.Greater(Pos(stasis).y, stasisRest.y);
            Assert.AreEqual(angle, stasis.transform.localEulerAngles.z, 1e-3, "Giữ góc nghiêng khi nâng.");
            Assert.AreEqual(heroRest, Pos(hero));
            Assert.Greater(stasis.transform.GetSiblingIndex(), hero.transform.GetSiblingIndex(), "Thẻ đang chọn vẽ trên cùng.");

            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: false);
            Assert.AreEqual(stasisRest, Pos(stasis), "Bỏ chọn trả về đúng vị trí nghỉ chuẩn.");
        }

        [Test]
        public void HeroCardLeavesHand_RemainingSupportCentersStraight()
        {
            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: true);

            hand.Show(new[] { State(CardKind.SupportStasis) }, reset: false);

            Assert.IsFalse(hero.gameObject.activeSelf);
            Assert.IsFalse(hero.Group.blocksRaycasts, "Thẻ đã rời tay không còn nhận chạm.");
            Assert.IsFalse(hand.IsInHand(CardKind.HeroDash));
            Assert.AreEqual(0f, Pos(stasis).x, 1e-3);
            Assert.AreEqual(0f, stasis.transform.localEulerAngles.z, 1e-3);
        }

        [Test]
        public void EmptyHand_HasNoRaycastTargets()
        {
            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: true);

            hand.Show(new CardState[0], reset: false);

            Assert.IsNull(hand.GetComponent<Graphic>(), "Gốc tay bài không có graphic.");
            Assert.IsEmpty(hand.GetComponentsInChildren<Graphic>(false), "Không còn vùng vô hình chặn thao tác.");
        }

        [Test]
        public void Reset_RestoresCardsThatLeftTheHand()
        {
            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: true);
            Vector2 heroStart = Pos(hero);
            hand.Show(new[] { State(CardKind.SupportStasis, selected: true) }, reset: false);
            hand.Show(new CardState[0], reset: false);

            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: true);

            Assert.IsTrue(hero.gameObject.activeSelf && stasis.gameObject.activeSelf);
            Assert.AreEqual(heroStart, Pos(hero));
            Assert.AreEqual(1f, hero.Group.alpha, 1e-3);
        }

        [Test]
        public void UnavailableCard_IsDimmed_ButStillInHand()
        {
            hand.Show(new[] { new CardState(CardKind.SupportStasis, 1, 2, false, available: false) }, reset: true);

            Assert.IsTrue(stasis.gameObject.activeSelf);
            Assert.Less(stasis.Group.alpha, 1f);
        }

        [Test]
        public void ThreeCards_HeroCenter_SupportsOnBothSides_UsedCardLeaves()
        {
            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis), State(CardKind.SupportBarrier) }, reset: true);

            Assert.AreEqual(0f, Pos(hero).x, 1e-3);
            Assert.Greater(Pos(stasis).x, 0f);
            Assert.Less(Pos(barrier).x, 0f);
            Assert.AreEqual(hand.transform.childCount - 1, hero.transform.GetSiblingIndex(), "Hero vẽ trên các phụ trợ.");

            hand.Show(new[] { State(CardKind.HeroDash), State(CardKind.SupportStasis) }, reset: false);
            Assert.IsFalse(barrier.gameObject.activeSelf);
            Assert.AreEqual(0f, Pos(hero).x, 1e-3, "Hero vẫn ở giữa sau khi phụ trợ rời tay.");
        }
    }
}
