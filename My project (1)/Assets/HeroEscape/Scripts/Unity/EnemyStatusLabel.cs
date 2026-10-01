using System.Collections.Generic;
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
        readonly List<Text> orderLabels = new List<Text>();

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
            frozenLabels.Clear();
            orderLabels.Clear();

            for (int i = 0; i < enemyCount; i++)
            {
                frozenLabels.Add(Clone(frozenTemplate, $"Frozen {i + 1}", UiText.FrozenLabel, active: false));
                orderLabels.Add(Clone(orderTemplate, $"Order {i + 1}", UiText.EnemyOrder(i + 1), active: showOrder));
            }
        }

        public void SetFrozen(int index, bool frozen)
        {
            if (index >= 0 && index < frozenLabels.Count) frozenLabels[index].gameObject.SetActive(frozen);
        }

        public void HideAllFrozen()
        {
            foreach (Text label in frozenLabels) label.gameObject.SetActive(false);
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
