using System.Collections.Generic;
using HeroEscape.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Đọc click/chạm trên bàn cờ bằng Input System và đổi sang ô lưới.
    /// Bỏ qua lần bấm nằm trên UI để không kích hoạt ô phía sau nút.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardInput : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] BoardView board;
        [SerializeField] GameController controller;

        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 screen = pointer.position.ReadValue();
            if (IsOverUi(screen)) return;

            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            if (board.TryWorldToCell(world, out GridPos cell)) controller.OnCellClicked(cell);
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
