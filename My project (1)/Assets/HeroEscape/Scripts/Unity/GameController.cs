using System;
using System.Collections;
using HeroEscape.Core;
using HeroEscape.Search;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HeroEscape.Unity
{
    /// <summary>Một màn trong chuỗi chơi thử: tên hiển thị và file level text.</summary>
    [Serializable]
    public sealed class LevelEntry
    {
        [SerializeField] string displayName;
        [SerializeField] TextAsset levelFile;

        public string DisplayName => displayName;
        public TextAsset LevelFile => levelFile;
    }

    /// <summary>
    /// Nối GameSession (Core) với hiển thị và input, và chuyển giữa các màn.
    /// Không tự quyết định luật: mọi hành động đi qua TurnInputController → GameSession,
    /// view chỉ vẽ lại theo trạng thái trả về.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameController : MonoBehaviour
    {
        [Tooltip("Các màn theo thứ tự. Màn đầu tiên được nạp khi bắt đầu.")]
        [SerializeField] LevelEntry[] levels;
        [SerializeField] BoardView boardView;
        [SerializeField] HudView hud;
        [Tooltip("Nhãn Frozen phía trên X01 khi X01 bị khóa.")]
        [SerializeField] EnemyStatusLabel enemyStatus;
        [Tooltip("Thời gian dừng giữa bước Hero và bước X01 để người chơi kịp nhìn. 0 = tức thời.")]
        [SerializeField, Min(0f)] float enemyStepDelay = 0.25f;

        GameSession session;
        TurnInputController input;
        int levelIndex = -1;
        Coroutine playback;

        // Hover hiện tại và khóa của lần cập nhật highlight/preview gần nhất (chỉ tính lại khi khóa đổi).
        GridPos? hoverCell;
        bool hoverWait;
        int levelEpoch;
        (int epoch, int revision, InputMode mode, bool canAct, GridPos? cell, bool wait)? interactionKey;

        void Start()
        {
            hud.WaitClicked += OnWaitClicked;
            hud.DashClicked += OnDashClicked;
            hud.StasisClicked += OnStasisClicked;
            hud.RestartClicked += OnRestartClicked;
            hud.NextLevelClicked += OnNextLevelClicked;
            hud.BackClicked += OnBackClicked;
            hud.WaitHoverChanged += OnWaitHoverChanged;

            if (levels == null || levels.Length == 0)
            {
                Fail("[GameController] No levels assigned. Rebuild the scene: Hero Escape > Milestone 01 > Dựng scene chơi thử.");
                return;
            }

            LoadLevel(0);
        }

        void OnDestroy()
        {
            hud.WaitClicked -= OnWaitClicked;
            hud.DashClicked -= OnDashClicked;
            hud.StasisClicked -= OnStasisClicked;
            hud.RestartClicked -= OnRestartClicked;
            hud.NextLevelClicked -= OnNextLevelClicked;
            hud.BackClicked -= OnBackClicked;
            hud.WaitHoverChanged -= OnWaitHoverChanged;
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
            if (input != null) Handle(input.PressBoard(pointerOverUi, cell));
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
                    if (enemyStatus != null) enemyStatus.SetFrozenPreview(-1);
                    hud.SetPreviewWarning(preview.WillLose ? UiText.WillBeCaught : preview.WillWin ? UiText.WillEscape : null);
                    break;
                case PreviewKind.StasisTarget:
                    // Chỉ cho thấy X sẽ bị khóa; không mô phỏng pha địch, không giả định Hero sẽ Wait.
                    boardView.HidePreview();
                    if (enemyStatus != null) enemyStatus.SetFrozenPreview(preview.StasisEnemyIndex);
                    hud.SetPreviewWarning(null);
                    break;
                default:
                    boardView.HidePreview();
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
            if (input != null) Handle(input.PressWait());
        }

        void OnDashClicked()
        {
            if (input != null) Handle(input.PressDash());
        }

        void OnStasisClicked()
        {
            if (input != null) Handle(input.PressStasis());
        }

        void OnRestartClicked()
        {
            if (input == null) return;
            StopPlayback();
            input.Restart();
            ShowState(UiText.Restarted);
        }

        void OnNextLevelClicked()
        {
            bool canAdvance = session != null && session.Current.Outcome == GameOutcome.Won && levelIndex + 1 < levels.Length;
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

            LevelEntry entry = levels[index];
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

            levelIndex = index;
            levelEpoch++;
            interactionKey = null;
            session = new GameSession(level, new Simulation(new BasicChasePolicy()));
            input = new TurnInputController(session);
            boardView.Build(level);
            bool severalEnemies = level.Enemies.Count > 1;
            if (enemyStatus != null) enemyStatus.Setup(level.Enemies.Count, showOrder: severalEnemies);
            hud.SetTitle(UiText.LevelTitle(entry.DisplayName));
            hud.SetHint(severalEnemies ? UiText.HintMultiEnemy
                : level.StasisMaxUses > 0 ? UiText.HintStasis
                : level.DashMaxUses > 0 ? UiText.HintDash
                : UiText.HintBasic);
            ShowState(string.Empty);
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

        /// <summary>Vẽ vị trí nhân vật và nhãn Frozen của từng X01 theo trạng thái đang trình bày.</summary>
        void ShowBoard(GameState displayed)
        {
            boardView.ShowActors(displayed);
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

        bool HasNextLevel => levelIndex + 1 < levels.Length;

        void RefreshButtons()
        {
            if (input == null) return;

            GameState state = session.Current;
            hud.SetWaitInteractable(input.CanWait);
            hud.SetDash(input.LevelHasDash, UiText.DashButton(state.DashUsesLeft, session.Level.DashMaxUses),
                input.CanPressDash, input.Mode == InputMode.ChoosingDashTarget);
            hud.SetStasis(input.LevelHasStasis, UiText.StasisButton(state.StasisUsesLeft, session.Level.StasisMaxUses),
                input.CanPressStasis, input.Mode == InputMode.ChoosingStasisTarget);
            hud.SetNavigation(
                showNext: state.Outcome == GameOutcome.Won && HasNextLevel,
                showBack: levelIndex > 0,
                backLabel: BackLabel);
        }

        string BackLabel => levelIndex > 0 ? UiText.BackToButton(levels[levelIndex - 1].DisplayName) : string.Empty;

        void Fail(string message)
        {
            Debug.LogError(message, this);
            hud.SetStatus(UiText.LevelError);
            hud.SetNotice(string.Empty);
            hud.SetWaitInteractable(false);
            hud.SetDash(false, string.Empty, false, false);
            hud.SetStasis(false, string.Empty, false, false);
            if (enemyStatus != null) enemyStatus.HideAllFrozen();
            ClearInteraction();
            hud.SetNavigation(false, levelIndex > 0, BackLabel);
        }
    }
}
