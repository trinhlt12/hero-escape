using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>Co RectTransform vào Screen.safeArea để UI không bị tai thỏ/thanh hệ thống che.</summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rect;
        Rect applied;
        Vector2Int appliedScreen;

        void Awake() => rect = (RectTransform)transform;

        void Update()
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == applied && screen == appliedScreen) return;
            if (screen.x <= 0 || screen.y <= 0) return;

            applied = safe;
            appliedScreen = screen;
            rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
