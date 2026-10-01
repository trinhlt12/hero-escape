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

        public void Restart()
        {
            Current = InitialState;
            Revision++;
        }
    }
}
