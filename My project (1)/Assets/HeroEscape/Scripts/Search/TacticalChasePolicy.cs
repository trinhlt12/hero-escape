using System.Collections.Generic;
using HeroEscape.Core;

namespace HeroEscape.Search
{
    /// <summary>Số liệu đánh giá một ứng viên của Tactical AI (để giải thích quyết định và kiểm thử).</summary>
    public sealed class TacticalCandidate
    {
        /// <summary>Bước của X; null = Wait.</summary>
        public Direction? Step { get; internal set; }
        public bool Captures { get; internal set; }

        /// <summary>Hero có ít nhất một phản ứng thắng ngay ở lượt tiếp theo.</summary>
        public bool HeroCanWin { get; internal set; }

        /// <summary>Số trạng thái kết quả khác nhau của các phản ứng an toàn (thắng, hoặc còn sống sau pha địch Basic).</summary>
        public int SafeResponses { get; internal set; }

        /// <summary>
        /// Khoảng cách BFS X→Hero lớn nhất mà Hero đạt được bằng một phản ứng an toàn.
        /// <see cref="InfiniteDistance"/> nếu Hero thắng hoặc X không có đường; -1 nếu không có phản ứng an toàn.
        /// </summary>
        public int MaxSafeDistance { get; internal set; }

        public int ResponsesEvaluated { get; internal set; }
        public int DashResponses { get; internal set; }
        public int StasisResponses { get; internal set; }
        public int BarrierResponses { get; internal set; }

        public const int InfiniteDistance = int.MaxValue;

        public override string ToString() =>
            $"{(Step.HasValue ? Step.Value.ToString() : "WAIT")}: win={HeroCanWin} safe={SafeResponses} " +
            $"dist={(MaxSafeDistance == InfiniteDistance ? "inf" : MaxSafeDistance.ToString())} " +
            $"(phản ứng {ResponsesEvaluated}, Dash {DashResponses}, có Stasis {StasisResponses}, có Barrier {BarrierResponses})";
    }

    /// <summary>Một quyết định của Tactical AI.</summary>
    public sealed class TacticalDecision
    {
        public int EnemyIndex { get; internal set; }

        /// <summary>Bước được chọn; null = Wait (đứng yên).</summary>
        public Direction? Chosen { get; internal set; }

        /// <summary>Hết ngân sách: bỏ kết quả dở dang và chọn bằng Basic AI từ trạng thái gốc.</summary>
        public bool UsedFallback { get; internal set; }

        /// <summary>Chọn ngay bước bắt Hero, không tìm kiếm.</summary>
        public bool ImmediateCapture { get; internal set; }

        /// <summary>Số chuyển trạng thái mô phỏng đã dùng (xem <see cref="TacticalChasePolicy"/>).</summary>
        public int Transitions { get; internal set; }

        public IReadOnlyList<TacticalCandidate> Candidates { get; internal set; } = new TacticalCandidate[0];
    }

    /// <summary>
    /// Tactical AI của X01 (GDD mục 5, TDD mục 9), nhìn trước một vòng:
    /// X chọn nước hiện tại → Hero phản ứng đầy đủ (không phụ trợ, Stasis lên từng X, hoặc Barrier ở từng ô kề — tối đa một
    /// phụ trợ theo luật; rồi Move/Wait/Dash với tài nguyên thật) →
    /// một pha địch Basic. Không gọi Tactical đệ quy. Chỉ dùng thông tin công khai của trạng thái; không ngẫu nhiên,
    /// không phụ thuộc thời gian: cùng trạng thái và cấu hình luôn cho cùng quyết định.
    ///
    /// Chọn nước theo thứ tự ưu tiên (so sánh từ điển, không cộng trọng số):
    /// 1. Bắt Hero ngay nếu có bước bắt hợp lệ (không tìm kiếm).
    /// 2. Ứng viên không để Hero thắng ở phản ứng tiếp theo.
    /// 3. Ít phản ứng an toàn của Hero hơn (đếm trạng thái kết quả khác nhau).
    /// 4. Khoảng cách BFS X→Hero nhỏ hơn, giả định Hero chọn phản ứng an toàn xa nhất.
    /// 5. Thứ tự cố định N, NE, E, SE, S, SW, W, NW, Wait.
    ///
    /// Ngân sách: số chuyển trạng thái mô phỏng cho một quyết định. Một chuyển trạng thái = một lần gọi
    /// <see cref="Simulation.ResolveRemainingEnemyPhase"/>, <see cref="Simulation.ApplyStasis(GameState,string)"/>,
    /// <see cref="Simulation.ApplyBarrier"/> hoặc
    /// <see cref="Simulation.Resolve"/> trong tìm kiếm (kể cả lần bị từ chối). BFS đo khoảng cách không tính.
    /// Hết ngân sách: bỏ kết quả dở dang, chọn bằng Basic AI từ trạng thái gốc của quyết định.
    /// </summary>
    public sealed class TacticalChasePolicy : IEnemyPolicy
    {
        public const int DefaultTransitionBudget = 20000;

