using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Chỉnh camera trực giao để toàn bộ bàn cờ nằm trong vùng màn hình giữa panel trên và panel dưới,
    /// ở mọi tỷ lệ màn hình (9:16, 9:20, hoặc ngang trong Editor). UI không che bàn cờ.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class CameraFitter : MonoBehaviour
    {
        [SerializeField] BoardView board;
        [SerializeField] RectTransform topPanel;
        [SerializeField] RectTransform bottomPanel;
        [Tooltip("Khoảng trống quanh bàn, tính bằng số ô.")]
        [SerializeField, Min(0f)] float marginCells = 0.25f;

        static readonly Vector3[] Corners = new Vector3[4];
        Camera targetCamera;

        void Awake()
        {
            targetCamera = GetComponent<Camera>();
            targetCamera.orthographic = true;
        }

        void LateUpdate()
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            if (screenWidth <= 0f || screenHeight <= 0f || board == null) return;

            // Canvas Screen Space Overlay: góc world của RectTransform chính là tọa độ pixel màn hình.
            float topReserved = topPanel != null ? screenHeight - EdgeY(topPanel, bottom: true) : 0f;
            float bottomReserved = bottomPanel != null ? EdgeY(bottomPanel, bottom: false) : 0f;
            float freeHeight = Mathf.Max(screenHeight - topReserved - bottomReserved, screenHeight * 0.2f);

            Vector2 needed = board.BoardSize + Vector2.one * (2f * marginCells);
            // orthographicSize = nửa chiều cao nhìn thấy; pixel mỗi đơn vị = screenHeight / (2 * size).
            float sizeForHeight = needed.y * screenHeight / (2f * freeHeight);
            float sizeForWidth = needed.x * screenHeight / (2f * screenWidth);
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            targetCamera.orthographicSize = size;

            // Dời camera để tâm bàn nằm giữa vùng trống, không phải giữa màn hình.
            float pixelsPerUnit = screenHeight / (2f * size);
            float freeCenter = bottomReserved + freeHeight * 0.5f;
            float offset = (freeCenter - screenHeight * 0.5f) / pixelsPerUnit;
            Vector3 center = board.BoardCenter;
            transform.position = new Vector3(center.x, center.y - offset, transform.position.z);
        }

        static float EdgeY(RectTransform rect, bool bottom)
        {
            rect.GetWorldCorners(Corners); // 0 dưới-trái, 1 trên-trái, 2 trên-phải, 3 dưới-phải
            return bottom ? Mathf.Min(Corners[0].y, Corners[3].y) : Mathf.Max(Corners[1].y, Corners[2].y);
        }
    }
}
