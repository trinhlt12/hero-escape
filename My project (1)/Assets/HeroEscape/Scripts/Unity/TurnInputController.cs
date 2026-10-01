using System;
using System.Collections.Generic;
using HeroEscape.Core;

namespace HeroEscape.Unity
{
    /// <summary>Chế độ chọn của lượt Hero. <see cref="Normal"/> là chế độ Move mặc định.</summary>
    public enum InputMode
    {
        Normal,
        ChoosingDashTarget,
        ChoosingStasisTarget,
    }

    /// <summary>Kết quả một thao tác input: lượt đã gửi (nếu có) và phản hồi cho người chơi.</summary>
    public readonly struct InputResponse
    {
        public static readonly InputResponse None = new InputResponse(null, null);

        /// <summary>Lượt được Core chấp nhận; null nếu không có hành động chính nào được thực hiện.</summary>
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

    public enum PreviewKind
    {
        None,

        /// <summary>Kết quả dự kiến của một hành động chính (Move/Dash/Wait) và toàn bộ pha địch.</summary>
        MainAction,

        /// <summary>X01 sẽ bị khóa nếu dùng Stasis; không mô phỏng pha địch.</summary>
        StasisTarget,
    }

    /// <summary>Kết quả dự kiến khi hover. Chỉ để hiển thị; không bao giờ được áp vào trạng thái thật.</summary>
    public sealed class TurnPreview
    {
        public static readonly TurnPreview None = new TurnPreview(PreviewKind.None, null, -1);

        public PreviewKind Kind { get; }

        /// <summary>Với <see cref="PreviewKind.MainAction"/>: kết quả mô phỏng trên bản sao bất biến.</summary>
        public TurnResult Turn { get; }

        /// <summary>Với <see cref="PreviewKind.StasisTarget"/>: chỉ số X01 (theo SpawnIndex) sẽ bị khóa.</summary>
        public int StasisEnemyIndex { get; }

        public TurnPreview(PreviewKind kind, TurnResult turn, int stasisEnemyIndex)
        {
            Kind = kind;
            Turn = turn;
            StasisEnemyIndex = stasisEnemyIndex;
        }

        public bool WillLose => Kind == PreviewKind.MainAction && Turn.NextState.Outcome == GameOutcome.Lost;
        public bool WillWin => Kind == PreviewKind.MainAction && Turn.NextState.Outcome == GameOutcome.Won;
    }

    /// <summary>
    /// Trạng thái giao diện của lượt Hero và định tuyến input sang GameSession. Không dùng UnityEngine.
    /// - Highlight: các lựa chọn hợp lệ của chế độ hiện tại, tính bằng cùng bộ kiểm tra với thực thi.
    /// - Hover: dự đoán bằng chính Simulation/AI trên trạng thái bất biến; không đổi gì trong trạng thái thật.
    /// - Click: kiểm tra lại với trạng thái hiện tại rồi thực hiện ngay; không có Confirm, Cancel hay Undo.
    /// </summary>
    public sealed class TurnInputController
    {
        static readonly IReadOnlyList<GridPos> NoCells = Array.Empty<GridPos>();

        readonly GameSession session;

        // Cache theo (revision, chế độ, khả năng hành động): không tính lại mỗi frame khi không có gì đổi.
        (int revision, InputMode mode, bool canAct) highlightKey = (-1, InputMode.Normal, false);
        IReadOnlyList<GridPos> highlightCells = NoCells;
        (int revision, InputMode mode, bool canAct, GridPos? cell, bool wait) previewKey = (-1, InputMode.Normal, false, null, false);
        TurnPreview previewResult = TurnPreview.None;

        public TurnInputController(GameSession session)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public InputMode Mode { get; private set; } = InputMode.Normal;

        /// <summary>Lớp hiển thị đang trình bày lượt (pha địch); mọi input gameplay bị chặn, không highlight.</summary>
        public bool Busy { get; set; }

        /// <summary>Số lần đã thực sự mô phỏng preview (không tính lần lấy từ cache). Dùng để kiểm tra cache.</summary>
        public int PreviewEvaluations { get; private set; }

        public GameSession Session => session;
        public bool LevelHasDash => session.Level.DashMaxUses > 0;
        public bool LevelHasStasis => session.Level.StasisMaxUses > 0;
        public bool CanAct => !Busy && session.Current.Outcome == GameOutcome.Playing;

        /// <summary>Wait chỉ dùng được ở chế độ Move.</summary>
        public bool CanWait => CanAct && Mode == InputMode.Normal;

        /// <summary>Nút Dash bấm được khi còn lượt dùng, hoặc đang ở chế độ Dash (để trở về Move).</summary>
        public bool CanPressDash =>
            CanAct && LevelHasDash && (Mode == InputMode.ChoosingDashTarget || session.Current.DashUsesLeft > 0);

        /// <summary>Nút Stasis bấm được khi còn thẻ và chưa dùng phụ trợ lượt này, hoặc đang ở chế độ Stasis (để trở về Move).</summary>
        public bool CanPressStasis =>
            CanAct && LevelHasStasis && (Mode == InputMode.ChoosingStasisTarget ||
                                         (session.Current.StasisUsesLeft > 0 && !session.Current.SupportUsedThisTurn));

        // ---------------- Highlight ----------------

        /// <summary>
        /// Các ô hợp lệ của chế độ hiện tại: ô Move, ô đích Dash, hoặc ô của X01 là mục tiêu Stasis hợp lệ.
        /// Rỗng trong pha địch và sau khi trận kết thúc.
        /// </summary>
        public IReadOnlyList<GridPos> HighlightedCells
        {
            get
            {
                var key = (session.Revision, Mode, CanAct);
                if (key != highlightKey)
                {
                    highlightKey = key;
                    highlightCells = CanAct ? ComputeHighlights() : NoCells;
                }

                return highlightCells;
            }
        }

        List<GridPos> ComputeHighlights()
        {
            var cells = new List<GridPos>();
            switch (Mode)
            {
                case InputMode.Normal:
                    foreach ((HeroAction _, GridPos destination) in session.LegalTargets(HeroActionKind.Move)) cells.Add(destination);
                    break;
                case InputMode.ChoosingDashTarget:
                    foreach ((HeroAction _, GridPos destination) in session.LegalTargets(HeroActionKind.Dash)) cells.Add(destination);
                    break;
                case InputMode.ChoosingStasisTarget:
                    foreach (EnemyState enemy in session.Current.Enemies)
                        if (session.PreviewStasis(enemy.Id).Accepted) cells.Add(enemy.Pos);
                    break;
            }

            return cells;
        }

        // ---------------- Hover preview ----------------

        /// <summary>
        /// Dự đoán cho ô đang hover trên bàn (null = không hover ô nào). Chỉ có kết quả khi ô là lựa chọn hợp lệ
        /// của chế độ hiện tại. Có cache: cùng revision, chế độ và ô thì không mô phỏng lại.
        /// </summary>
        public TurnPreview PreviewBoardHover(GridPos? cell) => CachedPreview(cell, wait: false);

        /// <summary>Dự đoán khi hover nút Wait: Hero đứng yên rồi toàn bộ pha địch. Chỉ ở chế độ Move.</summary>
        public TurnPreview PreviewWaitHover() => CachedPreview(null, wait: true);

        TurnPreview CachedPreview(GridPos? cell, bool wait)
        {
            var key = (session.Revision, Mode, CanAct, cell, wait);
            if (key == previewKey) return previewResult;

            previewKey = key;
            previewResult = ComputePreview(cell, wait);
            return previewResult;
        }

        TurnPreview ComputePreview(GridPos? cell, bool wait)
        {
            if (!CanAct) return TurnPreview.None;

            if (wait)
            {
                if (!CanWait) return TurnPreview.None;
                PreviewEvaluations++;
                return new TurnPreview(PreviewKind.MainAction, session.Preview(HeroAction.Wait), -1);
            }

            if (!cell.HasValue) return TurnPreview.None;

            if (Mode == InputMode.ChoosingStasisTarget)
            {
                int index = session.Current.IndexOfEnemyAt(cell.Value);
                if (index < 0 || !session.PreviewStasis(session.Current.Enemies[index].Id).Accepted) return TurnPreview.None;
                PreviewEvaluations++;
                return new TurnPreview(PreviewKind.StasisTarget, null, index);
            }

            if (!TryFindMainAction(cell.Value, out HeroAction action)) return TurnPreview.None;
            PreviewEvaluations++;
            return new TurnPreview(PreviewKind.MainAction, session.Preview(action), -1);
        }

        /// <summary>Hành động Move/Dash (theo chế độ) có ô đích đúng bằng <paramref name="cell"/>, kiểm tra trên trạng thái hiện tại.</summary>
        bool TryFindMainAction(GridPos cell, out HeroAction action)
        {
            HeroActionKind kind = Mode == InputMode.ChoosingDashTarget ? HeroActionKind.Dash : HeroActionKind.Move;
            foreach ((HeroAction candidate, GridPos destination) in session.LegalTargets(kind))
            {
                if (destination == cell)
                {
                    action = candidate;
                    return true;
                }
            }

            action = default;
            return false;
        }

        // ---------------- Click ----------------

        /// <summary>
        /// Bấm lên vùng bàn cờ. <paramref name="pointerOverUi"/> = con trỏ đang nằm trên UI: bỏ qua hoàn toàn.
        /// <paramref name="cell"/> = null nếu bấm ngoài bàn. Lựa chọn hợp lệ được thực hiện ngay.
        /// </summary>
        public InputResponse PressBoard(bool pointerOverUi, GridPos? cell)
        {
            if (pointerOverUi || !CanAct) return InputResponse.None;

            switch (Mode)
            {
                case InputMode.ChoosingStasisTarget: return ChooseStasisTarget(cell);
                case InputMode.ChoosingDashTarget: return ChooseDashTarget(cell);
                default: return ChooseMoveTarget(cell);
            }
        }

        InputResponse ChooseMoveTarget(GridPos? cell)
        {
            if (!cell.HasValue) return InputResponse.None;

            GridPos hero = session.Current.HeroPos;
            if (cell.Value == hero) return Feedback(UiText.ClickHero);
            if (TryFindMainAction(cell.Value, out HeroAction move)) return Submit(move);

            // Không phải ô Move hợp lệ: giải thích lý do bằng cùng bộ kiểm tra của Core.
            if (!Directions.TryFromDelta(cell.Value - hero, out Direction direction)) return Feedback(UiText.NotAdjacent);
            session.TryGetDestination(HeroAction.Move(direction), out _, out ActionError error, out _);
            return Feedback(UiText.MoveError(error));
        }

        /// <summary>Chế độ Dash: bấm thẳng ô đích. Ô kề Hero không còn dùng để chọn hướng.</summary>
        InputResponse ChooseDashTarget(GridPos? cell)
        {
            if (!cell.HasValue || cell.Value == session.Current.HeroPos) return Feedback(UiText.DashPickTarget);
            if (TryFindMainAction(cell.Value, out HeroAction dash)) return Submit(dash);

            // Ô cách đúng 2 bước theo một hướng nhưng bị chặn: báo bước bị chặn. Các ô khác: nhắc chọn ô highlight.
            GridPos delta = cell.Value - session.Current.HeroPos;
            if (delta.X % 2 == 0 && delta.Y % 2 == 0 &&
                Directions.TryFromDelta(new GridPos(delta.X / 2, delta.Y / 2), out Direction direction))
            {
                session.TryGetDestination(HeroAction.Dash(direction), out _, out ActionError error, out int step);
                if (error != ActionError.None) return Feedback(UiText.DashError(error, step));
            }

            return Feedback(UiText.DashPickTarget);
        }

        /// <summary>
        /// Chế độ Stasis: bấm bàn cờ chỉ chọn mục tiêu, không di chuyển Hero. Mục tiêu hợp lệ: áp ngay lên đúng ID
        /// của X01 ở ô đó, trở về Move; Hero vẫn còn hành động chính. Mục tiêu sai: giữ chế độ, không tiêu hao gì.
        /// </summary>
        InputResponse ChooseStasisTarget(GridPos? cell)
        {
            if (!cell.HasValue) return Feedback(UiText.StasisPrompt);

            int index = session.Current.IndexOfEnemyAt(cell.Value);
            if (index < 0) return Feedback(UiText.StasisError(ActionError.NoEnemyAtTarget));

            SupportResult result = session.ApplyStasis(session.Current.Enemies[index].Id);
            if (!result.Accepted) return Feedback(UiText.StasisError(result.Error));

            Mode = InputMode.Normal;
            return Feedback(UiText.StasisApplied);
        }

        public InputResponse PressWait() => CanWait ? Submit(HeroAction.Wait) : InputResponse.None;

        /// <summary>Bật/tắt chế độ Dash. Chỉ đổi chế độ, không tiêu hao. Đang ở Stasis thì chuyển thẳng sang Dash.</summary>
        public InputResponse PressDash()
        {
            if (!CanAct || !LevelHasDash) return InputResponse.None;

            if (Mode == InputMode.ChoosingDashTarget)
            {
                Mode = InputMode.Normal;
                return Feedback(UiText.DashCancelled);
            }

            if (session.Current.DashUsesLeft <= 0) return Feedback(UiText.NoDashUsesLeft);

            Mode = InputMode.ChoosingDashTarget;
            return Feedback(UiText.DashPrompt);
        }

        /// <summary>Bật/tắt chế độ Stasis. Chỉ đổi chế độ, không tiêu hao thẻ. Đang ở Dash thì chuyển thẳng sang Stasis.</summary>
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

        /// <summary>Escape: trở về chế độ Move. Không tốn lượt, không hoàn tác thẻ đã áp.</summary>
        public InputResponse Cancel()
        {
            switch (Mode)
            {
                case InputMode.ChoosingDashTarget:
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

        InputResponse Submit(HeroAction action)
        {
            TurnResult result = session.Apply(action);
            if (!result.Accepted)
            {
                return Feedback(action.Kind == HeroActionKind.Dash
                    ? UiText.DashError(result.Error, result.FailedDashStep)
                    : UiText.MoveError(result.Error));
            }

            Mode = InputMode.Normal;
            return new InputResponse(result, string.Empty);
        }

        static InputResponse Feedback(string text) => new InputResponse(null, text);
    }
}
