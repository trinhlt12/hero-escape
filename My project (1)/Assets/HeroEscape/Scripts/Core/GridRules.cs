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
    }

    /// <summary>
    /// Luật một bước, dùng chung cho Hero, X01 và BFS của X01.
    /// Chỉ đọc tọa độ nguyên và dữ liệu level; không dùng Transform hay physics.
    /// </summary>
    public static class GridRules
    {
        /// <summary>
        /// Kiểm tra một bước từ <paramref name="from"/> theo <paramref name="direction"/>.
        /// <paramref name="otherActor"/> là vị trí nhân vật còn lại: vị trí X01 khi Hero đi, vị trí Hero khi X01 đi.
        /// </summary>
        /// <remarks>
        /// - Không ra ngoài bàn, không vào Tường.
        /// - Hero không vào ô X01. X01 được vào ô Hero (bắt), không vào Cửa.
        /// - Bước chéo cần cả hai ô cạnh trực giao không bị chặn. Tường và nhân vật khác chặn góc;
        ///   Cửa chặn góc đối với X01.
        /// </remarks>
        public static StepCheck CheckStep(LevelData level, Mover mover, GridPos from, Direction direction, GridPos otherActor)
        {
            GridPos delta = direction.ToDelta();
            GridPos to = from + delta;

            if (!level.IsInside(to)) return StepCheck.OutOfBounds;

            CellType target = level.GetCell(to);
            if (target == CellType.Wall) return StepCheck.Wall;
            if (target == CellType.Exit && mover == Mover.Enemy) return StepCheck.ExitForbidden;
            if (mover == Mover.Hero && to == otherActor) return StepCheck.Occupied;

            if (direction.IsDiagonal())
            {
                // Ô đích nằm trong bàn nên hai ô cạnh trực giao cũng nằm trong bàn.
                var sideX = new GridPos(from.X + delta.X, from.Y);
                var sideY = new GridPos(from.X, from.Y + delta.Y);
                if (BlocksCorner(level, mover, sideX, otherActor) || BlocksCorner(level, mover, sideY, otherActor))
                    return StepCheck.CornerBlocked;
            }

            return StepCheck.Ok;
        }

        static bool BlocksCorner(LevelData level, Mover mover, GridPos side, GridPos otherActor)
        {
            CellType cell = level.GetCell(side);
            if (cell == CellType.Wall) return true;
            if (cell == CellType.Exit && mover == Mover.Enemy) return true;
            return side == otherActor;
        }
    }
}
