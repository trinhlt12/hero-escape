using System;
using System.Collections.Generic;
using HeroEscape.Core;

namespace HeroEscape.Search
{
    /// <summary>
    /// BFS tìm đường ngắn nhất của X01 đến Hero. Mỗi bước (ngang, dọc, chéo) có chi phí 1.
    /// Láng giềng mở rộng theo <see cref="Directions.Order"/>; đánh dấu visited khi đưa vào hàng đợi;
    /// đường đầu tiên chạm Hero là đường được chọn. Nhờ đó phá hòa luôn ổn định.
    /// </summary>
    public static class EnemyPathfinder
    {
        /// <summary>Đường đi khi không có X01 nào khác trên bàn.</summary>
        public static List<GridPos> FindPathToHero(LevelData level, GridPos enemy, GridPos hero) =>
            FindPathToHero(level, enemy, hero, Array.Empty<GridPos>());

        /// <summary>
        /// Trả về các ô trên đường đi (không gồm ô xuất phát, ô cuối là ô Hero), hoặc null nếu không có đường hợp lệ.
        /// <paramref name="otherEnemies"/>: vị trí hiện tại của các X01 khác (kể cả X bị khóa), là vật cản cố định;
        /// không giả định chúng sẽ rời đi. Ô của chính X đang tìm đường không phải vật cản.
        /// </summary>
        public static List<GridPos> FindPathToHero(LevelData level, GridPos enemy, GridPos hero, IReadOnlyList<GridPos> otherEnemies)
        {
            int width = level.Width;
            var previous = new int[width * level.Height];
            var visited = new bool[previous.Length];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;

            var queue = new Queue<GridPos>();
            visited[Index(enemy, width)] = true;
            queue.Enqueue(enemy);

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();
                foreach (Direction direction in Directions.Order)
                {
                    // Cùng luật bước với gameplay: Tường, Cửa, X01 khác, góc chéo; ô Hero là đích hợp lệ và chặn góc.
                    if (GridRules.CheckStep(level, Mover.Enemy, current, direction, hero, otherEnemies) != StepCheck.Ok) continue;

                    GridPos next = current + direction.ToDelta();
                    int nextIndex = Index(next, width);
                    if (visited[nextIndex]) continue;

                    visited[nextIndex] = true;
                    previous[nextIndex] = Index(current, width);
                    if (next == hero) return Reconstruct(previous, nextIndex, Index(enemy, width), width);

                    queue.Enqueue(next);
                }
            }

            return null;
        }

        static List<GridPos> Reconstruct(int[] previous, int targetIndex, int startIndex, int width)
        {
            var path = new List<GridPos>();
            for (int i = targetIndex; i != startIndex; i = previous[i])
                path.Add(new GridPos(i % width, i / width));
            path.Reverse();
            return path;
        }

        static int Index(GridPos p, int width) => p.Y * width + p.X;
    }

    /// <summary>AI Cơ bản của X01: đi bước đầu tiên của đường BFS; không có đường thì đứng yên.</summary>
    public sealed class BasicChasePolicy : IEnemyPolicy
    {
        /// <summary>Chọn bước cho X thứ <paramref name="enemyIndex"/>; các X khác là vật cản tại vị trí hiện tại.</summary>
        public bool TryChooseStep(GameState state, int enemyIndex, out Direction direction)
        {
            GridPos from = state.Enemies[enemyIndex].Pos;
            List<GridPos> path = EnemyPathfinder.FindPathToHero(state.Level, from, state.HeroPos, state.EnemyCells(enemyIndex));
            if (path != null && path.Count > 0 && Directions.TryFromDelta(path[0] - from, out direction))
                return true;

            direction = default;
            return false;
        }

        /// <summary>Lối tắt cho màn một X01: chọn bước cho X đầu tiên.</summary>
        public bool TryChooseStep(GameState state, out Direction direction) => TryChooseStep(state, 0, out direction);
    }
}
