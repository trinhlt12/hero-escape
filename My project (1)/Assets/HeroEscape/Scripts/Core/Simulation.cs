using System;
using System.Collections.Generic;

namespace HeroEscape.Core
{
    /// <summary>Chính sách chọn bước của X01. Core nhận từ ngoài, không phụ thuộc assembly Search.</summary>
    public interface IEnemyPolicy
    {
        /// <summary>
        /// Chọn bước cho X thứ <paramref name="enemyIndex"/> trong <see cref="GameState.Enemies"/>, tính từ trạng thái hiện tại.
        /// Trả về false nếu X đứng yên.
        /// </summary>
        bool TryChooseStep(GameState state, int enemyIndex, out Direction direction);
    }

    public enum ActionError
    {
        None,
        GameOver,
        OutOfBounds,
        Wall,
        OccupiedByEnemy,
        CornerBlocked,
        DashNotAvailable,
        NoDashUsesLeft,
        StasisNotAvailable,
        NoStasisUsesLeft,
        SupportAlreadyUsedThisTurn,
        NoEnemyAtTarget,
        TargetAlreadyLocked,
        TargetOutOfRange,
        NoLineOfSight,
    }

    public enum RuleEventKind
    {
        HeroMoved,
        HeroWaited,
        HeroDashed,
        EnemyMoved,
        EnemyStayed,
        EnemySkipped,
        RunEnded,
    }

    public readonly struct RuleEvent
    {
        public readonly RuleEventKind Kind;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly GameOutcome Outcome;

        /// <summary>ID X01 thực hiện sự kiện; null với sự kiện của Hero hoặc kết thúc trận.</summary>
        public readonly string EnemyId;

        public RuleEvent(RuleEventKind kind, GridPos from, GridPos to, GameOutcome outcome = GameOutcome.Playing, string enemyId = null)
        {
            Kind = kind;
            From = from;
            To = to;
            Outcome = outcome;
            EnemyId = enemyId;
        }

        public override string ToString() =>
            Kind == RuleEventKind.RunEnded ? $"{Kind}({Outcome})" : $"{EnemyId}{Kind}{From}->{To}";
    }

    public sealed class TurnResult
    {
        public bool Accepted { get; }
        public ActionError Error { get; }

        /// <summary>Với Dash bị từ chối do ô: bước bị chặn (1 hoặc 2). Các trường hợp khác là 0.</summary>
        public int FailedDashStep { get; }

        /// <summary>Trạng thái sau toàn bộ lượt. Khi bị từ chối, đúng bằng trạng thái đầu vào.</summary>
        public GameState NextState { get; }

        /// <summary>Trạng thái ngay sau hành động Hero, trước pha địch. Chỉ dùng để trình bày.</summary>
        public GameState AfterHeroState { get; }

        public bool EnemyPhaseRan { get; }
        public IReadOnlyList<RuleEvent> Events { get; }

        TurnResult(bool accepted, ActionError error, int failedDashStep, GameState next, GameState afterHero,
            bool enemyPhaseRan, IReadOnlyList<RuleEvent> events)
        {
            Accepted = accepted;
            Error = error;
            FailedDashStep = failedDashStep;
            NextState = next;
            AfterHeroState = afterHero;
            EnemyPhaseRan = enemyPhaseRan;
            Events = events;
        }

        internal static TurnResult Rejected(GameState state, ActionError error, int failedDashStep = 0) =>
            new TurnResult(false, error, failedDashStep, state, state, false, Array.Empty<RuleEvent>());

        internal static TurnResult Done(GameState next, GameState afterHero, bool enemyPhaseRan, List<RuleEvent> events) =>
            new TurnResult(true, ActionError.None, 0, next, afterHero, enemyPhaseRan, events);
    }

    /// <summary>Kết quả dùng thẻ phụ trợ. Không bao giờ chạy pha địch hay kết thúc lượt Hero.</summary>
    public sealed class SupportResult
    {
        public bool Accepted { get; }
        public ActionError Error { get; }

        /// <summary>Trạng thái sau khi áp thẻ; khi bị từ chối, đúng bằng trạng thái đầu vào.</summary>
        public GameState NextState { get; }

        SupportResult(bool accepted, ActionError error, GameState next)
        {
            Accepted = accepted;
            Error = error;
            NextState = next;
        }

        internal static SupportResult Rejected(GameState state, ActionError error) => new SupportResult(false, error, state);
        internal static SupportResult Done(GameState next) => new SupportResult(true, ActionError.None, next);
    }

