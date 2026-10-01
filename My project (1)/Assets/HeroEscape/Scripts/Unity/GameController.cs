using System.Collections;
using HeroEscape.Core;
using HeroEscape.Search;
using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>
    /// Nối GameSession (Core) với hiển thị và input. Không tự quyết định luật:
    /// mọi hành động đi qua GameSession, view chỉ vẽ lại theo trạng thái trả về.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameController : MonoBehaviour
    {
        [Tooltip("File level dạng text (Assets/HeroEscape/Data/Levels).")]
        [SerializeField] TextAsset levelFile;
        [SerializeField] BoardView boardView;
        [SerializeField] HudView hud;
        [Tooltip("Thời gian dừng giữa bước Hero và bước X01 để người chơi kịp nhìn. 0 = tức thời.")]
        [SerializeField, Min(0f)] float enemyStepDelay = 0.25f;

        GameSession session;
        Coroutine playback;
        bool resolving;

        bool CanAct => session != null && !resolving && session.Current.Outcome == GameOutcome.Playing;

        void Start()
        {
            hud.WaitClicked += OnWaitClicked;
            hud.RestartClicked += OnRestartClicked;

            if (levelFile == null)
            {
                Fail("[GameController] Chưa gán file level.");
                return;
            }

            LevelData level;
            try
            {
                level = LevelParser.Parse(levelFile.text);
            }
            catch (LevelFormatException e)
            {
                Fail($"[GameController] Level '{levelFile.name}' không hợp lệ: {e.Message}");
                return;
            }

            session = new GameSession(level, new Simulation(new BasicChasePolicy()));
            boardView.Build(level);
            ShowState(session.Current, string.Empty);
        }

        void OnDestroy()
        {
            hud.WaitClicked -= OnWaitClicked;
            hud.RestartClicked -= OnRestartClicked;
        }

        /// <summary>Gọi từ BoardInput khi người chơi bấm vào một ô trong bàn.</summary>
        public void OnCellClicked(GridPos cell)
        {
            if (!CanAct) return;

            GridPos hero = session.Current.HeroPos;
            if (cell == hero)
            {
                // Bấm vào ô Hero không được tính là Chờ.
                hud.SetNotice(UiText.ClickHero);
                return;
            }

            if (!Directions.TryFromDelta(cell - hero, out Direction direction))
            {
                hud.SetNotice(UiText.NotAdjacent);
                return;
            }

            Submit(HeroAction.Move(direction));
        }

        void OnWaitClicked()
        {
            if (CanAct) Submit(HeroAction.Wait);
        }

        void OnRestartClicked()
        {
            if (session == null) return;

            // Hủy phần trình bày của lượt cũ; Revision mới cũng chặn mọi callback còn sót.
            if (playback != null) StopCoroutine(playback);
            playback = null;
            resolving = false;

            session.Restart();
            ShowState(session.Current, UiText.Restarted);
        }

        void Submit(HeroAction action)
        {
            TurnResult result = session.Apply(action);
            if (!result.Accepted)
            {
                // Hành động không hợp lệ: không đổi trạng thái, không mất lượt.
                hud.SetNotice(UiText.ForError(result.Error));
                return;
            }

            if (result.EnemyPhaseRan && enemyStepDelay > 0f)
                playback = StartCoroutine(PlayEnemyStep(result, session.Revision));
            else
                FinishTurn(result);
        }

        /// <summary>Hiện bước Hero trước, dừng ngắn rồi mới hiện bước X01. Chặn input trong lúc này.</summary>
        IEnumerator PlayEnemyStep(TurnResult result, int revision)
        {
            resolving = true;
            hud.SetWaitInteractable(false);
            hud.SetNotice(string.Empty);
            boardView.ShowActors(result.AfterHeroState);
            hud.SetStatus(UiText.EnemyTurn);

            yield return new WaitForSeconds(enemyStepDelay);
            if (revision != session.Revision) yield break; // đã Restart trong lúc chờ

            resolving = false;
            playback = null;
            FinishTurn(result);
        }

        void FinishTurn(TurnResult result)
        {
            bool enemyStayed = false;
            foreach (RuleEvent e in result.Events)
                enemyStayed |= e.Kind == RuleEventKind.EnemyStayed;

            ShowState(result.NextState, enemyStayed ? UiText.EnemyStayed : string.Empty);
        }

        void ShowState(GameState state, string notice)
        {
            boardView.ShowActors(state);

            switch (state.Outcome)
            {
                case GameOutcome.Won:
                    hud.SetStatus(UiText.Won);
                    hud.SetNotice(UiText.PressRestart);
                    break;
                case GameOutcome.Lost:
                    hud.SetStatus(UiText.Lost);
                    hud.SetNotice(UiText.PressRestart);
                    break;
                default:
                    hud.SetStatus(UiText.PlayerTurn(state.Round));
                    hud.SetNotice(notice);
                    break;
            }

            hud.SetWaitInteractable(CanAct);
        }

        void Fail(string message)
        {
            Debug.LogError(message, this);
            hud.SetStatus(UiText.LevelError);
            hud.SetWaitInteractable(false);
        }
    }
}
