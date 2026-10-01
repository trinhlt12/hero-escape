using System.Collections.Generic;
using System.Linq;
using HeroEscape.Core;

namespace HeroEscape.Tests
{
    public enum ReachVerdict
    {
        /// <summary>Tìm thấy chuỗi hành động dẫn tới Thắng.</summary>
        Solved,

        /// <summary>Đã duyệt hết mọi trạng thái đạt tới được mà không có Thắng.</summary>
        Unsolvable,

        /// <summary>Dừng vì giới hạn trạng thái; không kết luận được.</summary>
        Unknown,
    }

    /// <summary>
    /// Công cụ kiểm chứng NHỎ, chỉ dùng trong test cho các màn chơi thử (không phải solver sản phẩm).
    /// BFS trên trạng thái (Hero, X01, Dash còn lại) bằng chính Simulation + AI của game.
    /// Round không nằm trong khóa vì không ảnh hưởng diễn biến về sau.
    /// </summary>
    static class LevelReachability
    {
        public const int DefaultStateCap = 100000;

        static readonly HeroAction[] MoveAndWait =
            Directions.Order.Select(HeroAction.Move).Append(HeroAction.Wait).ToArray();

        static readonly HeroAction[] Dashes = Directions.Order.Select(HeroAction.Dash).ToArray();

        public sealed class Result
        {
            public ReachVerdict Verdict;
            public int StatesVisited;
            public List<HeroAction> Solution;
        }

        /// <param name="maxDashes">Số Dash tối đa được phép dùng trong cả lời giải (0 = chỉ Move/Wait).</param>
        public static Result Explore(GameState start, Simulation simulation, int maxDashes, int stateCap = DefaultStateCap)
        {
            var seen = new HashSet<(GridPos, GridPos, int)> { Key(start) };
            var previous = new Dictionary<(GridPos, GridPos, int), ((GridPos, GridPos, int) from, HeroAction action)>();
            var queue = new Queue<GameState>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                if (seen.Count > stateCap) return new Result { Verdict = ReachVerdict.Unknown, StatesVisited = seen.Count };

                GameState state = queue.Dequeue();
                int dashesUsed = start.DashUsesLeft - state.DashUsesLeft;
                IEnumerable<HeroAction> actions = dashesUsed < maxDashes ? MoveAndWait.Concat(Dashes) : MoveAndWait;

                foreach (HeroAction action in actions)
                {
                    TurnResult result = simulation.Resolve(state, action);
                    if (!result.Accepted || result.NextState.Outcome == GameOutcome.Lost) continue;

                    if (result.NextState.Outcome == GameOutcome.Won)
                    {
                        var path = new List<HeroAction> { action };
                        for (var k = Key(state); previous.TryGetValue(k, out var step); k = step.from) path.Insert(0, step.action);
                        return new Result { Verdict = ReachVerdict.Solved, StatesVisited = seen.Count, Solution = path };
                    }

                    var nextKey = Key(result.NextState);
                    if (seen.Add(nextKey))
                    {
                        previous[nextKey] = (Key(state), action);
                        queue.Enqueue(result.NextState);
                    }
                }
            }

            return new Result { Verdict = ReachVerdict.Unsolvable, StatesVisited = seen.Count };
        }

        static (GridPos, GridPos, int) Key(GameState s) => (s.HeroPos, s.EnemyPos, s.DashUsesLeft);
    }
}
