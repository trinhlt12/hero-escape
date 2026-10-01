using HeroEscape.Core;

namespace HeroEscape.Unity
{
    /// <summary>Mọi chuỗi hiển thị của Milestone 01, gom một chỗ để kiểm tra font.</summary>
    public static class UiText
    {
        public const string Title = "Hero Escape · Màn thử M01";
        public const string WaitButton = "Chờ";
        public const string RestartButton = "Chơi lại";
        public const string Hint = "Bấm một ô kề bên (8 hướng) để đi. Nút Chờ để đứng yên.\nĐến ô Cửa xanh để thắng; đừng để X01 bắt.";

        public const string Won = "THẮNG! Hero đã đến Cửa.";
        public const string Lost = "THUA! X01 đã bắt được Hero.";
        public const string EnemyTurn = "X01 đang đi…";
        public const string PressRestart = "Bấm Chơi lại để thử lại.";
        public const string Restarted = "Đã chơi lại từ đầu.";
        public const string EnemyStayed = "X01 không có đường nên đứng yên.";

        public const string ClickHero = "Đây là Hero. Muốn đứng yên, hãy bấm nút Chờ.";
        public const string NotAdjacent = "Chỉ đi được sang một ô kề bên.";
        public const string Wall = "Không đi được: ô đó là Tường.";
        public const string OccupiedByEnemy = "Không đi được: X01 đang đứng ở ô đó.";
        public const string CornerBlocked = "Không đi chéo được: góc bị Tường hoặc X01 chặn.";
        public const string OutOfBounds = "Không đi được: ra ngoài bàn.";
        public const string LevelError = "Lỗi dữ liệu level. Xem Console.";

        public static string PlayerTurn(int round) => $"Vòng {round} · Lượt của bạn";

        public static string ForError(ActionError error)
        {
            switch (error)
            {
                case ActionError.Wall: return Wall;
                case ActionError.OccupiedByEnemy: return OccupiedByEnemy;
                case ActionError.CornerBlocked: return CornerBlocked;
                case ActionError.OutOfBounds: return OutOfBounds;
                case ActionError.GameOver: return PressRestart;
                default: return string.Empty;
            }
        }

        /// <summary>Tất cả ký tự có thể hiển thị, dùng để kiểm tra font có đủ glyph tiếng Việt.</summary>
        public static string AllDisplayedText =>
            Title + WaitButton + RestartButton + Hint + Won + Lost + EnemyTurn + PressRestart + Restarted + EnemyStayed +
            ClickHero + NotAdjacent + Wall + OccupiedByEnemy + CornerBlocked + OutOfBounds + LevelError +
            PlayerTurn(1234567890);
    }
}
