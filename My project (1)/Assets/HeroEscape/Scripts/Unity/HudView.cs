using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeroEscape.Unity
{
    /// <summary>
    /// UI tạm bằng uGUI: tiêu đề màn, trạng thái, thông báo, hướng dẫn và các nút Wait, Restart,
    /// Next Level, Back, Level Select. Dash/Stasis kích hoạt bằng tay bài (HandView), không có nút riêng.
    /// Chỉ hiển thị; GameController quyết định nội dung và trạng thái nút.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] Text titleText;
        [SerializeField] Text statusText;
        [SerializeField] Text noticeText;
        [SerializeField] Text hintText;
        [SerializeField] Button waitButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button nextLevelButton;
        [SerializeField] Button backButton;
        [SerializeField] Button levelSelectButton;
        [Tooltip("Panel dưới. Chiều cao trong scene đã gồm phần chừa cho tay bài; màn không có thẻ thì thu phần đó lại.")]
        [SerializeField] RectTransform bottomPanel;
        [Tooltip("Chiều cao phần đáy panel dành cho tay bài (đơn vị canvas).")]
        [SerializeField, Min(0f)] float handSpace = 270f;

        float bottomPanelFullHeight = -1f;

        public event Action WaitClicked;
        public event Action RestartClicked;
        public event Action NextLevelClicked;
        public event Action BackClicked;
        public event Action LevelSelectClicked;

        /// <summary>Con trỏ vào (true) hoặc rời (false) nút Wait: dùng để preview Wait.</summary>
        public event Action<bool> WaitHoverChanged;

        string notice = string.Empty;
        string previewWarning;

        void Awake()
        {
            AddHoverRelay(waitButton.gameObject, hovered => WaitHoverChanged?.Invoke(hovered));

            waitButton.onClick.AddListener(() => WaitClicked?.Invoke());
            restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
            nextLevelButton.onClick.AddListener(() => NextLevelClicked?.Invoke());
            backButton.onClick.AddListener(() => BackClicked?.Invoke());
            levelSelectButton.onClick.AddListener(() => LevelSelectClicked?.Invoke());

            SetLabel(waitButton, UiText.WaitButton);
            SetLabel(restartButton, UiText.RestartButton);
            SetLabel(nextLevelButton, UiText.NextLevelButton);
            SetLabel(levelSelectButton, UiText.LevelSelectButton);
            ReportMissingGlyphs();
        }

        public void SetTitle(string text) => titleText.text = text;

        public void SetHint(string text) => hintText.text = text;

        public void SetStatus(string text) => statusText.text = text;

        public void SetNotice(string text)
        {
            notice = text ?? string.Empty;
            noticeText.text = previewWarning ?? notice;
        }

        /// <summary>Cảnh báo dự kiến khi hover ("You will be caught"/"You will escape"); null = trả lại thông báo thường.</summary>
        public void SetPreviewWarning(string text)
        {
            previewWarning = string.IsNullOrEmpty(text) ? null : text;
            noticeText.text = previewWarning ?? notice;
        }

        static void AddHoverRelay(GameObject target, Action<bool> onHover)
        {
            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => onHover(true));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => onHover(false));
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        public void SetWaitInteractable(bool interactable) => waitButton.interactable = interactable;

        public void SetLevelSelectInteractable(bool interactable) => levelSelectButton.interactable = interactable;

        /// <summary>
        /// Chừa (true) hoặc thu lại (false) phần đáy panel dưới dành cho tay bài. Gọi khi vào màn, không gọi giữa màn,
        /// nên bàn cờ không đổi kích thước khi thẻ cuối cùng rời tay. Nội dung panel neo theo cạnh trên nên không dịch chuyển.
        /// </summary>
        public void SetHandSpace(bool reserve)
        {
            if (bottomPanel == null) return;
            if (bottomPanelFullHeight < 0f) bottomPanelFullHeight = bottomPanel.sizeDelta.y;
            float height = reserve ? bottomPanelFullHeight : Mathf.Max(0f, bottomPanelFullHeight - handSpace);
            bottomPanel.sizeDelta = new Vector2(bottomPanel.sizeDelta.x, height);
        }

        public void SetNavigation(bool showNext, bool showBack, string backLabel)
        {
            nextLevelButton.gameObject.SetActive(showNext);
            backButton.gameObject.SetActive(showBack);
            SetLabel(backButton, backLabel);
        }

        static void SetLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = label;
        }

        /// <summary>
        /// Báo trong Console nếu font không có glyph cho chữ đang dùng, thay vì để chữ lỗi mà không ai biết.
        /// Font dynamic có thể vẫn hiển thị nhờ font hệ điều hành, nên cần kiểm tra bằng mắt khi có cảnh báo.
        /// </summary>
        void ReportMissingGlyphs()
        {
            Font font = statusText.font;
            if (font == null)
            {
                Debug.LogError("[HudView] Text has no font assigned.", this);
                return;
            }

            var missing = new StringBuilder();
            foreach (char c in UiText.AllDisplayedText)
            {
                if (char.IsWhiteSpace(c) || missing.ToString().IndexOf(c) >= 0) continue;
                if (!font.HasCharacter(c)) missing.Append(c);
            }

            if (missing.Length > 0)
            {
                Debug.LogWarning($"[HudView] Font '{font.name}' is missing glyphs: \"{missing}\". " +
                                 "Unity may fall back to an OS font; check the text in the Game view.", this);
            }
        }
    }
}
