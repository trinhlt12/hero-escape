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
