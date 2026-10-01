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
    }

    public enum RuleEventKind
    {
        HeroMoved,
        HeroWaited,
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

        /// <summary>Trạng thái sau toàn bộ lượt. Khi bị từ chối, đúng bằng trạng thái đầu vào.</summary>
        public GameState NextState { get; }

        /// <summary>Trạng thái ngay sau hành động Hero, trước pha X01. Chỉ dùng để trình bày.</summary>
        public GameState AfterHeroState { get; }

        public bool EnemyPhaseRan { get; }
        public IReadOnlyList<RuleEvent> Events { get; }

        TurnResult(bool accepted, ActionError error, GameState next, GameState afterHero, bool enemyPhaseRan,
            IReadOnlyList<RuleEvent> events)
        {
            Accepted = accepted;
            Error = error;
            NextState = next;
            AfterHeroState = afterHero;
            EnemyPhaseRan = enemyPhaseRan;
            Events = events;
        }

        internal static TurnResult Rejected(GameState state, ActionError error) =>
            new TurnResult(false, error, state, state, false, Array.Empty<RuleEvent>());

        internal static TurnResult Done(GameState next, GameState afterHero, bool enemyPhaseRan, List<RuleEvent> events) =>
            new TurnResult(true, ActionError.None, next, afterHero, enemyPhaseRan, events);
    }

    /// <summary>
    /// Chuyển trạng thái một lượt: hành động Hero → kiểm tra Cửa → pha X01.
    /// Không có ngẫu nhiên; cùng trạng thái và hành động luôn cho cùng kết quả.
    /// </summary>
    public sealed class Simulation
    {
        readonly IEnemyPolicy enemyPolicy;

        public Simulation(IEnemyPolicy enemyPolicy)
        {
            this.enemyPolicy = enemyPolicy ?? throw new ArgumentNullException(nameof(enemyPolicy));
        }

        public TurnResult Resolve(GameState state, HeroAction action)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Outcome != GameOutcome.Playing) return TurnResult.Rejected(state, ActionError.GameOver);

            LevelData level = state.Level;
            var events = new List<RuleEvent>();
            GameState afterHero;

            if (action.Kind == HeroActionKind.Wait)
            {
                afterHero = state;
                events.Add(new RuleEvent(RuleEventKind.HeroWaited, state.HeroPos, state.HeroPos));
            }
            else
            {
                StepCheck check = GridRules.CheckStep(level, Mover.Hero, state.HeroPos, action.Direction, state.EnemyPos);
                if (check != StepCheck.Ok) return TurnResult.Rejected(state, ToError(check));

                GridPos to = state.HeroPos + action.Direction.ToDelta();
                afterHero = state.WithHero(to);
                events.Add(new RuleEvent(RuleEventKind.HeroMoved, state.HeroPos, to));

                // Đến Cửa: thắng ngay, không chạy pha X01.
                if (level.GetCell(to) == CellType.Exit)
                {
                    GameState won = afterHero.WithOutcome(GameOutcome.Won);
                    events.Add(new RuleEvent(RuleEventKind.RunEnded, to, to, GameOutcome.Won));
                    return TurnResult.Done(won, won, false, events);
                }
            }

            GameState next = afterHero;
            if (enemyPolicy.TryChooseStep(afterHero, out Direction enemyDirection))
            {
                // Chính sách AI phải tuân thủ cùng luật bước; vi phạm là lỗi lập trình.
                StepCheck enemyCheck = GridRules.CheckStep(level, Mover.Enemy, afterHero.EnemyPos, enemyDirection, afterHero.HeroPos);
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
