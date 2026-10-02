using System;
using System.Collections;
using HeroEscape.Core;
using HeroEscape.Search;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Nối GameSession (Core) với hiển thị và input, và chuyển giữa các màn.
    /// Không tự quyết định luật: mọi hành động đi qua TurnInputController → GameSession,
    /// view chỉ vẽ lại theo trạng thái trả về.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameController : MonoBehaviour
    {
        [Tooltip("Catalog màn dùng chung với Level Select. Màn mở = ID chọn ở Level Select, hoặc màn mặc định của catalog.")]
        [SerializeField] LevelCatalog catalog;
        [SerializeField] BoardView boardView;
        [SerializeField] HudView hud;
        [Tooltip("Tay bài ở đáy màn hình: thẻ Hero (Dash) và thẻ phụ trợ (Stasis). Thay cho nút Dash/Stasis cũ.")]
        [SerializeField] HandView hand;
        [Tooltip("Nhãn Frozen phía trên X01 khi X01 bị khóa.")]
        [SerializeField] EnemyStatusLabel enemyStatus;
        [Tooltip("Thời gian dừng giữa bước Hero và bước X01 để người chơi kịp nhìn. 0 = tức thời.")]
        [SerializeField, Min(0f)] float enemyStepDelay = 0.25f;

        GameSession session;
        TurnInputController input;
        TacticalChasePolicy tactical; // null nếu màn dùng Basic
        int levelIndex = -1;
        Coroutine playback;
        bool leavingScene;
        bool resetHand; // lần vẽ tay bài kế tiếp đặt thẳng vào bố cục, hủy tween cũ (vào màn, Restart)

        // Hover hiện tại và khóa của lần cập nhật highlight/preview gần nhất (chỉ tính lại khi khóa đổi).
        GridPos? hoverCell;
        bool hoverWait;
        int levelEpoch;
        (int epoch, int revision, InputMode mode, bool canAct, GridPos? cell, bool wait)? interactionKey;

        void Start()
        {
            hud.WaitClicked += OnWaitClicked;
            hud.RestartClicked += OnRestartClicked;
            hud.NextLevelClicked += OnNextLevelClicked;
            hud.BackClicked += OnBackClicked;
            hud.WaitHoverChanged += OnWaitHoverChanged;
            hud.LevelSelectClicked += OnLevelSelectClicked;
            if (hand != null) hand.CardPressed += OnCardPressed;
            else Debug.LogError("[GameController] No HandView assigned: Dash/Stasis cards are unavailable. Rebuild the scene: Hero Escape > Milestone 01 > Dựng scene chơi thử.", this);

            // Đọc (và xóa) ID màn được chọn ở Level Select; mở thẳng scene này thì dùng màn mặc định.
            string requestedId = LevelSelection.TakePending();
            if (catalog == null || catalog.Levels.Count == 0)
            {
                Fail("[GameController] No level catalog assigned. Rebuild the scene: Hero Escape > Milestone 01 > Dựng scene chơi thử.");
                return;
            }

            int index = catalog.IndexOf(requestedId);
            if (index < 0)
            {
                if (requestedId != null)
                    Debug.LogWarning($"[GameController] Unknown level id '{requestedId}'; opening the default level.", this);
                index = catalog.DefaultIndex;
            }

            LoadLevel(index);
        }

        /// <summary>
        /// Về màn Level Select ngay (không xác nhận). Không cho trong lúc đang xử lý pha địch; chặn bấm lặp.
        /// Scene gameplay bị hủy toàn bộ nên bàn cờ, ghost, highlight và callback của phiên chơi đều được dọn.
        /// </summary>
        void OnLevelSelectClicked()
        {
            if (leavingScene || (input != null && input.Busy)) return;
            leavingScene = true;
            StopPlayback();
            ClearInteraction();
            UnityEngine.SceneManagement.SceneManager.LoadScene(catalog.LevelSelectScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            hud.WaitClicked -= OnWaitClicked;
            hud.RestartClicked -= OnRestartClicked;
            hud.NextLevelClicked -= OnNextLevelClicked;
            hud.BackClicked -= OnBackClicked;
            hud.WaitHoverChanged -= OnWaitHoverChanged;
            hud.LevelSelectClicked -= OnLevelSelectClicked;
            if (hand != null) hand.CardPressed -= OnCardPressed;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (input != null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Handle(input.Cancel());
        }

        /// <summary>Gọi từ BoardInput mỗi lần bấm; cell = null nếu bấm ngoài bàn. Lựa chọn hợp lệ được thực hiện ngay.</summary>
        public void OnBoardPressed(bool pointerOverUi, GridPos? cell)
        {
            if (input != null) HandleWithDiagnostics(() => input.PressBoard(pointerOverUi, cell));
        }

        /// <summary>Gọi từ BoardInput mỗi frame: ô đang hover trên bàn (null nếu không có hoặc con trỏ trên UI).</summary>
        public void OnBoardHover(GridPos? cell)
        {
            hoverCell = cell;
            RefreshInteraction();
        }

        void OnWaitHoverChanged(bool hovered)
        {
            hoverWait = hovered;
            RefreshInteraction();
        }

        /// <summary>
        /// Cập nhật highlight lựa chọn hợp lệ và preview của ô/nút đang hover. Chỉ tính lại khi màn, revision trạng thái,
        /// chế độ, khả năng hành động hoặc mục tiêu hover thay đổi; nhờ đó chuột đứng yên vẫn được cập nhật khi trạng
        /// thái đổi, nhưng không mô phỏng lại mỗi frame. Preview không bao giờ được áp vào trạng thái thật.
        /// </summary>
        void RefreshInteraction()
        {
            if (input == null)
            {
                interactionKey = null;
                ClearInteraction();
                return;
            }

            var key = (levelEpoch, session.Revision, input.Mode, input.CanAct, hoverCell, hoverWait);
            if (interactionKey.HasValue && interactionKey.Value == key) return;
            interactionKey = key;

            boardView.SetHighlights(input.HighlightedCells);
            ShowPreview(hoverWait ? input.PreviewWaitHover() : input.PreviewBoardHover(hoverCell));
        }

        void ShowPreview(TurnPreview preview)
        {
            switch (preview.Kind)
            {
                case PreviewKind.MainAction:
                    boardView.ShowPreview(session.Current, preview.Turn.NextState);
                    boardView.ShowBarrierPreview(null);
                    if (enemyStatus != null) enemyStatus.SetFrozenPreview(-1);
                    hud.SetPreviewWarning(preview.WillLose ? UiText.WillBeCaught : preview.WillWin ? UiText.WillEscape : null);
                    break;
                case PreviewKind.StasisTarget:
                    // Chỉ cho thấy X sẽ bị khóa; không mô phỏng pha địch, không giả định Hero sẽ Wait.
                    boardView.HidePreview();
                    boardView.ShowBarrierPreview(null);
                    if (enemyStatus != null) enemyStatus.SetFrozenPreview(preview.StasisEnemyIndex);
                    hud.SetPreviewWarning(null);
                    break;
                case PreviewKind.BarrierTarget:
                    // Chỉ cho thấy vị trí vật cản dự kiến; không đoán hành động chính, không mô phỏng pha địch.
                    boardView.HidePreview();
                    boardView.ShowBarrierPreview(preview.BarrierCell);
                    if (enemyStatus != null) enemyStatus.SetFrozenPreview(-1);
                    hud.SetPreviewWarning(null);
                    break;
                default:
                    boardView.HidePreview();
                    boardView.ShowBarrierPreview(null);
                    if (enemyStatus != null) enemyStatus.SetFrozenPreview(-1);
                    hud.SetPreviewWarning(null);
                    break;
            }
        }

        void ClearInteraction()
        {
            boardView.SetHighlights(System.Array.Empty<GridPos>());
            ShowPreview(TurnPreview.None);
        }

        void OnWaitClicked()
        {
            if (input != null) HandleWithDiagnostics(input.PressWait);
        }

        /// <summary>
        /// Chạm thẻ: chỉ bật/đổi/tắt chế độ chọn mục tiêu qua TurnInputController (PressDash/PressStasis).
        /// Không tiêu hao, không tăng lượt, không chạy pha địch; bị chặn trong pha địch và sau thắng/thua.
        /// </summary>
        void OnCardPressed(CardKind kind)
        {
            if (input != null && !leavingScene) Handle(CardHand.Press(input, kind));
        }

        void OnRestartClicked()
        {
            if (input == null) return;
            StopPlayback();
            input.Restart();
            resetHand = true;
            boardView.ShowBarriers(Array.Empty<GridPos>(), animate: false); // không để vật cản cũ chạy hiệu ứng sang phiên mới
            ShowState(UiText.Restarted);
        }

        void OnNextLevelClicked()
        {
            bool canAdvance = session != null && session.Current.Outcome == GameOutcome.Won && levelIndex + 1 < catalog.Levels.Count;
            if (canAdvance) LoadLevel(levelIndex + 1);
        }

        /// <summary>Về màn liền trước (Level 02 → Level 01, Level 03 → Level 02).</summary>
        void OnBackClicked()
        {
            if (levelIndex > 0) LoadLevel(levelIndex - 1);
        }

        /// <summary>Vào màn luôn tạo GameSession và trạng thái input mới; hủy mọi trình bày của màn cũ.</summary>
        void LoadLevel(int index)
        {
            StopPlayback();
            session = null;
            input = null;
            tactical = null;

            LevelEntry entry = catalog.Levels[index];
            if (entry == null || entry.LevelFile == null)
            {
                Fail($"[GameController] Level {index} has no level file.");
                return;
            }

            LevelData level;
            try
            {
                level = LevelParser.Parse(entry.LevelFile.text);
            }
            catch (LevelFormatException e)
            {
                Fail($"[GameController] Level '{entry.LevelFile.name}' is invalid: {e.Message}");
                return;
            }

            if (level.Id != entry.Id)
            {
                Fail($"[GameController] Catalog entry '{entry.Id}' points to level file '{entry.LevelFile.name}' with id '{level.Id}'.");
                return;
            }

            levelIndex = index;
            levelEpoch++;
            interactionKey = null;
            // AI theo profile cấu hình của level; gameplay và preview dùng chung Simulation (cùng ngân sách, fallback).
            IEnemyPolicy policy = EnemyPolicies.ForProfile(level.AiProfile);
            tactical = policy as TacticalChasePolicy;
            session = new GameSession(level, new Simulation(policy));
            input = new TurnInputController(session);
            resetHand = true;
            hud.SetHandSpace(CardHand.LevelHasCards(level));
            boardView.Build(level);
            bool severalEnemies = level.Enemies.Count > 1;
            if (enemyStatus != null) enemyStatus.Setup(level.Enemies.Count, showOrder: severalEnemies);
            hud.SetTitle(level.AiProfile == AiProfile.Tactical ? UiText.LevelTitleTactical(entry.DisplayName) : UiText.LevelTitle(entry.DisplayName));
            hud.SetHint(level.AiProfile == AiProfile.Tactical ? UiText.HintTactical
                : severalEnemies ? UiText.HintMultiEnemy
                : level.BarrierMaxUses > 0 ? UiText.HintBarrier
                : level.StasisMaxUses > 0 ? UiText.HintStasis
                : level.DashMaxUses > 0 ? UiText.HintDash
                : UiText.HintBasic);
            ShowState(string.Empty);
        }

        /// <summary>
        /// Thực hiện thao tác người chơi; nếu Tactical phải fallback về Basic trong chính nước đi thật này thì ghi một
        /// cảnh báo cho developer. Preview khi hover không đi qua đây nên không spam Console.
        /// </summary>
        void HandleWithDiagnostics(Func<InputResponse> act)
        {
            int fallbacksBefore = tactical?.FallbackCount ?? 0;
            InputResponse response = act();
            if (tactical != null && response.Acted && tactical.FallbackCount > fallbacksBefore)
            {
                Debug.LogWarning($"[Tactical] Hết ngân sách {tactical.TransitionBudget} chuyển trạng thái, X01 dùng Basic AI " +
                                 $"ở vòng {response.Turn.AfterHeroState.Round}.", this);
            }

            Handle(response);
        }

        void Handle(InputResponse response)
        {
            if (!response.Acted)
            {
                // Không có hành động chính, nhưng thẻ phụ trợ có thể đã đổi trạng thái (Stasis): vẽ lại bàn và nút.
                if (response.Feedback != null) hud.SetNotice(response.Feedback);
                ShowBoard(session.Current);
                RefreshButtons();
                RefreshInteraction();
                return;
            }

            TurnResult turn = response.Turn;
            if (turn.EnemyPhaseRan && enemyStepDelay > 0f)
                playback = StartCoroutine(PlayEnemyStep(turn, session, session.Revision));
            else
                FinishTurn(turn);
        }

        /// <summary>Hiện bước Hero trước, dừng ngắn rồi mới hiện bước X01. Chặn input trong lúc này.</summary>
        IEnumerator PlayEnemyStep(TurnResult turn, GameSession owner, int revision)
        {
            input.Busy = true;
            ShowBoard(turn.AfterHeroState);
            hud.SetStatus(UiText.EnemyTurn);
            hud.SetNotice(AnyLocked(turn.AfterHeroState) ? UiText.EnemySkipped : string.Empty);
            RefreshButtons();
            RefreshInteraction(); // pha địch: xóa highlight và preview, chặn hành động Hero

            yield return new WaitForSeconds(enemyStepDelay);
            // Đã Restart hoặc đổi màn trong lúc chờ: bỏ qua phần còn lại của lượt cũ.
            if (owner != session || revision != session.Revision) yield break;

            input.Busy = false;
            playback = null;
            FinishTurn(turn);
        }

        void FinishTurn(TurnResult turn)
        {
            string notice = string.Empty;
            foreach (RuleEvent e in turn.Events)
            {
                if (e.Kind == RuleEventKind.EnemyStayed) notice = UiText.EnemyStayed;
                else if (e.Kind == RuleEventKind.EnemySkipped) notice = UiText.EnemySkipped;
            }

            ShowState(notice);
        }

        /// <summary>Vẽ vị trí nhân vật, vật cản tạm (kèm nhãn số pha còn lại) và nhãn Frozen của từng X01 theo trạng thái đang trình bày.</summary>
        void ShowBoard(GameState displayed)
        {
            boardView.ShowActors(displayed);
            boardView.ShowBarriers(displayed.Barriers, animate: true);
            if (enemyStatus != null) enemyStatus.SetBarriers(displayed.Barriers, GameState.BarrierPhases);
            if (enemyStatus == null) return;
            for (int i = 0; i < displayed.Enemies.Count; i++)
                enemyStatus.SetFrozen(i, displayed.Enemies[i].Locked && displayed.Outcome == GameOutcome.Playing);
        }

        static bool AnyLocked(GameState state)
        {
            foreach (EnemyState enemy in state.Enemies)
                if (enemy.Locked) return true;
            return false;
        }

        void StopPlayback()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
            if (input != null) input.Busy = false;
        }

        void ShowState(string notice)
        {
            GameState state = session.Current;
            ShowBoard(state);

            switch (state.Outcome)
            {
                case GameOutcome.Won:
                    hud.SetStatus(UiText.Won);
                    hud.SetNotice(HasNextLevel ? UiText.WonNext : UiText.WonLast);
                    break;
                case GameOutcome.Lost:
                    hud.SetStatus(UiText.Lost);
                    hud.SetNotice(UiText.PressRestart);
                    break;
                default:
                    hud.SetStatus(UiText.YourTurn(state.Round));
                    hud.SetNotice(notice);
                    break;
            }

            RefreshButtons();
            RefreshInteraction();
        }

        bool HasNextLevel => levelIndex + 1 < catalog.Levels.Count;

        void RefreshButtons()
        {
            if (input == null) return;

            GameState state = session.Current;
            hud.SetWaitInteractable(input.CanWait);
            // Tay bài suy ra từ GameState + chế độ chọn hiện tại; thẻ hết lượt dùng rời tay ngay khi Core xác nhận.
            if (hand != null) hand.Show(CardHand.From(input), resetHand);
            resetHand = false;
            hud.SetNavigation(
                showNext: state.Outcome == GameOutcome.Won && HasNextLevel,
                showBack: levelIndex > 0,
                backLabel: BackLabel);
            // Về Level Select: dùng được trong lượt Hero và sau thắng/thua; khóa khi đang xử lý pha địch.
            hud.SetLevelSelectInteractable(!leavingScene && !input.Busy);
        }

        string BackLabel => levelIndex > 0 ? UiText.BackToButton(catalog.Levels[levelIndex - 1].DisplayName) : string.Empty;

        void Fail(string message)
        {
            Debug.LogError(message, this);
            hud.SetStatus(UiText.LevelError);
            hud.SetNotice(string.Empty);
            hud.SetWaitInteractable(false);
            if (hand != null) hand.Show(Array.Empty<CardState>(), reset: true);
            if (enemyStatus != null) enemyStatus.HideAllFrozen();
            ClearInteraction();
            hud.SetNavigation(false, levelIndex > 0, BackLabel);
            hud.SetLevelSelectInteractable(!leavingScene);
        }
    }
}
