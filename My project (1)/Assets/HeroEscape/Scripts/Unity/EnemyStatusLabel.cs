using System.Collections.Generic;
using HeroEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Nhãn chữ gắn với từng X01: số thứ tự hành động ("1", "2"; chỉ hiện khi màn có nhiều X) ở góc trên trái ô,
    /// và "Frozen" phía trên X khi X đó đang bị khóa. Chỉ hiển thị; nội dung do GameController đặt theo trạng thái.
    /// Nhãn nằm trên canvas overlay, không nhận raycast nên không chặn việc bấm vào X.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class EnemyStatusLabel : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] BoardView board;
        [Tooltip("Mẫu nhãn Frozen (tắt sẵn); được nhân bản cho từng X01.")]
        [SerializeField] Text frozenTemplate;
        [Tooltip("Mẫu nhãn số thứ tự (tắt sẵn); được nhân bản cho từng X01.")]
        [SerializeField] Text orderTemplate;
        [Tooltip("Độ cao nhãn Frozen so với tâm X01, tính bằng số ô.")]
        [SerializeField] float frozenOffsetCells = 0.55f;
        [Tooltip("Vị trí nhãn số so với tâm X01 (góc trên trái ô), tính bằng số ô.")]
        [SerializeField] Vector2 orderOffsetCells = new Vector2(-0.36f, 0.34f);

        readonly List<Text> frozenLabels = new List<Text>();
        readonly List<bool> realFrozen = new List<bool>();
        int previewFrozenIndex = -1;
        readonly List<Text> orderLabels = new List<Text>();
        readonly List<(GridPos cell, Text label)> barrierLabels = new List<(GridPos, Text)>();

        void Awake()
        {
            frozenTemplate.gameObject.SetActive(false);
            orderTemplate.gameObject.SetActive(false);
        }

        /// <summary>Tạo nhãn cho <paramref name="enemyCount"/> X01 của màn mới; xóa nhãn của màn cũ.</summary>
        public void Setup(int enemyCount, bool showOrder)
        {
            foreach (Text label in frozenLabels) Destroy(label.gameObject);
            foreach (Text label in orderLabels) Destroy(label.gameObject);
            ClearBarrierLabels();
            frozenLabels.Clear();
            orderLabels.Clear();
            realFrozen.Clear();
            previewFrozenIndex = -1;

            for (int i = 0; i < enemyCount; i++)
            {
                frozenLabels.Add(Clone(frozenTemplate, $"Frozen {i + 1}", UiText.FrozenLabel, active: false));
                realFrozen.Add(false);
                orderLabels.Add(Clone(orderTemplate, $"Order {i + 1}", UiText.EnemyOrder(i + 1), active: showOrder));
            }
        }

        /// <summary>Trạng thái khóa thật của X thứ <paramref name="index"/> (chỉ đúng sau khi đã dùng thẻ).</summary>
        public void SetFrozen(int index, bool frozen)
        {
            if (index < 0 || index >= realFrozen.Count) return;
            realFrozen[index] = frozen;
            Refresh(index);
        }

        /// <summary>Hover mục tiêu Stasis hợp lệ: hiện "Will be frozen" trên X đó (-1 = không có). Chỉ là dự kiến.</summary>
        public void SetFrozenPreview(int index)
        {
            if (index == previewFrozenIndex) return;
            int previous = previewFrozenIndex;
            previewFrozenIndex = index;
            Refresh(previous);
            Refresh(index);
        }

        /// <summary>
        /// Nhãn số pha địch còn lại trên mỗi vật cản tạm ("1"). Đây là số pha, không phải thời gian thực. Dùng lại mẫu nhãn số.
        /// </summary>
        public void SetBarriers(IReadOnlyList<GridPos> cells, int phasesLeft)
        {
            bool same = barrierLabels.Count == cells.Count;
            for (int i = 0; same && i < cells.Count; i++) same = barrierLabels[i].cell == cells[i];
            if (same) return;

            ClearBarrierLabels();
            foreach (GridPos cell in cells)
                barrierLabels.Add((cell, Clone(orderTemplate, $"Barrier {cell}", UiText.BarrierPhasesLabel(phasesLeft), active: true)));
        }

        void ClearBarrierLabels()
        {
            foreach ((GridPos _, Text label) in barrierLabels)
                if (label != null) Destroy(label.gameObject);
            barrierLabels.Clear();
        }

        public void HideAllFrozen()
        {
            previewFrozenIndex = -1;
            for (int i = 0; i < realFrozen.Count; i++)
            {
                realFrozen[i] = false;
                Refresh(i);
            }
        }

        void Refresh(int index)
        {
            if (index < 0 || index >= frozenLabels.Count) return;
            Text label = frozenLabels[index];
            bool preview = index == previewFrozenIndex && !realFrozen[index];
            label.text = realFrozen[index] ? UiText.FrozenLabel : UiText.WillBeFrozen;
            label.gameObject.SetActive(realFrozen[index] || preview);
        }

        void LateUpdate()
        {
            float cell = board.CellSize;
            for (int i = 0; i < frozenLabels.Count; i++)
            {
                if (!board.TryGetEnemyWorldPosition(i, out Vector3 world)) continue;
                Place(frozenLabels[i], world + Vector3.up * (cell * frozenOffsetCells));
                Place(orderLabels[i], world + new Vector3(orderOffsetCells.x, orderOffsetCells.y, 0f) * cell);
            }

            foreach ((GridPos barrier, Text label) in barrierLabels)
                Place(label, board.CellToWorld(barrier));
        }

        void Place(Text label, Vector3 world)
        {
            if (!label.gameObject.activeSelf) return;
            Vector3 screen = worldCamera.WorldToScreenPoint(world);
            label.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
        }

        Text Clone(Text template, string objectName, string text, bool active)
        {
            Text label = Instantiate(template, transform);
            label.name = objectName;
            label.text = text;
            label.raycastTarget = false;
            label.gameObject.SetActive(active);
            return label;
        }
    }
}
