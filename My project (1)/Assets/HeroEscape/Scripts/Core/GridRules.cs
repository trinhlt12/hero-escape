using System;
using System.Collections.Generic;

namespace HeroEscape.Core
{
    public enum Mover
    {
        Hero,
        Enemy,
    }

    public enum StepCheck
    {
        Ok,
        OutOfBounds,
        Wall,
        ExitForbidden,
        Occupied,
        CornerBlocked,

        /// <summary>Ô đích có vật cản tạm (Barrier). Chặn như Tường nhưng báo riêng để giải thích cho người chơi.</summary>
        Barrier,
    }

    /// <summary>
    /// Luật một bước, dùng chung cho Hero, X01 và BFS của X01.
    /// Chỉ đọc tọa độ nguyên và dữ liệu level; không dùng Transform hay physics.
    /// </summary>
    public static class GridRules
    {
        /// <summary>
        /// Kiểm tra một bước khi chỉ có hai nhân vật (Hero và một X01).
        /// <paramref name="otherActor"/> là vị trí nhân vật còn lại: vị trí X01 khi Hero đi, vị trí Hero khi X01 đi.
        /// </summary>
        public static StepCheck CheckStep(LevelData level, Mover mover, GridPos from, Direction direction, GridPos otherActor) =>
            mover == Mover.Hero
                ? CheckStep(level, Mover.Hero, from, direction, from, new[] { otherActor })
                : CheckStep(level, Mover.Enemy, from, direction, otherActor, Array.Empty<GridPos>());

        /// <summary>
        /// Kiểm tra một bước từ <paramref name="from"/> theo <paramref name="direction"/>.
        /// <paramref name="heroPos"/>: vị trí Hero (khi Hero đi thì bằng <paramref name="from"/>).
        /// <paramref name="enemyCells"/>: vị trí các X01 khác người đang đi (Hero đi: mọi X01). X01 bị khóa vẫn tính.
        /// </summary>
        /// <remarks>
        /// - Không ra ngoài bàn, không vào Tường.
        /// - Không vào ô đang có X01 (Hero và X01 đều vậy). X01 được vào ô Hero (bắt), không vào Cửa.
        /// - Bước chéo cần cả hai ô cạnh trực giao không bị chặn. Tường, vật cản tạm và mọi nhân vật khác chặn góc;
        ///   Cửa chặn góc đối với X01.
        /// </remarks>
        public static StepCheck CheckStep(LevelData level, Mover mover, GridPos from, Direction direction,
            GridPos heroPos, IReadOnlyList<GridPos> enemyCells) =>
            CheckStep(level, mover, from, direction, heroPos, enemyCells, Array.Empty<GridPos>());

        /// <summary>
        /// Như trên, kèm <paramref name="barriers"/>: các ô có vật cản tạm (lớp riêng, không đổi địa hình gốc).
        /// Vật cản tạm chặn ô đích và chặn lách góc chéo đúng như Tường, với cả Hero và X01.
        /// </summary>
        public static StepCheck CheckStep(LevelData level, Mover mover, GridPos from, Direction direction,
            GridPos heroPos, IReadOnlyList<GridPos> enemyCells, IReadOnlyList<GridPos> barriers)
        {
            GridPos delta = direction.ToDelta();
            GridPos to = from + delta;

            if (!level.IsInside(to)) return StepCheck.OutOfBounds;

            CellType target = level.GetCell(to);
            if (target == CellType.Wall) return StepCheck.Wall;
            if (Contains(barriers, to)) return StepCheck.Barrier;
            if (target == CellType.Exit && mover == Mover.Enemy) return StepCheck.ExitForbidden;
            if (Contains(enemyCells, to)) return StepCheck.Occupied;

            if (direction.IsDiagonal())
            {
                // Ô đích nằm trong bàn nên hai ô cạnh trực giao cũng nằm trong bàn.
                var sideX = new GridPos(from.X + delta.X, from.Y);
                var sideY = new GridPos(from.X, from.Y + delta.Y);
                if (BlocksCorner(level, mover, sideX, heroPos, enemyCells, barriers) || BlocksCorner(level, mover, sideY, heroPos, enemyCells, barriers))
                    return StepCheck.CornerBlocked;
            }

            return StepCheck.Ok;
        }

