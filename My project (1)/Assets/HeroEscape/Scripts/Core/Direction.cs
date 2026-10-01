using System.Collections.Generic;

namespace HeroEscape.Core
{
    /// <summary>Tám hướng. Thứ tự khai báo chính là thứ tự phá hòa cố định của luật.</summary>
    public enum Direction
    {
        N = 0,
        NE = 1,
        E = 2,
        SE = 3,
        S = 4,
        SW = 5,
        W = 6,
        NW = 7,
    }

    public static class Directions
    {
        /// <summary>Thứ tự duyệt cố định: N, NE, E, SE, S, SW, W, NW.</summary>
        public static readonly IReadOnlyList<Direction> Order = new[]
        {
            Direction.N, Direction.NE, Direction.E, Direction.SE,
            Direction.S, Direction.SW, Direction.W, Direction.NW,
        };

        // N là Y + 1.
        static readonly GridPos[] Deltas =
        {
            new GridPos(0, 1),   // N
            new GridPos(1, 1),   // NE
            new GridPos(1, 0),   // E
            new GridPos(1, -1),  // SE
            new GridPos(0, -1),  // S
            new GridPos(-1, -1), // SW
            new GridPos(-1, 0),  // W
            new GridPos(-1, 1),  // NW
        };

        public static GridPos ToDelta(this Direction direction) => Deltas[(int)direction];

        public static bool IsDiagonal(this Direction direction) => ((int)direction & 1) == 1;

        /// <summary>Chuyển độ lệch giữa hai ô kề nhau thành hướng. Sai nếu không phải ô kề.</summary>
        public static bool TryFromDelta(GridPos delta, out Direction direction)
        {
            for (int i = 0; i < Deltas.Length; i++)
            {
                if (Deltas[i] == delta)
                {
                    direction = (Direction)i;
                    return true;
                }
            }

            direction = default;
            return false;
        }

        /// <summary>Đọc tên hướng ("N", "NE", ...), không phân biệt hoa thường.</summary>
        public static bool TryParse(string token, out Direction direction)
        {
            switch (token.Trim().ToUpperInvariant())
            {
                case "N": direction = Direction.N; return true;
                case "NE": direction = Direction.NE; return true;
                case "E": direction = Direction.E; return true;
                case "SE": direction = Direction.SE; return true;
                case "S": direction = Direction.S; return true;
                case "SW": direction = Direction.SW; return true;
                case "W": direction = Direction.W; return true;
                case "NW": direction = Direction.NW; return true;
                default: direction = default; return false;
            }
        }
    }
}