        static readonly HeroAction[] MovesAndWait =
        {
            HeroAction.Move(Direction.N), HeroAction.Move(Direction.NE), HeroAction.Move(Direction.E), HeroAction.Move(Direction.SE),
            HeroAction.Move(Direction.S), HeroAction.Move(Direction.SW), HeroAction.Move(Direction.W), HeroAction.Move(Direction.NW),
            HeroAction.Wait,
        };

        readonly BasicChasePolicy basicPolicy = new BasicChasePolicy();
        readonly Simulation basicSimulation;

        public TacticalChasePolicy(int transitionBudget = DefaultTransitionBudget)
        {
            TransitionBudget = transitionBudget;
            basicSimulation = new Simulation(basicPolicy);
        }

        public int TransitionBudget { get; }

        /// <summary>Quyết định gần nhất (gồm cả các lần gọi từ preview). Chỉ để chẩn đoán.</summary>
        public TacticalDecision LastDecision { get; private set; }

        /// <summary>Tổng số quyết định và số lần fallback kể từ khi tạo (gồm cả preview). Chỉ để chẩn đoán.</summary>
        public int DecisionCount { get; private set; }
        public int FallbackCount { get; private set; }

        public bool TryChooseStep(GameState state, int enemyIndex, out Direction direction)
        {
            TacticalDecision decision = Decide(state, enemyIndex);
            direction = decision.Chosen ?? default;
            return decision.Chosen.HasValue;
        }

        public TacticalDecision Decide(GameState state, int enemyIndex)
        {
            TacticalDecision decision = Evaluate(state, enemyIndex);
            DecisionCount++;
            if (decision.UsedFallback) FallbackCount++;
            LastDecision = decision;
            return decision;
        }

        TacticalDecision Evaluate(GameState state, int enemyIndex)
        {
            EnemyState self = state.Enemies[enemyIndex];
            GridPos[] others = state.EnemyCells(enemyIndex);

            // A. Ứng viên: các bước hợp lệ theo thứ tự cố định, Wait cuối cùng.
            var steps = new List<Direction?>();
            foreach (Direction direction in Directions.Order)
                if (GridRules.CheckStep(state.Level, Mover.Enemy, self.Pos, direction, state.HeroPos, others, state.Barriers) == StepCheck.Ok)
                    steps.Add(direction);
            steps.Add(null);

            // B. Bắt Hero ngay nếu được.
            foreach (Direction? step in steps)
            {
                if (step.HasValue && self.Pos + step.Value.ToDelta() == state.HeroPos)
                    return new TacticalDecision { EnemyIndex = enemyIndex, Chosen = step, ImmediateCapture = true };
            }

            // C–D. Đánh giá từng ứng viên với ngân sách cố định.
            int transitions = 0;
            var candidates = new List<TacticalCandidate>();
            foreach (Direction? step in steps)
            {
                TacticalCandidate candidate = EvaluateCandidate(state, enemyIndex, step, ref transitions);
                if (candidate == null) return Fallback(state, enemyIndex, transitions);
                candidates.Add(candidate);
            }

            TacticalCandidate best = candidates[0];
            for (int k = 1; k < candidates.Count; k++)
                if (IsBetter(candidates[k], best)) best = candidates[k];

            return new TacticalDecision
            {
                EnemyIndex = enemyIndex,
                Chosen = best.Step,
                Transitions = transitions,
                Candidates = candidates,
            };
        }

