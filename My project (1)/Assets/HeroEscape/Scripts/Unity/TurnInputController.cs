using System;
using HeroEscape.Core;

namespace HeroEscape.Unity
{
    public enum InputMode
    {
        Normal,
        ChoosingDashDirection,
        ChoosingStasisTarget,
    }

    /// <summary>Kết quả một thao tác input: lượt đã gửi (nếu có) và phản hồi cho người chơi.</summary>
    public readonly struct InputResponse
    {
        public static readonly InputResponse None = new InputResponse(null, null);

        /// <summary>Lượt được Core chấp nhận; null nếu không có hành động nào được thực hiện.</summary>
        public readonly TurnResult Turn;

        /// <summary>Phản hồi hiển thị. null = giữ nguyên dòng thông báo hiện tại.</summary>
        public readonly string Feedback;

        public InputResponse(TurnResult turn, string feedback)
        {
            Turn = turn;
            Feedback = feedback;
        }

        public bool Acted => Turn != null;
    }

    /// <summary>
    /// Trạng thái giao diện của lượt Hero (chế độ chọn hướng Dash) và định tuyến input sang GameSession.
    /// Không dùng UnityEngine; luật và tài nguyên vẫn nằm trong Core, lớp này chỉ gửi hành động.
    /// </summary>
    public sealed class TurnInputController
    {
        readonly GameSession session;

