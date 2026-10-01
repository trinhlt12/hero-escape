using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HeroEscape.Unity
{
    /// <summary>UI tạm bằng uGUI: tiêu đề, trạng thái, thông báo, hướng dẫn, nút Chờ và Chơi lại.</summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] Text titleText;
        [SerializeField] Text statusText;
        [SerializeField] Text noticeText;
        [SerializeField] Text hintText;
        [SerializeField] Button waitButton;
        [SerializeField] Button restartButton;

        public event Action WaitClicked;
        public event Action RestartClicked;

        void Awake()
        {
            waitButton.onClick.AddListener(() => WaitClicked?.Invoke());
            restartButton.onClick.AddListener(() => RestartClicked?.Invoke());
            titleText.text = UiText.Title;
            hintText.text = UiText.Hint;
            SetLabel(waitButton, UiText.WaitButton);
            SetLabel(restartButton, UiText.RestartButton);
            ReportMissingGlyphs();
        }

        public void SetStatus(string text) => statusText.text = text;

        public void SetNotice(string text) => noticeText.text = text ?? string.Empty;

        /// <summary>Nút Chờ chỉ bấm được trong lượt Hero; nút Chơi lại luôn bấm được.</summary>
        public void SetWaitInteractable(bool interactable) => waitButton.interactable = interactable;

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
                Debug.LogError("[HudView] Text chưa gán font.", this);
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
                Debug.LogWarning($"[HudView] Font '{font.name}' không chứa glyph: \"{missing}\". " +
                                 "Unity có thể thay bằng font hệ điều hành; hãy kiểm tra chữ tiếng Việt trên Game view.", this);
            }
        }
    }
}
