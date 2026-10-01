using System;
using HeroEscape.Core;

namespace HeroEscape.Unity
{
    public enum InputMode
    {
        Normal,
        ChoosingDashDirection,
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
        public bool CanAct => !Busy && session.Current.Outcome == GameOutcome.Playing;
        public bool CanWait => CanAct && Mode == InputMode.Normal;

        /// <summary>Nút Dash bấm được khi còn lượt dùng, hoặc đang chọn hướng (để hủy).</summary>
        public bool CanPressDash =>
            CanAct && LevelHasDash && (Mode == InputMode.ChoosingDashDirection || session.Current.DashUsesLeft > 0);

        /// <summary>
        /// Bấm lên vùng bàn cờ. <paramref name="pointerOverUi"/> = con trỏ đang nằm trên UI: bỏ qua hoàn toàn.
        /// <paramref name="cell"/> = null nếu bấm ngoài bàn.
        /// </summary>
        public InputResponse PressBoard(bool pointerOverUi, GridPos? cell)
        {
            if (pointerOverUi || !CanAct) return InputResponse.None;

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

        /// <summary>Bật/tắt chế độ chọn hướng Dash.</summary>
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

        /// <summary>Escape: hủy chọn hướng Dash nếu đang chọn. Không tốn lượt.</summary>
        public InputResponse Cancel()
        {
            if (Mode != InputMode.ChoosingDashDirection) return InputResponse.None;
            Mode = InputMode.Normal;
            return Feedback(UiText.DashCancelled);
        }

        /// <summary>Chơi lại màn hiện tại: trạng thái đầu màn, đủ lượt Dash, xóa chế độ chọn hướng.</summary>
        public void Restart()
        {
            session.Restart();
            Mode = InputMode.Normal;
            Busy = false;
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
