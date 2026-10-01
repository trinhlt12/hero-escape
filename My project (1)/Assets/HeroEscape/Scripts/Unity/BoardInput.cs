using System.Collections.Generic;
using HeroEscape.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Đọc click/chạm bằng Input System, đổi sang ô lưới và chuyển cho GameController.
    /// Luôn báo kèm việc con trỏ có nằm trên UI hay không; lần bấm trên UI không được tác động bàn cờ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardInput : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] BoardView board;
        [SerializeField] GameController controller;

        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        // Sau khi nạp scene (ví dụ từ Level Select), bỏ qua lần bấm đang giữ cho tới khi thả ra,
        // để click chọn màn không trở thành một hành động trên bàn cờ.
        bool waitForRelease = true;

        void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (waitForRelease)
            {
                if (pointer.press.isPressed) return;
                waitForRelease = false;
            }

            Vector2 screen = pointer.position.ReadValue();
            bool overUi = IsOverUi(screen);
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            GridPos? cell = board.TryWorldToCell(world, out GridPos hit) ? hit : (GridPos?)null;

            // Hover (chuột trong Editor): ô dưới con trỏ, hoặc null nếu con trỏ đang trên UI. GameController chỉ
            // mô phỏng lại khi ô, trạng thái hoặc chế độ thay đổi.
            controller.OnBoardHover(overUi ? null : cell);

            if (pointer.press.wasPressedThisFrame) controller.OnBoardPressed(overUi, cell);
        }

        static bool IsOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var data = new PointerEventData(eventSystem) { position = screenPosition };
            UiHits.Clear();
            eventSystem.RaycastAll(data, UiHits);
            return UiHits.Count > 0;
        }
    }
}