    /// <summary>
    /// Chuyển trạng thái một lượt: (thẻ phụ trợ tùy chọn) → hành động Hero → kiểm tra Cửa → pha địch.
    /// Không có ngẫu nhiên; cùng trạng thái và hành động luôn cho cùng kết quả.
    /// Hành động bị từ chối không đổi trạng thái, không tiêu tài nguyên, không chạy pha địch.
    /// </summary>
    public sealed class Simulation
    {
        public const int DashLength = 2;

        /// <summary>Tầm Chebyshev tối đa của Stasis (GDD S01).</summary>
        public const int StasisRange = 3;

        /// <summary>Số pha địch X bị khóa sau Stasis (GDD S01).</summary>
        public const int StasisLockPhases = 1;

        readonly IEnemyPolicy enemyPolicy;

        public Simulation(IEnemyPolicy enemyPolicy)
        {
            this.enemyPolicy = enemyPolicy ?? throw new ArgumentNullException(nameof(enemyPolicy));
        }

        /// <summary>Stasis lên X01 đang đứng ở ô <paramref name="targetCell"/>. Ô không có X01: <see cref="ActionError.NoEnemyAtTarget"/>.</summary>
        public SupportResult ApplyStasis(GameState state, GridPos targetCell)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            int index = state.IndexOfEnemyAt(targetCell);
            return index < 0 ? ApplyStasisChecked(state, -1) : ApplyStasisChecked(state, index);
        }

        /// <summary>
        /// Thẻ Stasis lên X01 có ID <paramref name="enemyId"/>, trước hành động chính của lượt.
        /// Thành công: trừ một lần dùng, đánh dấu đã dùng phụ trợ, khóa riêng X đó một pha địch.
        /// Không chạy pha địch, không kết thúc lượt, không tăng vòng. Sai điều kiện: không đổi gì.
        /// </summary>
        public SupportResult ApplyStasis(GameState state, string enemyId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return ApplyStasisChecked(state, state.IndexOfEnemy(enemyId));
        }

        SupportResult ApplyStasisChecked(GameState state, int index)
        {
            if (state.Outcome != GameOutcome.Playing) return SupportResult.Rejected(state, ActionError.GameOver);
            if (state.Level.StasisMaxUses <= 0) return SupportResult.Rejected(state, ActionError.StasisNotAvailable);
            if (state.StasisUsesLeft <= 0) return SupportResult.Rejected(state, ActionError.NoStasisUsesLeft);
            if (state.SupportUsedThisTurn) return SupportResult.Rejected(state, ActionError.SupportAlreadyUsedThisTurn);
            if (index < 0) return SupportResult.Rejected(state, ActionError.NoEnemyAtTarget);

            EnemyState target = state.Enemies[index];
            if (target.Locked) return SupportResult.Rejected(state, ActionError.TargetAlreadyLocked);
            if (GridRules.Chebyshev(state.HeroPos, target.Pos) > StasisRange) return SupportResult.Rejected(state, ActionError.TargetOutOfRange);
            if (!GridRules.HasLineOfSight(state.Level, state.HeroPos, target.Pos)) return SupportResult.Rejected(state, ActionError.NoLineOfSight);

            return SupportResult.Done(state.WithStasisApplied(index, StasisLockPhases));
        }

