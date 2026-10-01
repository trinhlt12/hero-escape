using System.Collections.Generic;
using System.Linq;
using HeroEscape.Core;

namespace HeroEscape.Tests
{
    public enum ReachVerdict
    {
        /// <summary>Tìm thấy chuỗi lượt dẫn tới Thắng.</summary>
        Solved,

        /// <summary>Đã duyệt hết mọi trạng thái đạt tới được mà không có Thắng.</summary>
        Unsolvable,

        /// <summary>Dừng vì giới hạn trạng thái; không kết luận được.</summary>
        Unknown,
    }

    /// <summary>Một lượt Hero trong lời giải: Stasis tùy chọn lên một X01 (theo ID), rồi hành động chính.</summary>
    public readonly struct PlannedTurn
    {
        /// <summary>ID X01 bị Stasis trong lượt này; null nếu không dùng thẻ.</summary>
        public readonly string StasisTarget;

        /// <summary>Dùng Stasis nhưng không ghi ID: chỉ hợp lệ khi màn có đúng một X01.</summary>
        readonly bool stasisOnOnlyEnemy;

        public readonly HeroAction Main;

        public PlannedTurn(string stasisTarget, HeroAction main)
        {
            StasisTarget = stasisTarget;
            stasisOnOnlyEnemy = false;
            Main = main;
        }

        /// <summary>Dạng cũ cho màn một X01.</summary>
        public PlannedTurn(bool useStasis, HeroAction main)
        {
            StasisTarget = null;
            stasisOnOnlyEnemy = useStasis;
            Main = main;
        }

        public bool UseStasis => StasisTarget != null || stasisOnOnlyEnemy;

        public string ResolveTarget(GameState state)
        {
            if (StasisTarget != null) return StasisTarget;
            if (state.Enemies.Count != 1)
                throw new System.InvalidOperationException("Lượt dùng Stasis không ghi mục tiêu nhưng màn có nhiều X01.");
            return state.Enemies[0].Id;
        }

        public override string ToString() =>
            (StasisTarget != null ? $"STASIS({StasisTarget})+" : stasisOnOnlyEnemy ? LevelParser.StasisPrefix : string.Empty) + Main;
    }

    /// <summary>
    /// Công cụ kiểm chứng NHỎ, chỉ dùng trong test cho các màn chơi thử (không phải solver sản phẩm).
    /// BFS bằng chính Simulation + AI của game. Một cạnh = một lượt Hero: không dùng phụ trợ, hoặc Stasis lên
    /// một X01 theo ID, rồi một hành động chính hợp lệ. Khóa trạng thái gồm vị trí Hero; vị trí và thời hạn khóa
    /// của từng X01 theo thứ tự SpawnIndex (hai X hoán đổi vị trí là hai trạng thái khác nhau); Dash và Stasis còn
    /// lại; cờ đã dùng phụ trợ. Round không nằm trong khóa vì không ảnh hưởng diễn biến về sau.
    /// </summary>
    static class LevelReachability
    {
        public const int DefaultStateCap = 100000;

        static readonly HeroAction[] MoveAndWait =
            Directions.Order.Select(HeroAction.Move).Append(HeroAction.Wait).ToArray();

        static readonly HeroAction[] Dashes = Directions.Order.Select(HeroAction.Dash).ToArray();

        public static readonly HeroAction[] AllMainActions = MoveAndWait.Concat(Dashes).ToArray();

        public sealed class Result
        {
            public ReachVerdict Verdict;
            public int StatesVisited;
            public List<PlannedTurn> Solution;
        }

        /// <param name="maxDashes">Số Dash tối đa trong cả lời giải (0 = không Dash).</param>
        /// <param name="maxStasis">Số lần Stasis tối đa trong cả lời giải (0 = không Stasis).</param>
        public static Result Explore(GameState start, Simulation simulation, int maxDashes, int maxStasis = 0,
            int stateCap = DefaultStateCap)
        {
            var seen = new HashSet<string> { Key(start) };
            var previous = new Dictionary<string, (string from, PlannedTurn turn)>();
            var queue = new Queue<GameState>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                if (seen.Count > stateCap) return new Result { Verdict = ReachVerdict.Unknown, StatesVisited = seen.Count };

                GameState state = queue.Dequeue();
                foreach ((string target, GameState beforeMain) in SupportOptions(state, simulation, start, maxStasis))
                {
                    bool dashAllowed = start.DashUsesLeft - beforeMain.DashUsesLeft < maxDashes;
                    foreach (HeroAction action in dashAllowed ? AllMainActions : MoveAndWait)
                    {
                        TurnResult result = simulation.Resolve(beforeMain, action);
                        if (!result.Accepted || result.NextState.Outcome == GameOutcome.Lost) continue;

                        var turn = new PlannedTurn(target, action);
                        if (result.NextState.Outcome == GameOutcome.Won)
                        {
                            var path = new List<PlannedTurn> { turn };
                            for (string k = Key(state); previous.TryGetValue(k, out var step); k = step.from) path.Insert(0, step.turn);
                            return new Result { Verdict = ReachVerdict.Solved, StatesVisited = seen.Count, Solution = path };
                        }

                        string nextKey = Key(result.NextState);
                        if (seen.Add(nextKey))
                        {
                            previous[nextKey] = (Key(state), turn);
                            queue.Enqueue(result.NextState);
                        }
                    }
                }
            }

            return new Result { Verdict = ReachVerdict.Unsolvable, StatesVisited = seen.Count };
        }

        /// <summary>Phát lại chuỗi lượt bằng Simulation thật; trả về trạng thái cuối, hoặc null nếu một bước bị từ chối.</summary>
        public static GameState Replay(GameState start, Simulation simulation, IEnumerable<PlannedTurn> turns)
        {
            GameState state = start;
            foreach (PlannedTurn turn in turns)
            {
                if (turn.UseStasis)
                {
                    SupportResult support = simulation.ApplyStasis(state, turn.ResolveTarget(state));
                    if (!support.Accepted) return null;
                    state = support.NextState;
                }

                TurnResult result = simulation.Resolve(state, turn.Main);
                if (!result.Accepted) return null;
                state = result.NextState;
            }

            return state;
        }

        /// <summary>Lời giải tham chiếu trong file level, dạng chuỗi lượt.</summary>
        public static List<PlannedTurn> ReferenceTurns(LevelData level) =>
            level.ReferenceSolution
                .Select((action, i) => new PlannedTurn(level.ReferenceStasisTargets.TryGetValue(i, out string id) ? id : null, action))
                .ToList();

        /// <summary>Các lựa chọn trước hành động chính: không dùng thẻ, hoặc Stasis lên từng X01 hợp lệ (theo ID).</summary>
        public static IEnumerable<(string target, GameState beforeMain)> SupportOptions(GameState state, Simulation simulation,
            GameState origin, int maxStasis)
        {
            yield return (null, state);
            if (origin.StasisUsesLeft - state.StasisUsesLeft >= maxStasis) yield break;

            foreach (EnemyState enemy in state.Enemies)
            {
                SupportResult support = simulation.ApplyStasis(state, enemy.Id);
                if (support.Accepted) yield return (enemy.Id, support.NextState);
            }
        }

        static string Key(GameState s)
        {
            var key = new System.Text.StringBuilder();
            key.Append(s.HeroPos).Append('|');
            foreach (EnemyState e in s.Enemies) key.Append(e.Id).Append(e.Pos).Append(e.LockPhases).Append(';');
            return key.Append('|').Append(s.DashUsesLeft).Append('|').Append(s.StasisUsesLeft).Append('|').Append(s.SupportUsedThisTurn).ToString();
        }
    }
}
