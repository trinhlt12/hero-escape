using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Tay bài ở đáy màn hình: xếp quạt, nâng thẻ đang chọn, loại thẻ hết lượt dùng và sắp xếp lại bằng DOTween.
    /// Chỉ trình bày danh sách <see cref="CardState"/> mà GameController đưa vào (suy ra từ GameState); không giữ bộ
    /// đếm, không quyết định luật. Animation chỉ thể hiện kết quả đã xác nhận; mọi vị trí tính lại từ bố cục chuẩn
    /// (<see cref="HandLayout"/>) nên đổi thẻ liên tục không làm thẻ trôi.
    /// Gốc tay bài không có graphic: chỉ các thẻ nhận raycast, tay rỗng không chặn thao tác nào.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandView : MonoBehaviour
    {
        [Tooltip("Mỗi loại thẻ một CardView (đặt sẵn trong scene, tắt khi không có trên tay).")]
        [SerializeField] CardView[] cards = Array.Empty<CardView>();

        [Header("Bố cục (đơn vị canvas 1080 x 1920)")]
        [SerializeField] float slotSpacing = 230f;
        [SerializeField] float heroExtraSpacing = 18f;
        [SerializeField] float tiltPerSlot = 8f;
        [SerializeField] float dropPerSlot = 16f;
        [Tooltip("Cạnh dưới thẻ khi nghỉ, so với đáy vùng an toàn. Âm = thụt xuống dưới mép.")]
        [SerializeField] float restY = -130f;
        [SerializeField, Min(0.1f)] float heroScale = 1.15f;
        [Tooltip("Độ nâng khi chọn, đo theo trục đứng của thẻ.")]
        [SerializeField, Min(0f)] float liftHeight = 145f;
        [SerializeField, Min(0.1f)] float selectedScale = 1f;
        [Tooltip("Độ trong suốt của thẻ còn trên tay nhưng luật chưa cho dùng (đã dùng phụ trợ lượt này, trận đã kết thúc).")]
        [SerializeField, Range(0f, 1f)] float unavailableAlpha = 0.5f;

        [Header("Animation (giây)")]
        [SerializeField, Min(0f)] float selectDuration = 0.17f;
        [SerializeField, Min(0f)] float reflowDuration = 0.2f;
        [SerializeField, Min(0f)] float removeDuration = 0.22f;
        [Tooltip("Thẻ hết lượt dùng tụt xuống bao nhiêu đơn vị khi biến mất.")]
        [SerializeField, Min(0f)] float removeDrop = 80f;
        [SerializeField, Range(0.1f, 1f)] float removeScale = 0.8f;
        [Tooltip("Bỏ qua lần chạm thẻ tiếp theo trong khoảng này (chống chạm lặp ngoài ý muốn).")]
        [SerializeField, Min(0f)] float pressCooldown = 0.08f;

        sealed class Slot
        {
            public CardView View;
            public bool InHand;
            public bool Removing;
            public bool Selected;
            public bool HasTarget;
            public CardPose Target;
            public float Alpha;
            public Sequence Tween;
        }

        readonly Dictionary<CardKind, Slot> slots = new Dictionary<CardKind, Slot>();
        bool initialized;
        bool needsSnap = true;
        int generation; // tăng khi reset/disable: callback của tween cũ không được tác động vào phiên mới
        int lastPressFrame = -1;
        float lastPressTime = float.NegativeInfinity;

        /// <summary>Người chơi chạm/click một thẻ đang trên tay. Chưa qua kiểm tra luật.</summary>
        public event Action<CardKind> CardPressed;

        public HandLayoutSettings Settings => new HandLayoutSettings
        {
            SlotSpacing = slotSpacing,
            HeroExtraSpacing = heroExtraSpacing,
            TiltPerSlot = tiltPerSlot,
            DropPerSlot = dropPerSlot,
            RestY = restY,
            HeroScale = heroScale,
            LiftHeight = liftHeight,
            SelectedScale = selectedScale,
        };

        /// <summary>Thẻ loại <paramref name="kind"/> đang trên tay (không tính thẻ đang chạy animation biến mất).</summary>
        public bool IsInHand(CardKind kind) => slots.TryGetValue(kind, out Slot slot) && slot.InHand;

        void Awake() => EnsureSlots();

        void OnDisable()
        {
            KillAll();
            needsSnap = true;
        }

        void OnDestroy()
        {
            KillAll();
            foreach (Slot slot in slots.Values)
                if (slot.View != null) slot.View.Clicked -= OnCardClicked;
        }

        /// <summary>
        /// Trình bày tay bài. <paramref name="reset"/> = vào màn hoặc Restart: hủy mọi tween/callback cũ, đặt thẳng vào
        /// bố cục, không animation. Ngược lại: thẻ mới rời tay chạy animation biến mất rồi các thẻ còn lại dời về bố cục
        /// mới; thẻ đổi trạng thái chọn nâng/hạ; thẻ khác giữ chỗ.
        /// </summary>
        public void Show(IReadOnlyList<CardState> hand, bool reset)
        {
            EnsureSlots();
            if (reset || needsSnap)
            {
                KillAll();
                needsSnap = false;
                reset = true;
            }

            bool animate = !reset && Application.isPlaying;

            var present = new HashSet<CardKind>();
            foreach (CardState card in hand) present.Add(card.Kind);

            bool removalStarted = false;
            foreach (KeyValuePair<CardKind, Slot> entry in slots)
            {
                Slot slot = entry.Value;
                if (present.Contains(entry.Key)) continue;
                if (reset || !slot.InHand)
                {
                    if (reset || !slot.Removing) Hide(slot);
                }
                else if (animate && removeDuration > 0f)
                {
                    StartRemoval(slot);
                    removalStarted = true;
                }
                else
                {
                    Hide(slot);
                }
            }

            CardPose[] poses = HandLayout.Compute(hand, Settings);
            for (int i = 0; i < hand.Count; i++)
            {
                if (!slots.TryGetValue(hand[i].Kind, out Slot slot))
                {
                    Debug.LogError($"[HandView] No CardView for {hand[i].Kind}. Rebuild the scene: Hero Escape > Milestone 01 > Dựng scene chơi thử.", this);
                    continue;
                }

                bool appearing = !slot.InHand;
                bool selectionChanged = !appearing && slot.Selected != hand[i].Selected;
                if (appearing)
                {
                    KillTween(slot);
                    slot.View.gameObject.SetActive(true);
                    slot.InHand = true;
                    slot.Removing = false;
                }

                slot.Selected = hand[i].Selected;
                slot.View.Show(hand[i]);
                slot.View.SetRaycastable(true);

                CardPose pose = poses[i];
                float alpha = hand[i].Available ? 1f : unavailableAlpha;
                if (!animate || appearing)
                {
                    KillTween(slot);
                    Apply(slot.View, pose, alpha);
                }
                else if (!slot.HasTarget || !slot.Target.Equals(pose) || slot.Alpha != alpha)
                {
                    // Chỉ đổi lựa chọn: nâng/hạ ngay. Còn lại (sắp xếp sau khi có thẻ rời tay): đợi thẻ kia biến mất.
                    float duration = selectionChanged ? selectDuration : reflowDuration;
                    float delay = removalStarted && !selectionChanged ? removeDuration : 0f;
                    TweenTo(slot, pose, alpha, duration, delay);
                }

                slot.Target = pose;
                slot.HasTarget = true;
                slot.Alpha = alpha;
            }

            foreach (int index in HandLayout.DrawOrder(hand))
                if (slots.TryGetValue(hand[index].Kind, out Slot slot)) slot.View.transform.SetAsLastSibling();
        }

        void EnsureSlots()
        {
            if (initialized) return;
            initialized = true;
            foreach (CardView view in cards)
            {
                if (view == null) continue;
                if (slots.ContainsKey(view.Kind))
                {
                    Debug.LogError($"[HandView] Two CardViews for {view.Kind}; ignoring '{view.name}'.", this);
                    continue;
                }

                slots.Add(view.Kind, new Slot { View = view });
                view.Clicked += OnCardClicked;
                view.SetRaycastable(false);
                view.gameObject.SetActive(false);
            }
        }

        void OnCardClicked(CardView view, PointerEventData _)
        {
            if (!slots.TryGetValue(view.Kind, out Slot slot) || !slot.InHand || slot.Removing) return;
            // Nhiều ngón tay chạm cùng frame, hoặc chạm lặp quá sát: chỉ nhận lần đầu.
            if (Time.frameCount == lastPressFrame || Time.unscaledTime - lastPressTime < pressCooldown) return;
            lastPressFrame = Time.frameCount;
            lastPressTime = Time.unscaledTime;
            CardPressed?.Invoke(view.Kind);
        }

        void StartRemoval(Slot slot)
        {
            KillTween(slot);
            slot.InHand = false;
            slot.Removing = true;
            slot.Selected = false;
            slot.HasTarget = false;
            slot.View.SetRaycastable(false); // đang biến mất: không còn tương tác được

            RectTransform rect = slot.View.Rect;
            CanvasGroup group = slot.View.Group;
            Vector2 end = rect.anchoredPosition + new Vector2(0f, -removeDrop);
            int owner = generation;

            Sequence sequence = NewSequence(slot.View);
            sequence.Join(DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, end, removeDuration).SetEase(Ease.InQuad));
            sequence.Join(rect.DOScale(rect.localScale.x * removeScale, removeDuration).SetEase(Ease.InQuad));
            sequence.Join(DOTween.To(() => group.alpha, a => group.alpha = a, 0f, removeDuration));
            sequence.OnComplete(() =>
            {
                if (owner != generation || slot.View == null) return;
                slot.Removing = false;
                slot.Tween = null;
                slot.View.gameObject.SetActive(false);
            });
            slot.Tween = sequence;
        }

        void TweenTo(Slot slot, CardPose pose, float alpha, float duration, float delay)
        {
            KillTween(slot);
            if (duration <= 0f && delay <= 0f)
            {
                Apply(slot.View, pose, alpha);
                return;
            }

            RectTransform rect = slot.View.Rect;
            CanvasGroup group = slot.View.Group;
            Sequence sequence = NewSequence(slot.View);
            sequence.Join(DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, new Vector2(pose.X, pose.Y), duration)
                .SetEase(Ease.OutCubic));
            sequence.Join(rect.DOLocalRotate(new Vector3(0f, 0f, pose.Angle), duration).SetEase(Ease.OutCubic));
            sequence.Join(rect.DOScale(pose.Scale, duration).SetEase(Ease.OutCubic));
            sequence.Join(DOTween.To(() => group.alpha, a => group.alpha = a, alpha, duration));
            if (delay > 0f) sequence.PrependInterval(delay);
            slot.Tween = sequence;
        }

        static Sequence NewSequence(CardView view) =>
            DOTween.Sequence().SetTarget(view).SetLink(view.gameObject).SetUpdate(true);

        static void Apply(CardView view, CardPose pose, float alpha)
        {
            RectTransform rect = view.Rect;
            rect.anchoredPosition = new Vector2(pose.X, pose.Y);
            rect.localEulerAngles = new Vector3(0f, 0f, pose.Angle);
            rect.localScale = new Vector3(pose.Scale, pose.Scale, 1f);
            view.Group.alpha = alpha;
        }

        void Hide(Slot slot)
        {
            KillTween(slot);
            slot.InHand = false;
            slot.Removing = false;
            slot.Selected = false;
            slot.HasTarget = false;
            if (slot.View == null) return;
            slot.View.SetRaycastable(false);
            slot.View.gameObject.SetActive(false);
        }

        static void KillTween(Slot slot)
        {
            if (slot.Tween != null && slot.Tween.IsActive()) slot.Tween.Kill();
            slot.Tween = null;
        }

        /// <summary>Hủy mọi tween và vô hiệu callback đang chờ (Restart, đổi màn, disable, destroy).</summary>
        void KillAll()
        {
            generation++;
            foreach (Slot slot in slots.Values)
            {
                KillTween(slot);
                slot.Removing = false;
            }
        }
    }
}