        /// <summary>Trả về null nếu hết ngân sách trong lúc đánh giá.</summary>
        TacticalCandidate EvaluateCandidate(GameState state, int enemyIndex, Direction? step, ref int transitions)
        {
            var candidate = new TacticalCandidate { Step = step, MaxSafeDistance = -1 };
            EnemyState self = state.Enemies[enemyIndex];
            GameState afterSelf = step.HasValue ? state.WithEnemy(enemyIndex, self.WithPos(self.Pos + step.Value.ToDelta())) : state;

            // Phần còn lại của pha địch hiện tại: các X sau dùng Basic.
            if (!Spend(ref transitions)) return null;
            GameState heroTurn = basicSimulation.ResolveRemainingEnemyPhase(afterSelf, enemyIndex + 1).NextState;
            if (heroTurn.Outcome == GameOutcome.Lost) return candidate; // X khác bắt Hero: không còn phản ứng an toàn.

            var safeStates = new HashSet<GameState>();

            // Phản ứng của Hero: không dùng thẻ, Stasis lên từng X hợp lệ, hoặc Barrier ở từng ô kề hợp lệ (tối đa một phụ trợ
            // mỗi lượt do Simulation kiểm tra); rồi Move/Wait/Dash với tài nguyên thật. Vật cản tạm tồn tại qua hành động chính
            // và pha địch Basic trong Resolve, rồi bị xóa đúng như gameplay.
            var beforeMain = new List<(SupportCard card, GameState state)> { (SupportCard.None, heroTurn) };
            if (heroTurn.StasisUsesLeft > 0 && !heroTurn.SupportUsedThisTurn)
            {
                foreach (EnemyState enemy in heroTurn.Enemies)
                {
                    if (!Spend(ref transitions)) return null;
                    SupportResult support = basicSimulation.ApplyStasis(heroTurn, enemy.Id);
                    if (support.Accepted) beforeMain.Add((SupportCard.Stasis, support.NextState));
                }
            }

            if (heroTurn.Level.BarrierMaxUses > 0 && heroTurn.BarrierUsesLeft > 0 && !heroTurn.SupportUsedThisTurn)
            {
                foreach (Direction direction in Directions.Order)
                {
                    if (!Spend(ref transitions)) return null;
                    SupportResult support = basicSimulation.ApplyBarrier(heroTurn, heroTurn.HeroPos + direction.ToDelta());
                    if (support.Accepted) beforeMain.Add((SupportCard.Barrier, support.NextState));
                }
            }

            foreach ((SupportCard card, GameState pre) in beforeMain)
            {
                bool dashAvailable = pre.Level.DashMaxUses > 0 && pre.DashUsesLeft > 0;
                for (int a = 0; a < MovesAndWait.Length + (dashAvailable ? 8 : 0); a++)
                {
                    HeroAction action = a < MovesAndWait.Length ? MovesAndWait[a] : HeroAction.Dash(Directions.Order[a - MovesAndWait.Length]);
                    if (!Spend(ref transitions)) return null;
                    TurnResult response = basicSimulation.Resolve(pre, action); // pha địch trong đây là Basic
                    if (!response.Accepted) continue;

                    candidate.ResponsesEvaluated++;
                    if (action.Kind == HeroActionKind.Dash) candidate.DashResponses++;
                    if (card == SupportCard.Stasis) candidate.StasisResponses++;
                    if (card == SupportCard.Barrier) candidate.BarrierResponses++;

                    GameState result = response.NextState;
                    if (result.Outcome == GameOutcome.Lost) continue;

                    safeStates.Add(result);
                    int distance;
                    if (result.Outcome == GameOutcome.Won)
                    {
                        candidate.HeroCanWin = true; // nhánh Hero thắng dừng tại đây
                        distance = TacticalCandidate.InfiniteDistance;
                    }
                    else
                    {
                        List<GridPos> path = EnemyPathfinder.FindPathToHero(result, enemyIndex);
                        distance = path == null ? TacticalCandidate.InfiniteDistance : path.Count;
                    }

                    if (distance > candidate.MaxSafeDistance) candidate.MaxSafeDistance = distance;
                }
            }

            candidate.SafeResponses = safeStates.Count;
            return candidate;
        }

        bool Spend(ref int transitions)
        {
            if (transitions >= TransitionBudget) return false;
            transitions++;
            return true;
        }

        /// <summary>So sánh từ điển: không để Hero thắng → ít phản ứng an toàn → gần hơn. Hòa thì giữ ứng viên đứng trước.</summary>
        static bool IsBetter(TacticalCandidate a, TacticalCandidate b)
        {
            if (a.HeroCanWin != b.HeroCanWin) return !a.HeroCanWin;
            if (a.SafeResponses != b.SafeResponses) return a.SafeResponses < b.SafeResponses;
            return a.MaxSafeDistance < b.MaxSafeDistance;
        }

        TacticalDecision Fallback(GameState state, int enemyIndex, int transitions)
        {
            Direction? chosen = basicPolicy.TryChooseStep(state, enemyIndex, out Direction direction) ? direction : (Direction?)null;
            return new TacticalDecision { EnemyIndex = enemyIndex, Chosen = chosen, UsedFallback = true, Transitions = transitions };
        }
    }

    enum SupportCard
    {
        None,
        Stasis,
        Barrier,
    }

    /// <summary>Tạo AI theo profile cấu hình của level.</summary>
    public static class EnemyPolicies
    {
        public static IEnemyPolicy ForProfile(AiProfile profile, int tacticalBudget = TacticalChasePolicy.DefaultTransitionBudget) =>
            profile == AiProfile.Tactical ? new TacticalChasePolicy(tacticalBudget) : (IEnemyPolicy)new BasicChasePolicy();

        /// <summary>Simulation dùng cho gameplay, preview và công cụ kiểm chứng của level (cùng profile, ngân sách, fallback).</summary>
        public static Simulation CreateSimulation(LevelData level) => new Simulation(ForProfile(level.AiProfile));
    }
}
