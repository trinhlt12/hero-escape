using System;

namespace HeroEscape.Core
{
    /// <summary>
    /// Sở hữu trạng thái đang chơi. Chỉ đổi trạng thái qua <see cref="Apply"/> và <see cref="Restart"/>.
    /// <see cref="Revision"/> tăng sau mỗi thay đổi để lớp hiển thị bỏ qua callback của lượt cũ.
    /// </summary>
    public sealed class GameSession
    {
        readonly Simulation simulation;

        public LevelData Level { get; }
        public GameState InitialState { get; }
        public GameState Current { get; private set; }
        public int Revision { get; private set; }

        public GameSession(LevelData level, Simulation simulation)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            InitialState = GameState.CreateInitial(level);
            Current = InitialState;
        }

        /// <summary>Hành động không hợp lệ không đổi trạng thái và không tăng Revision.</summary>
        public TurnResult Apply(HeroAction action)
        {
            TurnResult result = simulation.Resolve(Current, action);
            if (result.Accepted)
            {
                Current = result.NextState;
                Revision++;
            }

            return result;
        }

        /// <summary>Dùng Stasis lên X01 đang đứng ở ô mục tiêu, trước hành động chính. Bị từ chối thì không đổi gì.</summary>
        public SupportResult ApplyStasis(GridPos targetCell) => Commit(simulation.ApplyStasis(Current, targetCell));

        /// <summary>Dùng Stasis lên X01 có ID ổn định <paramref name="enemyId"/>. Bị từ chối thì không đổi gì.</summary>
        public SupportResult ApplyStasis(string enemyId) => Commit(simulation.ApplyStasis(Current, enemyId));

        // ---- Chỉ đọc: highlight và preview. Dùng cùng Simulation/AI; trạng thái bất biến nên không thể làm đổi Current. ----

        /// <summary>Kết quả dự kiến nếu Hero làm <paramref name="action"/> từ trạng thái hiện tại. Không commit.</summary>
        public TurnResult Preview(HeroAction action) => simulation.Resolve(Current, action);

        /// <summary>Kết quả dự kiến nếu dùng Stasis lên X01 <paramref name="enemyId"/>. Không commit.</summary>
        public SupportResult PreviewStasis(string enemyId) => simulation.ApplyStasis(Current, enemyId);

        /// <summary>Các Move hoặc Dash hợp lệ từ trạng thái hiện tại, kèm ô đích.</summary>
        public System.Collections.Generic.List<(HeroAction action, GridPos destination)> LegalTargets(HeroActionKind kind) =>
            simulation.LegalTargets(Current, kind);

        /// <summary>Kiểm tra một hành động chính từ trạng thái hiện tại (cùng bộ kiểm tra với Apply).</summary>
        public bool TryGetDestination(HeroAction action, out GridPos destination, out ActionError error, out int failedDashStep) =>
            simulation.TryGetDestination(Current, action, out destination, out error, out failedDashStep);

        SupportResult Commit(SupportResult result)
        {
            if (result.Accepted)
            {
                Current = result.NextState;
                Revision++;
            }

            return result;
        }

        public void Restart()
        {
            Current = InitialState;
            Revision++;
        }
    }
}
