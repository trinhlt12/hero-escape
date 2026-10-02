using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroEscape.Core
{
    public enum GameOutcome
    {
        Playing,
        Won,
        Lost,
    }

    /// <summary>
    /// Trạng thái trận, bất biến với bên ngoài: mỗi lần chuyển trạng thái tạo đối tượng mới,
    /// nên giữ tham chiếu tới trạng thái đầu màn là đủ để Restart.
    /// </summary>
    public sealed class GameState : IEquatable<GameState>
    {
        static readonly GridPos[] NoBarriers = new GridPos[0];

        EnemyState[] enemies;
        GridPos[] barriers = NoBarriers;

        public LevelData Level { get; private set; }
        public GridPos HeroPos { get; private set; }
        public int Round { get; private set; }
        public GameOutcome Outcome { get; private set; }

        /// <summary>Các X01 theo SpawnIndex tăng dần (đúng thứ tự hành động). Thứ tự không đổi trong cả trận.</summary>
        public IReadOnlyList<EnemyState> Enemies => enemies;

        /// <summary>Số lần Dash còn lại trong lần chơi này.</summary>
        public int DashUsesLeft { get; private set; }

        /// <summary>Số lần dùng thẻ Stasis còn lại trong lần chơi này.</summary>
        public int StasisUsesLeft { get; private set; }

        /// <summary>Đã dùng thẻ phụ trợ trong lượt Hero hiện tại. Xóa khi bắt đầu lượt Hero mới.</summary>
        public bool SupportUsedThisTurn { get; private set; }

        /// <summary>Số lần dùng thẻ Barrier còn lại trong lần chơi này.</summary>
        public int BarrierUsesLeft { get; private set; }

        /// <summary>
        /// Ô đang có vật cản tạm (Barrier), sắp theo (Y, X). Lớp riêng, không đổi địa hình gốc của level. Mọi vật cản
        /// hết hiệu lực cùng lúc ở cuối pha địch kế tiếp, nên không cần lưu thời hạn riêng (luôn còn đúng một pha).
        /// </summary>
        public IReadOnlyList<GridPos> Barriers => barriers;

        /// <summary>Số pha địch mỗi vật cản tạm còn tồn tại (hiển thị nhãn).</summary>
        public const int BarrierPhases = 1;

        // Lối tắt cho màn chỉ có một X01 (X đầu tiên theo SpawnIndex).
        public GridPos EnemyPos => enemies[0].Pos;
        public int EnemyLockPhases => enemies[0].LockPhases;
        public bool EnemyLocked => enemies[0].Locked;

        GameState() { }

        public static GameState CreateInitial(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            return new GameState
            {
                Level = level,
                HeroPos = level.HeroStart,
                enemies = level.Enemies.Select(EnemyState.FromSpawn).ToArray(),
                Round = 1,
                Outcome = GameOutcome.Playing,
                DashUsesLeft = level.DashMaxUses,
                StasisUsesLeft = level.StasisMaxUses,
                BarrierUsesLeft = level.BarrierMaxUses,
                SupportUsedThisTurn = false,
            };
        }

        /// <summary>Vị trí trong danh sách <see cref="Enemies"/> của X có ID này, -1 nếu không có.</summary>
        public int IndexOfEnemy(string id)
        {
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i].Id == id) return i;
            return -1;
        }

        /// <summary>Vị trí trong danh sách <see cref="Enemies"/> của X đang đứng ở ô này, -1 nếu ô trống.</summary>
        public int IndexOfEnemyAt(GridPos cell)
        {
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i].Pos == cell) return i;
            return -1;
        }

        /// <summary>Vị trí các X khác X thứ <paramref name="exceptIndex"/> (-1 = lấy tất cả), dùng làm vật cản.</summary>
        public GridPos[] EnemyCells(int exceptIndex = -1)
        {
            var cells = new GridPos[exceptIndex >= 0 ? enemies.Length - 1 : enemies.Length];
            for (int i = 0, k = 0; i < enemies.Length; i++)
                if (i != exceptIndex) cells[k++] = enemies[i].Pos;
            return cells;
        }

        GameState Clone() => (GameState)MemberwiseClone();

        public GameState WithHero(GridPos pos) { GameState s = Clone(); s.HeroPos = pos; return s; }
        public GameState WithOutcome(GameOutcome outcome) { GameState s = Clone(); s.Outcome = outcome; return s; }
        public GameState WithDashUsed() { GameState s = Clone(); s.DashUsesLeft--; return s; }

        /// <summary>Thay trạng thái X thứ <paramref name="index"/>; ID và SpawnIndex phải giữ nguyên.</summary>
        public GameState WithEnemy(int index, EnemyState enemy)
        {
            if (enemy.Id != enemies[index].Id || enemy.SpawnIndex != enemies[index].SpawnIndex)
                throw new InvalidOperationException($"Không được đổi ID/SpawnIndex của X01 '{enemies[index].Id}'.");
            GameState s = Clone();
            s.enemies = (EnemyState[])enemies.Clone();
            s.enemies[index] = enemy;
            return s;
        }

        /// <summary>Áp Stasis lên X thứ <paramref name="index"/>: trừ một lần dùng, đánh dấu đã dùng phụ trợ, khóa riêng X đó.</summary>
        public GameState WithStasisApplied(int index, int lockPhases)
        {
            GameState s = WithEnemy(index, enemies[index].WithLockPhases(lockPhases));
            s.StasisUsesLeft--;
            s.SupportUsedThisTurn = true;
            return s;
        }

        public bool HasBarrierAt(GridPos cell) => GridRules.Contains(barriers, cell);

        /// <summary>Đặt vật cản tạm tại <paramref name="cell"/>: trừ một lần dùng Barrier, đánh dấu đã dùng phụ trợ.</summary>
        public GameState WithBarrierPlaced(GridPos cell)
        {
            GameState s = Clone();
            s.barriers = barriers.Append(cell).OrderBy(p => p.Y).ThenBy(p => p.X).ToArray();
            s.BarrierUsesLeft--;
            s.SupportUsedThisTurn = true;
            return s;
        }

        /// <summary>Xóa mọi vật cản tạm (cuối pha địch). Không đụng tới địa hình gốc.</summary>
        public GameState WithBarriersCleared()
        {
            if (barriers.Length == 0) return this;
            GameState s = Clone();
            s.barriers = NoBarriers;
            return s;
        }

        /// <summary>Kết thúc vòng: sang lượt Hero mới, xóa cờ đã dùng phụ trợ.</summary>
        public GameState WithNextRound() { GameState s = Clone(); s.Round++; s.SupportUsedThisTurn = false; return s; }

        public bool Equals(GameState other) =>
            other != null && ReferenceEquals(Level, other.Level) && HeroPos == other.HeroPos &&
            Round == other.Round && Outcome == other.Outcome &&
            DashUsesLeft == other.DashUsesLeft && StasisUsesLeft == other.StasisUsesLeft &&
            SupportUsedThisTurn == other.SupportUsedThisTurn && BarrierUsesLeft == other.BarrierUsesLeft &&
            enemies.SequenceEqual(other.enemies) && barriers.SequenceEqual(other.barriers);

        public override bool Equals(object obj) => Equals(obj as GameState);

        public override int GetHashCode()
        {
            int hash = HeroPos.GetHashCode();
            hash = hash * 397 ^ Round;
            hash = hash * 397 ^ (int)Outcome;
            hash = hash * 397 ^ DashUsesLeft;
            hash = hash * 397 ^ StasisUsesLeft;
            hash = hash * 397 ^ (SupportUsedThisTurn ? 1 : 0);
            hash = hash * 397 ^ BarrierUsesLeft;
            foreach (EnemyState e in enemies) hash = hash * 397 ^ e.GetHashCode();
            foreach (GridPos b in barriers) hash = hash * 397 ^ b.GetHashCode();
            return hash;
        }

        public override string ToString() =>
            $"Vòng {Round} Hero{HeroPos} {string.Join(" ", enemies)} " +
            $"Dash {DashUsesLeft} Stasis {StasisUsesLeft} Barrier {BarrierUsesLeft}" +
            $"{(barriers.Length > 0 ? " vật cản " + string.Join(",", barriers) : "")}{(SupportUsedThisTurn ? " (đã dùng phụ trợ)" : "")} {Outcome}";
    }
}
