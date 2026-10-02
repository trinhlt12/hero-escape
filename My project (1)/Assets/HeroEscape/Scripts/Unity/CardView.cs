using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Một thẻ trên tay bằng uGUI: nền/viền màu phẳng, nhãn loại, tên, số lần dùng còn lại và mô tả.
    /// Chỉ hiển thị và báo chạm; không biết luật, không giữ bộ đếm. HandView quyết định vị trí và animation.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class CardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] CardKind kind;
        [Tooltip("Ảnh nền ngoài cùng, đóng vai viền. Là graphic duy nhất nhận raycast của thẻ.")]
        [SerializeField] Image border;
        [SerializeField] Text tagText;
        [SerializeField] Text titleText;
        [SerializeField] Text usesText;
        [SerializeField] Text descriptionText;
        [SerializeField] Color borderColor = Color.white;
        [Tooltip("Màu viền khi thẻ đang được chọn.")]
        [SerializeField] Color selectedBorderColor = new Color32(0xE8, 0xB8, 0x49, 0xFF);

        RectTransform rect;
        CanvasGroup group;

        /// <summary>Chạm/click hợp lệ lên thẻ (đã qua kiểm tra nút chuột). HandView lọc tiếp.</summary>
        internal event Action<CardView, PointerEventData> Clicked;

        public CardKind Kind => kind;
        public RectTransform Rect => rect != null ? rect : rect = (RectTransform)transform;
        public CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

        void Awake()
        {
            if (tagText != null) tagText.text = kind == CardKind.HeroDash ? UiText.HeroCardTag : UiText.SupportCardTag;
            if (titleText != null) titleText.text = Title(kind);
            if (descriptionText != null) descriptionText.text = Description(kind);
        }

        public static string Title(CardKind kind) =>
            kind == CardKind.HeroDash ? UiText.DashCardName : kind == CardKind.SupportStasis ? UiText.StasisCardName : UiText.BarrierCardName;

        public static string Description(CardKind kind) =>
            kind == CardKind.HeroDash ? UiText.DashCardDescription
            : kind == CardKind.SupportStasis ? UiText.StasisCardDescription
            : UiText.BarrierCardDescription;

        /// <summary>Cập nhật nội dung động: số lần dùng còn lại và viền chọn.</summary>
        public void Show(CardState state)
        {
            if (usesText != null) usesText.text = UiText.CardUsesLeft(state.UsesLeft);
            if (border != null) border.color = state.Selected ? selectedBorderColor : borderColor;
        }

        /// <summary>Bật/tắt nhận raycast của thẻ. Tắt khi thẻ đang biến mất hoặc không còn trong tay.</summary>
        public void SetRaycastable(bool raycastable) => Group.blocksRaycasts = raycastable;

        public void OnPointerClick(PointerEventData eventData)
        {
            // Chuột phải/giữa không chọn thẻ; chạm (touch) luôn báo nút trái.
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke(this, eventData);
        }
    }
}
