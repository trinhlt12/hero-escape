using System;
using System.Collections.Generic;

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
    /// Hero và X01 đứng trên ô Sàn; vị trí của họ nằm ngoài mảng ô.
    /// </summary>
    public sealed class LevelData
    {
        public const int MinSize = 5;
        public const int MaxSize = 8;

        readonly CellType[] cells;

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public GridPos HeroStart { get; }
        public GridPos EnemyStart { get; }
        public GridPos Exit { get; }

        /// <summary>Chuỗi hành động tham chiếu dẫn đến thắng (có thể rỗng). Dùng để kiểm chứng level.</summary>
        public IReadOnlyList<HeroAction> ReferenceSolution { get; }

        /// <summary>Số lần Dash tối đa trong một lần chơi màn này. 0 = màn không cho Dash.</summary>
        public int DashMaxUses { get; }

        public const int DashMaxUsesLimit = 9;

        public LevelData(string id, int width, int height, CellType[] cells, GridPos heroStart, GridPos enemyStart,
            IReadOnlyList<HeroAction> referenceSolution, int dashMaxUses = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new LevelFormatException("Level thiếu id.");
            if (width < MinSize || width > MaxSize || height < MinSize || height > MaxSize)
                throw new LevelFormatException($"[{id}] Kích thước {width}x{height} nằm ngoài giới hạn {MinSize}-{MaxSize} mỗi chiều.");
            if (cells == null || cells.Length != width * height)
                throw new LevelFormatException($"[{id}] Số ô không khớp kích thước {width}x{height}.");
            if (dashMaxUses < 0 || dashMaxUses > DashMaxUsesLimit)
                throw new LevelFormatException($"[{id}] Số lần Dash {dashMaxUses} nằm ngoài giới hạn 0-{DashMaxUsesLimit}.");

            DashMaxUses = dashMaxUses;
            Id = id;
            Width = width;
            Height = height;
            this.cells = (CellType[])cells.Clone();
            HeroStart = heroStart;
            EnemyStart = enemyStart;
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
            RequireFloor(enemyStart, "X01");
            if (heroStart == enemyStart) throw new LevelFormatException($"[{id}] Hero và X01 không được trùng ô.");
        }

        public bool IsInside(GridPos p) => p.X >= 0 && p.X < Width && p.Y >= 0 && p.Y < Height;

        public CellType GetCell(GridPos p)
        {
            if (!IsInside(p)) throw new ArgumentOutOfRangeException(nameof(p), $"Ô {p} nằm ngoài bàn {Width}x{Height}.");
            return cells[p.Y * Width + p.X];
        }

        void RequireFloor(GridPos p, string actor)
        {
            if (!IsInside(p)) throw new LevelFormatException($"[{Id}] {actor} ở {p} nằm ngoài bàn.");
            if (GetCell(p) != CellType.Floor) throw new LevelFormatException($"[{Id}] {actor} ở {p} phải đứng trên ô Sàn.");
        }
    }
}
