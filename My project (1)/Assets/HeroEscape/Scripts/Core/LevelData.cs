using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroEscape.Core
{
    public enum CellType
    {
        Floor = 0,
        Wall = 1,
        Exit = 2,
    }

    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Định nghĩa level chỉ đọc. Ô lưu mảng phẳng theo index = Y * Width + X.
    /// Hero và các X01 đứng trên ô Sàn; vị trí của họ nằm ngoài mảng ô.
    /// </summary>
    public sealed class LevelData
    {
        public const int MinSize = 5;
        public const int MaxSize = 8;
        public const int MaxEnemies = 3;
        public const int DashMaxUsesLimit = 9;
        public const int StasisMaxUsesLimit = 9;

        readonly CellType[] cells;

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public GridPos HeroStart { get; }
        public GridPos Exit { get; }

        /// <summary>Các X01 theo SpawnIndex tăng dần (đúng thứ tự hành động).</summary>
        public IReadOnlyList<EnemySpawn> Enemies { get; }

        /// <summary>Ô xuất phát của X01 đầu tiên. Lối tắt cho màn chỉ có một X.</summary>
        public GridPos EnemyStart => Enemies[0].Cell;

        /// <summary>Chuỗi hành động tham chiếu dẫn đến thắng (có thể rỗng). Dùng để kiểm chứng level.</summary>
        public IReadOnlyList<HeroAction> ReferenceSolution { get; }

        /// <summary>Số lần Dash tối đa trong một lần chơi màn này. 0 = màn không cho Dash.</summary>
        public int DashMaxUses { get; }

        /// <summary>Số lần dùng thẻ Stasis trong một lần chơi màn này. 0 = màn không có Stasis.</summary>
        public int StasisMaxUses { get; }

        /// <summary>Lượt (đánh số từ 0 theo <see cref="ReferenceSolution"/>) dùng Stasis → ID X01 mục tiêu.</summary>
        public IReadOnlyDictionary<int, string> ReferenceStasisTargets { get; }

        /// <summary>Các lượt dùng Stasis trong lời giải tham chiếu, tăng dần.</summary>
        public IReadOnlyCollection<int> ReferenceStasisTurns => ReferenceStasisTargets.Keys.OrderBy(t => t).ToArray();

        /// <summary>Màn có đúng một X01 (định dạng cũ).</summary>
        public LevelData(string id, int width, int height, CellType[] cells, GridPos heroStart, GridPos enemyStart,
            IReadOnlyList<HeroAction> referenceSolution, int dashMaxUses = 0, int stasisMaxUses = 0,
            IReadOnlyCollection<int> referenceStasisTurns = null)
            : this(id, width, height, cells, heroStart, new[] { new EnemySpawn(DefaultEnemyId(1), 1, enemyStart) },
                referenceSolution, dashMaxUses, stasisMaxUses,
                referenceStasisTurns?.ToDictionary(turn => turn, _ => DefaultEnemyId(1)))
        {
        }

        public LevelData(string id, int width, int height, CellType[] cells, GridPos heroStart, IReadOnlyList<EnemySpawn> enemies,
            IReadOnlyList<HeroAction> referenceSolution, int dashMaxUses, int stasisMaxUses,
            IReadOnlyDictionary<int, string> referenceStasisTargets)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new LevelFormatException("Level thiếu id.");
            if (width < MinSize || width > MaxSize || height < MinSize || height > MaxSize)
                throw new LevelFormatException($"[{id}] Kích thước {width}x{height} nằm ngoài giới hạn {MinSize}-{MaxSize} mỗi chiều.");
            if (cells == null || cells.Length != width * height)
                throw new LevelFormatException($"[{id}] Số ô không khớp kích thước {width}x{height}.");
            if (dashMaxUses < 0 || dashMaxUses > DashMaxUsesLimit)
                throw new LevelFormatException($"[{id}] Số lần Dash {dashMaxUses} nằm ngoài giới hạn 0-{DashMaxUsesLimit}.");
            if (stasisMaxUses < 0 || stasisMaxUses > StasisMaxUsesLimit)
                throw new LevelFormatException($"[{id}] Số lần Stasis {stasisMaxUses} nằm ngoài giới hạn 0-{StasisMaxUsesLimit}.");

            Id = id;
            Width = width;
            Height = height;
            this.cells = (CellType[])cells.Clone();
            HeroStart = heroStart;
            DashMaxUses = dashMaxUses;
            StasisMaxUses = stasisMaxUses;
            ReferenceSolution = referenceSolution ?? Array.Empty<HeroAction>();

            int exitCount = 0;
            for (int i = 0; i < this.cells.Length; i++)
            {
                if (this.cells[i] == CellType.Exit)
                {
                    exitCount++;
                    Exit = new GridPos(i % width, i / width);
                }
            }

            if (exitCount != 1) throw new LevelFormatException($"[{id}] Cần đúng một Cửa, đang có {exitCount}.");
            RequireFloor(heroStart, "Hero");

            Enemies = ValidateEnemies(enemies);

            var targets = new Dictionary<int, string>();
            if (referenceStasisTargets != null)
            {
                foreach (KeyValuePair<int, string> pair in referenceStasisTargets)
                {
                    if (Enemies.All(e => e.Id != pair.Value))
                        throw new LevelFormatException($"[{id}] Lời giải dùng Stasis lên '{pair.Value}' nhưng level không có X01 này.");
                    targets[pair.Key] = pair.Value;
                }
            }

            ReferenceStasisTargets = targets;
        }

        /// <summary>ID mặc định của X01 có SpawnIndex <paramref name="spawnIndex"/>: X1, X2, ...</summary>
        public static string DefaultEnemyId(int spawnIndex) => "X" + spawnIndex;

        public bool IsInside(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public CellType GetCell(GridPos p)
        {
            if (!IsInside(p)) throw new ArgumentOutOfRangeException(nameof(p), $"Ô {p} nằm ngoài bàn {Width}x{Height}.");
            return cells[p.Y * Width + p.X];
        }

        /// <summary>Báo lỗi rõ nếu dữ liệu X01 sai; không tự sửa. Trả về danh sách sắp theo SpawnIndex.</summary>
        IReadOnlyList<EnemySpawn> ValidateEnemies(IReadOnlyList<EnemySpawn> enemies)
        {
            if (enemies == null || enemies.Count == 0) throw new LevelFormatException($"[{Id}] Cần ít nhất một X01.");
            if (enemies.Count > MaxEnemies) throw new LevelFormatException($"[{Id}] Tối đa {MaxEnemies} X01, đang có {enemies.Count}.");

            var ids = new HashSet<string>();
            var indices = new HashSet<int>();
            var occupied = new HashSet<GridPos> { HeroStart };
            foreach (EnemySpawn e in enemies)
            {
                if (string.IsNullOrWhiteSpace(e.Id)) throw new LevelFormatException($"[{Id}] X01 ở {e.Cell} thiếu ID.");
                if (!ids.Add(e.Id)) throw new LevelFormatException($"[{Id}] Trùng ID X01 '{e.Id}'.");
                if (!indices.Add(e.SpawnIndex)) throw new LevelFormatException($"[{Id}] Trùng spawnIndex {e.SpawnIndex}.");
                RequireFloor(e.Cell, $"X01 '{e.Id}'");
                if (!occupied.Add(e.Cell)) throw new LevelFormatException($"[{Id}] X01 '{e.Id}' ở {e.Cell} trùng ô với Hero hoặc X01 khác.");
            }

            return enemies.OrderBy(e => e.SpawnIndex).ToArray();
        }

        void RequireFloor(GridPos p, string actor)
        {
            if (!IsInside(p)) throw new LevelFormatException($"[{Id}] {actor} ở {p} nằm ngoài bàn.");
            if (GetCell(p) != CellType.Floor) throw new LevelFormatException($"[{Id}] {actor} ở {p} phải đứng trên ô Sàn.");
        }
    }
}
