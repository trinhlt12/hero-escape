using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>Xóa lựa chọn màn khi bắt đầu mỗi lần Play (chạy cả khi Editor tắt Domain Reload).</summary>
    static class LevelSelectionReset
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => LevelSelection.Clear();
    }
}
