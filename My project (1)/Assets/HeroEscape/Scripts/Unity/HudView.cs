using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeroEscape.Unity
{
    /// <summary>
    /// UI tạm bằng uGUI: tiêu đề màn, trạng thái, thông báo, hướng dẫn và các nút Wait, Dash, Restart,
    /// Next Level, Back. Chỉ hiển thị; GameController quyết định nội dung và trạng thái nút.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] Text titleText;
        [SerializeField] Text statusText;
        [SerializeField] Text noticeText;
        [SerializeField] Text hintText;
        [SerializeField] Button waitButton;
        [SerializeField] Button dashButton;
        [SerializeField] Button stasisButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button nextLevelButton;
        [SerializeField] Button backButton;
        [SerializeField] Button levelSelectButton;
        [Tooltip("Màu nút Dash/Stasis khi đang chọn hướng hoặc mục tiêu.")]
        [SerializeField] Color dashSelectingColor = new Color32(0xE8, 0xB8, 0x49, 0xFF);

        Image dashImage;
        Color dashNormalColor;
        Image stasisImage;
        Color stasisNormalColor;

        public event Action WaitClicked;
        public event Action DashClicked;
        public event Action StasisClicked;
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
            dashButton.onClick.AddListener(() => DashClicked?.Invoke());
            stasisButton.onClick.AddListener(() => StasisClicked?.Invoke());
            restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
            nextLevelButton.onClick.AddListener(() => NextLevelClicked?.Invoke());
            backButton.onClick.AddListener(() => BackClicked?.Invoke());
            levelSelectButton.onClick.AddListener(() => LevelSelectClicked?.Invoke());

            dashImage = dashButton.targetGraphic as Image;
            if (dashImage != null) dashNormalColor = dashImage.color;
            stasisImage = stasisButton.targetGraphic as Image;
            if (stasisImage != null) stasisNormalColor = stasisImage.color;

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

        public void SetDash(bool visible, string label, bool interactable, bool selecting)
        {
            dashButton.gameObject.SetActive(visible);
            SetLabel(dashButton, label);
            dashButton.interactable = interactable;
            if (dashImage != null) dashImage.color = selecting ? dashSelectingColor : dashNormalColor;
        }

        public void SetStasis(bool visible, string label, bool interactable, bool selecting)
        {
            stasisButton.gameObject.SetActive(visible);
            SetLabel(stasisButton, label);
            stasisButton.interactable = interactable;
            if (stasisImage != null) stasisImage.color = selecting ? dashSelectingColor : stasisNormalColor;
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
