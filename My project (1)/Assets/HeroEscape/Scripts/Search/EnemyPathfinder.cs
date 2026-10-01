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
        /// <summary>
        /// Trả về các ô trên đường đi (không gồm ô xuất phát, ô cuối là ô Hero),
        /// hoặc null nếu không có đường hợp lệ.
        /// </summary>
        public static List<GridPos> FindPathToHero(LevelData level, GridPos enemy, GridPos hero)
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
                    // Cùng luật bước với gameplay: Tường, Cửa, góc chéo; ô Hero là đích hợp lệ và chặn góc.
                    if (GridRules.CheckStep(level, Mover.Enemy, current, direction, hero) != StepCheck.Ok) continue;

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
        public bool TryChooseStep(GameState state, out Direction direction)
        {
            List<GridPos> path = EnemyPathfinder.FindPathToHero(state.Level, state.EnemyPos, state.HeroPos);
            if (path != null && path.Count > 0 && Directions.TryFromDelta(path[0] - state.EnemyPos, out direction))
                return true;

            direction = default;
            return false;
        }
    }
}
