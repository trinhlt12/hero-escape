using HeroEscape.Core;
using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Vẽ bàn cờ từ LevelData và đặt nhân vật theo GameState. Chỉ hiển thị; luật nằm ở Core.
    /// Mỗi ô rộng <see cref="cellSize"/> đơn vị world, tâm bàn trùng vị trí của GameObject này.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        // Thứ tự hiển thị: Sàn → Tường/Cửa → nhân vật (Art Guide mục 9).
        public const int FloorOrder = 0;
        public const int TileOverlayOrder = 10;
        public const int HeroOrder = 20;
        public const int EnemyOrder = 21; // khi X01 bắt Hero, X01 vẽ đè lên Hero

        [Header("Sprite (Assets/Art)")]
        [SerializeField] Sprite floorSprite;
        [SerializeField] Sprite wallSprite;
        [SerializeField] Sprite exitSprite;
        [SerializeField] Sprite heroSprite;
        [SerializeField] Sprite enemySprite;

        [Header("Hiển thị")]
        [Tooltip("Material sprite Unlit của URP 2D.")]
        [SerializeField] Material spriteMaterial;
        [SerializeField, Min(0.1f)] float cellSize = 1f;
        [Tooltip("Cạnh dài nhất của tile chiếm tỷ lệ này của ô.")]
        [SerializeField, Range(0.5f, 1f)] float tileFill = 1f;
        [Tooltip("Cạnh dài nhất của nhân vật chiếm tỷ lệ này của ô.")]
        [SerializeField, Range(0.3f, 1f)] float actorFill = 0.8f;

        Transform contentRoot;
        Transform hero;
        Transform enemy;
        int width = 5;
        int height = 5;

        public Vector2 BoardSize => new Vector2(width * cellSize, height * cellSize);
        public Vector3 BoardCenter => transform.position;

        public void Build(LevelData level)
        {
            Clear();
            width = level.Width;
            height = level.Height;
            contentRoot = new GameObject("BoardContent").transform;
            contentRoot.SetParent(transform, false);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new GridPos(x, y);
                    CreateSprite($"Floor {cell}", floorSprite, cell, FloorOrder, tileFill);

                    CellType type = level.GetCell(cell);
                    if (type == CellType.Wall) CreateSprite($"Wall {cell}", wallSprite, cell, TileOverlayOrder, tileFill);
                    else if (type == CellType.Exit) CreateSprite($"Exit {cell}", exitSprite, cell, TileOverlayOrder, tileFill);
                }
            }

            hero = CreateSprite("Hero H01", heroSprite, level.HeroStart, HeroOrder, actorFill);
            enemy = CreateSprite("X01", enemySprite, level.EnemyStart, EnemyOrder, actorFill);
        }

        public void ShowActors(GameState state)
        {
            if (hero == null || enemy == null) return;
            hero.localPosition = CellToLocal(state.HeroPos);
            enemy.localPosition = CellToLocal(state.EnemyPos);
        }

        public bool TryWorldToCell(Vector3 world, out GridPos cell)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            int x = Mathf.FloorToInt(local.x / cellSize + width * 0.5f);
            int y = Mathf.FloorToInt(local.y / cellSize + height * 0.5f);
            cell = new GridPos(x, y);
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        Vector3 CellToLocal(GridPos p) =>
            new Vector3((p.X - (width - 1) * 0.5f) * cellSize, (p.Y - (height - 1) * 0.5f) * cellSize, 0f);

        Transform CreateSprite(string objectName, Sprite sprite, GridPos cell, int sortingOrder, float fill)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(contentRoot, false);
            go.transform.localPosition = CellToLocal(cell);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;

            if (sprite != null)
            {
                // Scale đều hai trục theo cạnh dài nhất: giữ tỷ lệ ảnh, không kéo méo.
                Vector3 size = sprite.bounds.size;
                float longest = Mathf.Max(size.x, size.y);
                if (longest > 0f) go.transform.localScale = Vector3.one * (cellSize * fill / longest);
            }
            else
            {
                Debug.LogError($"[BoardView] Thiếu sprite cho '{objectName}'. Chạy lại menu Hero Escape > Milestone 01 > Dựng scene chơi thử.", this);
            }

            return go.transform;
        }

        void Clear()
        {
            if (contentRoot != null) Destroy(contentRoot.gameObject);
            contentRoot = null;
            hero = null;
            enemy = null;
        }
    }
}
