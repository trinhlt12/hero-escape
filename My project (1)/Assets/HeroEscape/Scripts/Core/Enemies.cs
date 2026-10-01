using System;

namespace HeroEscape.Core
{
    /// <summary>Khai báo một X01 trong level: ID ổn định, thứ tự hành động và ô xuất phát.</summary>
    public readonly struct EnemySpawn
    {
        public readonly string Id;

        /// <summary>Thứ tự hành động trong pha địch (tăng dần). Không đổi trong cả trận.</summary>
        public readonly int SpawnIndex;

        public readonly GridPos Cell;

        public EnemySpawn(string id, int spawnIndex, GridPos cell)
        {
            Id = id;
            SpawnIndex = spawnIndex;
            Cell = cell;
        }

        public override string ToString() => $"{Id}#{SpawnIndex}{Cell}";
    }

    /// <summary>
    /// Trạng thái một X01 trong trận. Bất biến; ID và SpawnIndex không đổi khi X di chuyển.
    /// </summary>
    public readonly struct EnemyState : IEquatable<EnemyState>
    {
        public readonly string Id;
        public readonly int SpawnIndex;
        public readonly GridPos Pos;

        /// <summary>Số pha địch X này còn phải bỏ qua do bị khóa. 0 = không bị khóa.</summary>
        public readonly int LockPhases;

        public EnemyState(string id, int spawnIndex, GridPos pos, int lockPhases)
        {
            Id = id;
            SpawnIndex = spawnIndex;
            Pos = pos;
            LockPhases = lockPhases;
        }

        public static EnemyState FromSpawn(EnemySpawn spawn) => new EnemyState(spawn.Id, spawn.SpawnIndex, spawn.Cell, 0);

        public bool Locked => LockPhases > 0;

        public EnemyState WithPos(GridPos pos) => new EnemyState(Id, SpawnIndex, pos, LockPhases);
        public EnemyState WithLockPhases(int phases) => new EnemyState(Id, SpawnIndex, Pos, phases);

        public bool Equals(EnemyState other) =>
            Id == other.Id && SpawnIndex == other.SpawnIndex && Pos == other.Pos && LockPhases == other.LockPhases;

        public override bool Equals(object obj) => obj is EnemyState other && Equals(other);

        public override int GetHashCode() =>
            ((((Id?.GetHashCode() ?? 0) * 397) ^ SpawnIndex) * 397 ^ Pos.GetHashCode()) * 397 ^ LockPhases;

        public override string ToString() => $"{Id}{Pos}{(Locked ? $"[khóa {LockPhases}]" : "")}";
    }
}
