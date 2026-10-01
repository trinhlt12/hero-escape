using System.Collections.Generic;
using HeroEscape.Core;
using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Vẽ bàn cờ từ LevelData và đặt nhân vật theo GameState. Chỉ hiển thị; luật nằm ở Core.
    /// Mỗi ô rộng <see cref="cellSize"/> đơn vị world, tâm bàn trùng vị trí của GameObject này.
    /// Highlight: thay sprite sàn của ô bằng tile_highlight (không phủ thêm lớp lên nhân vật hay cửa).
    /// Preview: ghost mờ của quân ở vị trí dự kiến, vẽ dưới quân thật để vị trí thật luôn rõ.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        // Thứ tự hiển thị: Sàn (hoặc sàn highlight) → Tường/Cửa → ghost dự kiến → nhân vật thật (Art Guide mục 9).
        public const int FloorOrder = 0;
        public const int TileOverlayOrder = 10;
        public const int GhostOrder = 15;
        public const int HeroOrder = 20;
        public const int EnemyOrder = 21; // khi X01 bắt Hero, X01 vẽ đè lên Hero

        [Header("Sprite (Assets/Art)")]
        [SerializeField] Sprite floorSprite;
        [SerializeField] Sprite wallSprite;
        [SerializeField] Sprite exitSprite;
        [SerializeField] Sprite heroSprite;
        [SerializeField] Sprite enemySprite;
        [Tooltip("Sprite thay cho sàn ở các ô hợp lệ (tile_highlight).")]
        [SerializeField] Sprite highlightSprite;

        [Header("Hiển thị")]
        [Tooltip("Material sprite Unlit của URP 2D.")]
        [SerializeField] Material spriteMaterial;
        [SerializeField, Min(0.1f)] float cellSize = 1f;
        [Tooltip("Cạnh dài nhất của tile chiếm tỷ lệ này của ô.")]
        [SerializeField, Range(0.5f, 1f)] float tileFill = 1f;
        [Tooltip("Cạnh dài nhất của nhân vật chiếm tỷ lệ này của ô.")]
        [SerializeField, Range(0.3f, 1f)] float actorFill = 0.8f;
        [Tooltip("Độ đục của ghost vị trí dự kiến.")]
        [SerializeField, Range(0.1f, 0.9f)] float ghostAlpha = 0.4f;

        Transform contentRoot;
        Transform hero;
        readonly List<Transform> enemies = new List<Transform>();
        SpriteRenderer heroGhost;
        readonly List<SpriteRenderer> enemyGhosts = new List<SpriteRenderer>();

        SpriteRenderer[] floorRenderers = new SpriteRenderer[0];
        readonly HashSet<int> highlighted = new HashSet<int>();
        Vector3 floorScale = Vector3.one;
        Vector3 highlightScale = Vector3.one;

        int width = 5;
        int height = 5;

        public Vector2 BoardSize => new Vector2(width * cellSize, height * cellSize);
        public Vector3 BoardCenter => transform.position;
        public float CellSize => cellSize;

        public void Build(LevelData level)
        {
            Clear();
            width = level.Width;
            height = level.Height;
            contentRoot = new GameObject("BoardContent").transform;
            contentRoot.SetParent(transform, false);
            floorRenderers = new SpriteRenderer[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = new GridPos(x, y);
                    floorRenderers[y * width + x] = CreateSprite($"Floor {cell}", floorSprite, cell, FloorOrder, tileFill);

                    CellType type = level.GetCell(cell);
                    if (type == CellType.Wall) CreateSprite($"Wall {cell}", wallSprite, cell, TileOverlayOrder, tileFill);
                    else if (type == CellType.Exit) CreateSprite($"Exit {cell}", exitSprite, cell, TileOverlayOrder, tileFill);
                }
            }

            ComputeHighlightScale();

            hero = CreateSprite("Hero H01", heroSprite, level.HeroStart, HeroOrder, actorFill).transform;
            heroGhost = CreateGhost("Hero H01 (preview)", heroSprite);

            // Mọi X01 dùng chung sprite enemy_x01; thứ tự trong danh sách = thứ tự SpawnIndex.
            foreach (EnemySpawn spawn in level.Enemies)
            {
                enemies.Add(CreateSprite($"X01 {spawn.Id}", enemySprite, spawn.Cell, EnemyOrder, actorFill).transform);
                enemyGhosts.Add(CreateGhost($"X01 {spawn.Id} (preview)", enemySprite));
            }
        }

        public void ShowActors(GameState state)
        {
            if (hero == null) return;
            hero.localPosition = CellToLocal(state.HeroPos);
            for (int i = 0; i < enemies.Count && i < state.Enemies.Count; i++)
                enemies[i].localPosition = CellToLocal(state.Enemies[i].Pos);
        }

        // ---------------- Highlight ----------------

        /// <summary>
        /// Đặt sàn highlight cho đúng các ô <paramref name="cells"/>; ô không còn trong danh sách được trả về sprite sàn gốc.
        /// Chỉ đổi sprite hiển thị, không đổi dữ liệu địa hình.
        /// </summary>
        public void SetHighlights(IReadOnlyList<GridPos> cells)
        {
            var wanted = new HashSet<int>();
            foreach (GridPos cell in cells)
                if (cell.X >= 0 && cell.X < width && cell.Y >= 0 && cell.Y < height) wanted.Add(cell.Y * width + cell.X);

            foreach (int index in new List<int>(highlighted))
                if (!wanted.Contains(index)) SetFloor(index, highlight: false);
            foreach (int index in wanted)
                if (!highlighted.Contains(index)) SetFloor(index, highlight: true);
        }

        /// <summary>Ô đang dùng sprite highlight (cho kiểm tra).</summary>
        public bool IsHighlighted(GridPos cell) => highlighted.Contains(cell.Y * width + cell.X);

        /// <summary>Sprite sàn đang hiển thị ở ô (cho kiểm tra).</summary>
        public Sprite FloorSpriteAt(GridPos cell) => floorRenderers[cell.Y * width + cell.X].sprite;

        void SetFloor(int index, bool highlight)
        {
            SpriteRenderer floor = floorRenderers[index];
            if (floor == null) return;
            if (highlight && highlightSprite == null) return;

            floor.sprite = highlight ? highlightSprite : floorSprite;
            floor.transform.localScale = highlight ? highlightScale : floorScale;
            if (highlight) highlighted.Add(index);
            else highlighted.Remove(index);
        }

        /// <summary>
        /// Scale riêng từng trục để phần hình của tile_highlight trùng khít kích thước hiển thị của tile_floor
        /// (hai ảnh có tỷ lệ khác nhau vài phần trăm). Pivot cả hai ở giữa nên tâm ô không đổi; lưới không nhảy.
        /// </summary>
        void ComputeHighlightScale()
        {
            if (floorRenderers.Length == 0 || floorRenderers[0] == null) return;
            floorScale = floorRenderers[0].transform.localScale;
            if (floorSprite == null || highlightSprite == null) return;

            Vector3 floorShown = Vector3.Scale(floorSprite.bounds.size, floorScale);
            Vector3 source = highlightSprite.bounds.size;
            if (source.x > 0f && source.y > 0f)
                highlightScale = new Vector3(floorShown.x / source.x, floorShown.y / source.y, 1f);
        }

        // ---------------- Preview (ghost) ----------------

        /// <summary>
        /// Ghost mờ ở các vị trí dự kiến khác vị trí thật. Ghost không nhận input và không tham gia occupancy.
        /// </summary>
        public void ShowPreview(GameState actual, GameState predicted)
        {
            HidePreview();
            if (actual == null || predicted == null) return;

            if (predicted.HeroPos != actual.HeroPos) PlaceGhost(heroGhost, predicted.HeroPos);
            for (int i = 0; i < enemyGhosts.Count && i < predicted.Enemies.Count; i++)
                if (predicted.Enemies[i].Pos != actual.Enemies[i].Pos) PlaceGhost(enemyGhosts[i], predicted.Enemies[i].Pos);
        }

        public void HidePreview()
        {
            if (heroGhost != null) heroGhost.gameObject.SetActive(false);
            foreach (SpriteRenderer ghost in enemyGhosts)
                if (ghost != null) ghost.gameObject.SetActive(false);
        }

        void PlaceGhost(SpriteRenderer ghost, GridPos cell)
        {
            if (ghost == null) return;
            ghost.transform.localPosition = CellToLocal(cell);
            ghost.gameObject.SetActive(true);
        }

        SpriteRenderer CreateGhost(string objectName, Sprite sprite)
        {
            SpriteRenderer ghost = CreateSprite(objectName, sprite, new GridPos(0, 0), GhostOrder, actorFill);
            ghost.color = new Color(1f, 1f, 1f, ghostAlpha);
            ghost.gameObject.SetActive(false);
            return ghost;
        }

        // ---------------- Lưới ----------------

        /// <summary>Vị trí world của X01 thứ <paramref name="index"/> (theo SpawnIndex); false nếu không có.</summary>
        public bool TryGetEnemyWorldPosition(int index, out Vector3 position)
        {
            bool exists = index >= 0 && index < enemies.Count && enemies[index] != null;
            position = exists ? enemies[index].position : Vector3.zero;
            return exists;
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

        SpriteRenderer CreateSprite(string objectName, Sprite sprite, GridPos cell, int sortingOrder, float fill)
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

            return renderer;
        }

        void Clear()
        {
            if (contentRoot != null)
            {
                if (Application.isPlaying) Destroy(contentRoot.gameObject);
                else DestroyImmediate(contentRoot.gameObject);
            }

            contentRoot = null;
            hero = null;
            heroGhost = null;
            enemies.Clear();
            enemyGhosts.Clear();
            highlighted.Clear();
            floorRenderers = new SpriteRenderer[0];
        }
    }
}