        public TurnInputController(GameSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public InputMode Mode { get; private set; } = InputMode.Normal;

        /// <summary>Lớp hiển thị đang trình bày lượt; mọi input gameplay bị chặn.</summary>
        public bool Busy { get; set; }

        public GameSession Session => session;
        public bool LevelHasDash => session.Level.DashMaxUses > 0;
        public bool LevelHasStasis => session.Level.StasisMaxUses > 0;
        public bool CanAct => !Busy && session.Current.Outcome == GameOutcome.Playing;

        /// <summary>Wait chỉ dùng được khi không đang chọn hướng Dash hay mục tiêu Stasis.</summary>
        public bool CanWait => CanAct && Mode == InputMode.Normal;

        /// <summary>Nút Dash bấm được khi còn lượt dùng, hoặc đang chọn hướng (để hủy).</summary>
        public bool CanPressDash =>
            CanAct && LevelHasDash && (Mode == InputMode.ChoosingDashDirection || session.Current.DashUsesLeft > 0);

        /// <summary>Nút Stasis bấm được khi còn thẻ và chưa dùng phụ trợ lượt này, hoặc đang chọn mục tiêu (để hủy).</summary>
        public bool CanPressStasis =>
            CanAct && LevelHasStasis && (Mode == InputMode.ChoosingStasisTarget ||
                                         (session.Current.StasisUsesLeft > 0 && !session.Current.SupportUsedThisTurn));

        /// <summary>
        /// Bấm lên vùng bàn cờ. <paramref name="pointerOverUi"/> = con trỏ đang nằm trên UI: bỏ qua hoàn toàn.
        /// <paramref name="cell"/> = null nếu bấm ngoài bàn.
        /// </summary>
        public InputResponse PressBoard(bool pointerOverUi, GridPos? cell)
        {
            if (pointerOverUi || !CanAct) return InputResponse.None;

            if (Mode == InputMode.ChoosingStasisTarget) return ChooseStasisTarget(cell);

            bool dashing = Mode == InputMode.ChoosingDashDirection;
            if (!cell.HasValue) return dashing ? Feedback(UiText.DashPickAdjacent) : InputResponse.None;

            GridPos hero = session.Current.HeroPos;
            if (cell.Value == hero) return Feedback(dashing ? UiText.DashPickAdjacent : UiText.ClickHero);

            if (!Directions.TryFromDelta(cell.Value - hero, out Direction direction))
                return Feedback(dashing ? UiText.DashPickAdjacent : UiText.NotAdjacent);

            if (!dashing) return Submit(HeroAction.Move(direction));

            // Ô được bấm chỉ xác định hướng. Dash sai: giữ chế độ chọn hướng, không mất lượt dùng.
            TurnResult result = session.Apply(HeroAction.Dash(direction));
            if (!result.Accepted) return Feedback(UiText.DashError(result.Error, result.FailedDashStep));

            Mode = InputMode.Normal;
            return new InputResponse(result, string.Empty);
        }

        public InputResponse PressWait() => CanWait ? Submit(HeroAction.Wait) : InputResponse.None;

        /// <summary>Bật/tắt chế độ chọn hướng Dash. Đang chọn mục tiêu Stasis thì chuyển sang Dash (hủy chọn Stasis).</summary>
        public InputResponse PressDash()
        {
            if (!CanAct || !LevelHasDash) return InputResponse.None;

            if (Mode == InputMode.ChoosingDashDirection)
            {
                Mode = InputMode.Normal;
                return Feedback(UiText.DashCancelled);
            }

            if (session.Current.DashUsesLeft <= 0) return Feedback(UiText.NoDashUsesLeft);

            Mode = InputMode.ChoosingDashDirection;
            return Feedback(UiText.DashPrompt);
        }

        /// <summary>
        /// Bật/tắt chế độ chọn mục tiêu Stasis. Chọn thẻ chưa tiêu hao gì.
        /// Đang chọn hướng Dash thì chuyển sang Stasis (hủy chọn hướng Dash chưa thực hiện).
        /// </summary>
        public InputResponse PressStasis()
        {
            if (!CanAct || !LevelHasStasis) return InputResponse.None;

            if (Mode == InputMode.ChoosingStasisTarget)
            {
                Mode = InputMode.Normal;
                return Feedback(UiText.StasisCancelled);
            }

            if (session.Current.StasisUsesLeft <= 0) return Feedback(UiText.NoStasisUsesLeft);
            if (session.Current.SupportUsedThisTurn) return Feedback(UiText.SupportAlreadyUsed);

            Mode = InputMode.ChoosingStasisTarget;
            return Feedback(UiText.StasisPrompt);
        }

        /// <summary>Escape: hủy chế độ chọn đang mở (hướng Dash hoặc mục tiêu Stasis). Không tốn lượt, không hoàn tác thẻ đã áp.</summary>
        public InputResponse Cancel()
        {
            switch (Mode)
            {
                case InputMode.ChoosingDashDirection:
                    Mode = InputMode.Normal;
                    return Feedback(UiText.DashCancelled);
                case InputMode.ChoosingStasisTarget:
                    Mode = InputMode.Normal;
                    return Feedback(UiText.StasisCancelled);
                default:
                    return InputResponse.None;
            }
        }

        /// <summary>Chơi lại màn hiện tại: trạng thái đầu màn, đủ tài nguyên, xóa khóa và chế độ chọn.</summary>
        public void Restart()
        {
            session.Restart();
            Mode = InputMode.Normal;
            Busy = false;
        }

        /// <summary>
        /// Trong chế độ chọn Stasis, bấm bàn cờ chỉ chọn mục tiêu, không di chuyển Hero.
        /// Mục tiêu hợp lệ: áp thẻ ngay, thoát chế độ chọn, Hero vẫn còn hành động chính.
        /// Mục tiêu sai: giữ chế độ chọn, không tiêu hao gì.
        /// </summary>
        InputResponse ChooseStasisTarget(GridPos? cell)
        {
            if (!cell.HasValue) return Feedback(UiText.StasisPrompt);

            // Chỉ khóa đúng X01 đang đứng ở ô được bấm; gửi ID ổn định của X đó vào Core.
            int index = session.Current.IndexOfEnemyAt(cell.Value);
            if (index < 0) return Feedback(UiText.StasisError(ActionError.NoEnemyAtTarget));

            SupportResult result = session.ApplyStasis(session.Current.Enemies[index].Id);
            if (!result.Accepted) return Feedback(UiText.StasisError(result.Error));

            Mode = InputMode.Normal;
            return Feedback(UiText.StasisApplied);
        }

        InputResponse Submit(HeroAction action)
        {
            TurnResult result = session.Apply(action);
            if (!result.Accepted) return Feedback(UiText.MoveError(result.Error));

            Mode = InputMode.Normal;
            return new InputResponse(result, string.Empty);
        }

        static InputResponse Feedback(string text) => new InputResponse(null, text);
    }
}
