using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Màn Level Select: một nút cho mỗi màn trong <see cref="LevelCatalog"/>, tất cả mở sẵn.
    /// Click ghi nhận ID màn rồi nạp gameplay scene ngay (không Confirm). Chặn click lặp nạp scene nhiều lần.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSelectView : MonoBehaviour
    {
        [SerializeField] LevelCatalog catalog;
        [Tooltip("Nơi chứa các nút màn (có VerticalLayoutGroup).")]
        [SerializeField] RectTransform listRoot;
        [Tooltip("Nút mẫu (tắt sẵn) có hai Text con tên 'Name' và 'Description'.")]
        [SerializeField] Button buttonTemplate;
        [SerializeField] Text titleText;
        [SerializeField] Text messageText;

        readonly List<Button> buttons = new List<Button>();
        bool loading;

        void Start()
        {
            buttonTemplate.gameObject.SetActive(false);
            if (titleText != null) titleText.text = UiText.SelectLevelTitle;
            if (messageText != null) messageText.text = string.Empty;

            if (catalog == null || catalog.Levels.Count == 0)
            {
                Debug.LogError("[LevelSelect] Missing level catalog. Rebuild: Hero Escape > Milestone 01 > Dựng scene Level Select.", this);
                if (messageText != null) messageText.text = UiText.LevelError;
                return;
            }

            foreach (LevelEntry entry in catalog.Levels)
            {
                if (entry == null) continue;
                Button button = Instantiate(buttonTemplate, listRoot);
                button.name = $"Level {entry.Id}";
                SetText(button, "Name", entry.DisplayName);
                SetText(button, "Description", entry.Description);
                string id = entry.Id;
                button.onClick.AddListener(() => Select(id));
                button.gameObject.SetActive(true);
                buttons.Add(button);
            }
        }

        void OnDestroy()
        {
            foreach (Button button in buttons)
                if (button != null) button.onClick.RemoveAllListeners();
        }

        void Select(string levelId)
        {
            if (loading) return; // click nhanh nhiều lần chỉ nạp scene một lần
            loading = true;
            foreach (Button button in buttons) button.interactable = false;

            LevelSelection.Request(levelId);
            SceneManager.LoadScene(catalog.GameplayScene, LoadSceneMode.Single);
        }

        static void SetText(Button button, string childName, string text)
        {
            Transform child = button.transform.Find(childName);
            if (child != null && child.TryGetComponent(out Text label)) label.text = text;
        }
    }
}
