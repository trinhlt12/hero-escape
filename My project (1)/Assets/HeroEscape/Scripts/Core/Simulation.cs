using System;
using System.Collections.Generic;

namespace HeroEscape.Core
{
    /// <summary>Chính sách chọn bước của X01. Core nhận từ ngoài, không phụ thuộc assembly Search.</summary>
    public interface IEnemyPolicy
    {
        /// <summary>Trả về false nếu X01 đứng yên.</summary>
        bool TryChooseStep(GameState state, out Direction direction);
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
    }

    public enum RuleEventKind
    {
        HeroMoved,
        HeroWaited,
        HeroDashed,
        EnemyMoved,
        EnemyStayed,
        RunEnded,
    }

    public readonly struct RuleEvent
    {
        public readonly RuleEventKind Kind;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly GameOutcome Outcome;

        public RuleEvent(RuleEventKind kind, GridPos from, GridPos to, GameOutcome outcome = GameOutcome.Playing)
        {
            Kind = kind;
            From = from;
            To = to;
            Outcome = outcome;
        }

        public override string ToString() => Kind == RuleEventKind.RunEnded ? $"{Kind}({Outcome})" : $"{Kind}{From}->{To}";
    }

    public sealed class TurnResult
    {
        public bool Accepted { get; }
        public ActionError Error { get; }

        /// <summary>Với Dash bị từ chối do ô: bước bị chặn (1 hoặc 2). Các trường hợp khác là 0.</summary>
        public int FailedDashStep { get; }

        /// <summary>Trạng thái sau toàn bộ lượt. Khi bị từ chối, đúng bằng trạng thái đầu vào.</summary>
        public GameState NextState { get; }

        /// <summary>Trạng thái ngay sau hành động Hero, trước pha X01. Chỉ dùng để trình bày.</summary>
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

    /// <summary>
    /// Chuyển trạng thái một lượt: hành động Hero → kiểm tra Cửa → pha X01.
    /// Không có ngẫu nhiên; cùng trạng thái và hành động luôn cho cùng kết quả.
    /// Hành động bị từ chối không đổi trạng thái, không tiêu tài nguyên, không chạy X01.
    /// </summary>
    public sealed class Simulation
    {
        public const int DashLength = 2;

        readonly IEnemyPolicy enemyPolicy;

        public Simulation(IEnemyPolicy enemyPolicy)
        {
            this.enemyPolicy = enemyPolicy ?? throw new ArgumentNullException(nameof(enemyPolicy));
        }

        public TurnResult Resolve(GameState state, HeroAction action)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Outcome != GameOutcome.Playing) return TurnResult.Rejected(state, ActionError.GameOver);

            var events = new List<RuleEvent>();
            GameState afterHero;

            switch (action.Kind)
            {
                case HeroActionKind.Wait:
                    afterHero = state;
                    events.Add(new RuleEvent(RuleEventKind.HeroWaited, state.HeroPos, state.HeroPos));
                    break;

                case HeroActionKind.Move:
                {
                    StepCheck check = GridRules.CheckStep(state.Level, Mover.Hero, state.HeroPos, action.Direction, state.EnemyPos);
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
                        StepCheck check = GridRules.CheckStep(state.Level, Mover.Hero, current, action.Direction, state.EnemyPos);
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

            // Đến Cửa: thắng ngay, không chạy pha X01.
            if (state.Level.GetCell(afterHero.HeroPos) == CellType.Exit)
            {
                GameState won = afterHero.WithOutcome(GameOutcome.Won);
                events.Add(new RuleEvent(RuleEventKind.RunEnded, afterHero.HeroPos, afterHero.HeroPos, GameOutcome.Won));
                return TurnResult.Done(won, won, false, events);
            }

            return ResolveEnemyPhase(afterHero, events);
        }

        /// <summary>Đúng một lượt X01 sau toàn bộ hành động của Hero.</summary>
        TurnResult ResolveEnemyPhase(GameState afterHero, List<RuleEvent> events)
        {
            GameState next = afterHero;
            if (enemyPolicy.TryChooseStep(afterHero, out Direction enemyDirection))
            {
                // Chính sách AI phải tuân thủ cùng luật bước; vi phạm là lỗi lập trình.
                StepCheck enemyCheck = GridRules.CheckStep(afterHero.Level, Mover.Enemy, afterHero.EnemyPos, enemyDirection, afterHero.HeroPos);
                if (enemyCheck != StepCheck.Ok)
                    throw new InvalidOperationException($"Chính sách X01 chọn bước {enemyDirection} không hợp lệ ({enemyCheck}) tại {afterHero}.");

                GridPos enemyTo = afterHero.EnemyPos + enemyDirection.ToDelta();
                next = afterHero.WithEnemy(enemyTo);
                events.Add(new RuleEvent(RuleEventKind.EnemyMoved, afterHero.EnemyPos, enemyTo));

                if (enemyTo == afterHero.HeroPos)
                {
                    next = next.WithOutcome(GameOutcome.Lost);
                    events.Add(new RuleEvent(RuleEventKind.RunEnded, enemyTo, enemyTo, GameOutcome.Lost));
                    return TurnResult.Done(next, afterHero, true, events);
                }
            }
            else
            {
                events.Add(new RuleEvent(RuleEventKind.EnemyStayed, afterHero.EnemyPos, afterHero.EnemyPos));
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