        public TurnResult Resolve(GameState state, HeroAction action)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Outcome != GameOutcome.Playing) return TurnResult.Rejected(state, ActionError.GameOver);

            var events = new List<RuleEvent>();
            GameState afterHero;
            // Hero không vào, không xuyên và không lách góc qua bất kỳ X01 nào (kể cả X đang bị khóa).
            GridPos[] enemyCells = state.EnemyCells();

            switch (action.Kind)
            {
                case HeroActionKind.Wait:
                    afterHero = state;
                    events.Add(new RuleEvent(RuleEventKind.HeroWaited, state.HeroPos, state.HeroPos));
                    break;

                case HeroActionKind.Move:
                {
                    StepCheck check = GridRules.CheckStep(state.Level, Mover.Hero, state.HeroPos, action.Direction, state.HeroPos, enemyCells);
                    if (check != StepCheck.Ok) return TurnResult.Rejected(state, ToError(check));

                    GridPos to = state.HeroPos + action.Direction.ToDelta();
                    afterHero = state.WithHero(to);
                    events.Add(new RuleEvent(RuleEventKind.HeroMoved, state.HeroPos, to));
                    break;
                }

                case HeroActionKind.Dash:
                {
                    if (state.Level.DashMaxUses <= 0) return TurnResult.Rejected(state, ActionError.DashNotAvailable);
                    if (state.DashUsesLeft <= 0) return TurnResult.Rejected(state, ActionError.NoDashUsesLeft);

                    // Kiểm tra trọn đường Dash trước khi đổi trạng thái: không có di chuyển một phần.
                    GridPos current = state.HeroPos;
                    for (int step = 1; step <= DashLength; step++)
                    {
                        StepCheck check = GridRules.CheckStep(state.Level, Mover.Hero, current, action.Direction, current, enemyCells);
                        if (check != StepCheck.Ok) return TurnResult.Rejected(state, ToError(check), step);

                        current += action.Direction.ToDelta();
                        // Tới Cửa ở bất kỳ bước nào: dừng tại đó, không kiểm tra bước còn lại.
                        if (state.Level.GetCell(current) == CellType.Exit) break;
                    }

                    afterHero = state.WithHero(current).WithDashUsed();
                    events.Add(new RuleEvent(RuleEventKind.HeroDashed, state.HeroPos, current));
                    break;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Hành động không xác định.");
            }

            // Đến Cửa: thắng ngay, không X01 nào hành động.
            if (state.Level.GetCell(afterHero.HeroPos) == CellType.Exit)
            {
                GameState won = afterHero.WithOutcome(GameOutcome.Won);
                events.Add(new RuleEvent(RuleEventKind.RunEnded, afterHero.HeroPos, afterHero.HeroPos, GameOutcome.Won));
                return TurnResult.Done(won, won, false, events);
            }

            return ResolveEnemyPhase(afterHero, events);
        }

        /// <summary>
        /// Pha địch: mỗi X01 hành động đúng một lần theo SpawnIndex tăng dần. Mỗi X tính lại từ trạng thái
        /// sau khi X trước đã hành động (không di chuyển đồng thời). X bắt Hero thì thua ngay và dừng cả pha.
        /// </summary>
        TurnResult ResolveEnemyPhase(GameState afterHero, List<RuleEvent> events)
        {
            GameState next = afterHero;
            for (int i = 0; i < next.Enemies.Count; i++)
            {
                EnemyState enemy = next.Enemies[i];
                if (enemy.Locked)
                {
                    // Bị khóa: bỏ qua toàn bộ hành động trong pha này rồi giảm thời hạn khóa của riêng X này. X vẫn chiếm ô.
                    next = next.WithEnemy(i, enemy.WithLockPhases(enemy.LockPhases - 1));
                    events.Add(new RuleEvent(RuleEventKind.EnemySkipped, enemy.Pos, enemy.Pos, enemyId: enemy.Id));
                    continue;
                }

                if (!enemyPolicy.TryChooseStep(next, i, out Direction direction))
                {
                    events.Add(new RuleEvent(RuleEventKind.EnemyStayed, enemy.Pos, enemy.Pos, enemyId: enemy.Id));
                    continue;
                }

                // Chính sách AI phải tuân thủ cùng luật bước; vi phạm là lỗi lập trình.
                StepCheck check = GridRules.CheckStep(next.Level, Mover.Enemy, enemy.Pos, direction, next.HeroPos, next.EnemyCells(i));
                if (check != StepCheck.Ok)
                    throw new InvalidOperationException($"Chính sách X01 '{enemy.Id}' chọn bước {direction} không hợp lệ ({check}) tại {next}.");

                GridPos to = enemy.Pos + direction.ToDelta();
                next = next.WithEnemy(i, enemy.WithPos(to));
                events.Add(new RuleEvent(RuleEventKind.EnemyMoved, enemy.Pos, to, enemyId: enemy.Id));

                if (to == next.HeroPos)
                {
                    next = next.WithOutcome(GameOutcome.Lost);
                    events.Add(new RuleEvent(RuleEventKind.RunEnded, to, to, GameOutcome.Lost));
                    return TurnResult.Done(next, afterHero, true, events);
                }
            }

            return TurnResult.Done(next.WithNextRound(), afterHero, true, events);
        }

        static ActionError ToError(StepCheck check)
        {
            switch (check)
            {
                case StepCheck.OutOfBounds: return ActionError.OutOfBounds;
                case StepCheck.Wall: return ActionError.Wall;
                case StepCheck.Occupied: return ActionError.OccupiedByEnemy;
                case StepCheck.CornerBlocked: return ActionError.CornerBlocked;
                default: throw new ArgumentOutOfRangeException(nameof(check), check, "Bước của Hero không thể trả lỗi này.");
            }
        }
    }
}