        static bool BlocksCorner(LevelData level, Mover mover, GridPos side, GridPos heroPos, IReadOnlyList<GridPos> enemyCells,
            IReadOnlyList<GridPos> barriers)
        {
            CellType cell = level.GetCell(side);
            if (cell == CellType.Wall || Contains(barriers, side)) return true;
            if (cell == CellType.Exit && mover == Mover.Enemy) return true;
            if (mover == Mover.Enemy && side == heroPos) return true;
            return Contains(enemyCells, side);
        }

        public static bool Contains(IReadOnlyList<GridPos> cells, GridPos p)
        {
            for (int i = 0; i < cells.Count; i++)
                if (cells[i] == p) return true;
            return false;
        }

        /// <summary>Khoảng cách Chebyshev: max(|dx|, |dy|). Dùng để đo tầm kỹ năng, không phải đường đi.</summary>
        public static int Chebyshev(GridPos a, GridPos b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        /// <summary>
        /// Đường nhìn từ tâm ô <paramref name="from"/> đến tâm ô <paramref name="to"/>, bằng supercover nguyên trên lưới.
        /// Chỉ Tường chắn; nhân vật và Cửa không chắn. Bỏ qua ô nguồn và ô đích (loại ô đích do luật chọn mục tiêu xét).
        /// Khi tia đi đúng qua một góc, cả hai ô cạnh tại góc đó đều được xét; một trong hai là Tường thì bị chắn.
        /// </summary>
        public static bool HasLineOfSight(LevelData level, GridPos from, GridPos to) =>
            HasLineOfSight(level, from, to, Array.Empty<GridPos>());

        /// <summary>Như trên; vật cản tạm <paramref name="barriers"/> chắn tia như Tường (TDD mục 6).</summary>
        public static bool HasLineOfSight(LevelData level, GridPos from, GridPos to, IReadOnlyList<GridPos> barriers)
        {
            foreach (GridPos cell in SupercoverCells(from, to))
            {
                if (cell == from || cell == to) continue;
                if (level.GetCell(cell) == CellType.Wall || Contains(barriers, cell)) return false;
            }

            return true;
        }

        /// <summary>
        /// Mọi ô mà đoạn thẳng nối tâm hai ô đi qua hoặc chạm, theo thứ tự từ nguồn đến đích.
        /// Khi đoạn thẳng đi đúng qua góc chung của bốn ô, trả về cả hai ô cạnh tại góc đó.
        /// </summary>
        public static List<GridPos> SupercoverCells(GridPos from, GridPos to)
        {
            int nx = Math.Abs(to.X - from.X);
            int ny = Math.Abs(to.Y - from.Y);
            int sx = Math.Sign(to.X - from.X);
            int sy = Math.Sign(to.Y - from.Y);

            var cells = new List<GridPos> { from };
            int x = from.X, y = from.Y;
            for (int ix = 0, iy = 0; ix < nx || iy < ny;)
            {
                // So sánh vị trí giao cạnh dọc tiếp theo (ix + 0.5) / nx với cạnh ngang tiếp theo (iy + 0.5) / ny,
                // nhân chéo để giữ số nguyên.
                int decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
                if (decision == 0)
                {
                    // Đi đúng qua góc: thêm cả hai ô cạnh rồi sang ô chéo.
                    cells.Add(new GridPos(x + sx, y));
                    cells.Add(new GridPos(x, y + sy));
                    x += sx;
                    y += sy;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    x += sx;
                    ix++;
                }
                else
                {
                    y += sy;
                    iy++;
                }

                cells.Add(new GridPos(x, y));
            }

            return cells;
        }
    }
}
