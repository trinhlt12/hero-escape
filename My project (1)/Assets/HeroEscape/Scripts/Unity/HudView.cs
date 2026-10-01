using System;
using System.Text;
using UnityEngine;
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
        [SerializeField] Button restartButton;
        [SerializeField] Button nextLevelButton;
        [SerializeField] Button backButton;
        [Tooltip("Màu nút Dash khi đang chọn hướng.")]
        [SerializeField] Color dashSelectingColor = new Color32(0xE8, 0xB8, 0x49, 0xFF);

        Image dashImage;
        Color dashNormalColor;

        public event Action WaitClicked;
        public event Action DashClicked;
        public event Action RestartClicked;
        public event Action NextLevelClicked;
        public event Action BackClicked;

        void Awake()
        {
            waitButton.onClick.AddListener(() => WaitClicked?.Invoke());
            dashButton.onClick.AddListener(() => DashClicked?.Invoke());
            restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
            nextLevelButton.onClick.AddListener(() => NextLevelClicked?.Invoke());
            backButton.onClick.AddListener(() => BackClicked?.Invoke());

            dashImage = dashButton.targetGraphic as Image;
            if (dashImage != null) dashNormalColor = dashImage.color;

            SetLabel(waitButton, UiText.WaitButton);
            SetLabel(restartButton, UiText.RestartButton);
            SetLabel(nextLevelButton, UiText.NextLevelButton);
            ReportMissingGlyphs();
        }

        public void SetTitle(string text) => titleText.text = text;

        public void SetHint(string text) => hintText.text = text;

        public void SetStatus(string text) => statusText.text = text;

        public void SetNotice(string text) => noticeText.text = text ?? string.Empty;

        public void SetWaitInteractable(bool interactable) => waitButton.interactable = interactable;

        public void SetDash(bool visible, string label, bool interactable, bool selecting)
        {
            dashButton.gameObject.SetActive(visible);
            SetLabel(dashButton, label);
            dashButton.interactable = interactable;
            if (dashImage != null) dashImage.color = selecting ? dashSelectingColor : dashNormalColor;
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
