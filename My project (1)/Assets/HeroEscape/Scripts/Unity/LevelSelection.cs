namespace HeroEscape.Unity
{
    /// <summary>
    /// Dữ liệu duy nhất truyền từ màn Level Select sang gameplay scene: ID màn cần mở.
    /// Gameplay đọc một lần rồi xóa (<see cref="TakePending"/>). Được xóa khi bắt đầu mỗi lần Play
    /// (<see cref="LevelSelectionReset"/>) nên lựa chọn của phiên Play trước không tồn tại, kể cả khi tắt Domain Reload.
    /// Không giữ GameSession, bàn cờ hay trạng thái chơi nào giữa các scene.
    /// </summary>
    public static class LevelSelection
    {
        static string pendingLevelId;

        public static string PendingLevelId => pendingLevelId;

        public static void Request(string levelId) => pendingLevelId = levelId;

        /// <summary>Lấy ID đang chờ rồi xóa; null nếu không có (ví dụ mở thẳng gameplay scene).</summary>
        public static string TakePending()
        {
            string id = pendingLevelId;
            pendingLevelId = null;
            return id;
        }

        public static void Clear() => pendingLevelId = null;
    }
}
