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
        [Tooltip("Thời gian dừng giữa bước Hero và bước X01 để người chơi kịp nhìn. 0 = tức thời.")]
        [SerializeField, Min(0f)] float enemyStepDelay = 0.25f;

        GameSession session;
        TurnInputController input;
        int levelIndex = -1;
        Coroutine playback;

        void Start()
        {
            hud.WaitClicked += OnWaitClicked;
            hud.DashClicked += OnDashClicked;
            hud.RestartClicked += OnRestartClicked;
            hud.NextLevelClicked += OnNextLevelClicked;
            hud.BackClicked += OnBackClicked;

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
            hud.RestartClicked -= OnRestartClicked;
            hud.NextLevelClicked -= OnNextLevelClicked;
            hud.BackClicked -= OnBackClicked;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (input != null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                Handle(input.Cancel());
        }

        /// <summary>Gọi từ BoardInput mỗi lần bấm; cell = null nếu bấm ngoài bàn.</summary>
        public void OnBoardPressed(bool pointerOverUi, GridPos? cell)
        {
            if (input != null) Handle(input.PressBoard(pointerOverUi, cell));
        }

        void OnWaitClicked()
        {
            if (input != null) Handle(input.PressWait());
        }

        void OnDashClicked()
        {
            if (input != null) Handle(input.PressDash());
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

        void OnBackClicked()
        {
            if (levelIndex > 0) LoadLevel(0);
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
            session = new GameSession(level, new Simulation(new BasicChasePolicy()));
            input = new TurnInputController(session);
            boardView.Build(level);
            hud.SetTitle(UiText.LevelTitle(entry.DisplayName));
            hud.SetHint(level.DashMaxUses > 0 ? UiText.HintDash : UiText.HintBasic);
            ShowState(string.Empty);
        }

        void Handle(InputResponse response)
        {
            if (!response.Acted)
            {
                if (response.Feedback != null) hud.SetNotice(response.Feedback);
                RefreshButtons();
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
            boardView.ShowActors(turn.AfterHeroState);
            hud.SetStatus(UiText.EnemyTurn);
            hud.SetNotice(string.Empty);
            RefreshButtons();

            yield return new WaitForSeconds(enemyStepDelay);
            // Đã Restart hoặc đổi màn trong lúc chờ: bỏ qua phần còn lại của lượt cũ.
            if (owner != session || revision != session.Revision) yield break;

            input.Busy = false;
            playback = null;
            FinishTurn(turn);
        }

        void FinishTurn(TurnResult turn)
        {
            bool enemyStayed = false;
            foreach (RuleEvent e in turn.Events)
                enemyStayed |= e.Kind == RuleEventKind.EnemyStayed;

            ShowState(enemyStayed ? UiText.EnemyStayed : string.Empty);
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
            boardView.ShowActors(state);

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
        }

        bool HasNextLevel => levelIndex + 1 < levels.Length;

        void RefreshButtons()
        {
            if (input == null) return;

            GameState state = session.Current;
            hud.SetWaitInteractable(input.CanWait);
            hud.SetDash(input.LevelHasDash, UiText.DashButton(state.DashUsesLeft, session.Level.DashMaxUses),
                input.CanPressDash, input.Mode == InputMode.ChoosingDashDirection);
            hud.SetNavigation(
                showNext: state.Outcome == GameOutcome.Won && HasNextLevel,
                showBack: levelIndex > 0,
                backLabel: UiText.BackToButton(levels[0].DisplayName));
        }

        void Fail(string message)
        {
            Debug.LogError(message, this);
            hud.SetStatus(UiText.LevelError);
            hud.SetNotice(string.Empty);
            hud.SetWaitInteractable(false);
            hud.SetDash(false, string.Empty, false, false);
            hud.SetNavigation(false, levelIndex > 0, UiText.BackToButton(levels != null && levels.Length > 0 ? levels[0].DisplayName : "Level 01"));
        }
    }
}
