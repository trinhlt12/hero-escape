using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroEscape.Unity
{
    /// <summary>Một màn trong catalog: ID ổn định (trùng <c>id:</c> trong file level), tên hiển thị, mô tả ngắn và file level.</summary>
    [Serializable]
    public sealed class LevelEntry
    {
        [Tooltip("ID ổn định, phải trùng dòng 'id:' trong file level. Không dùng tên hiển thị làm định danh.")]
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] string description;
        [SerializeField] TextAsset levelFile;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public TextAsset LevelFile => levelFile;
    }

    /// <summary>
    /// Danh sách màn dùng chung cho màn Level Select và gameplay scene. Chỉ trỏ tới file level; bố cục và cấu hình
    /// (Dash, Stasis, profile AI, X01) nằm trong file level, không chép vào UI.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Hero Escape/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] LevelEntry[] levels = new LevelEntry[0];
        [Tooltip("Màn mở khi chạy thẳng gameplay scene mà chưa chọn màn.")]
        [SerializeField] string defaultLevelId;
        [SerializeField] string gameplayScene = "M01_TestLevel";
        [SerializeField] string levelSelectScene = "LevelSelect";

        public IReadOnlyList<LevelEntry> Levels => levels;
        public string DefaultLevelId => defaultLevelId;
        public string GameplayScene => gameplayScene;
        public string LevelSelectScene => levelSelectScene;

        /// <summary>Vị trí của màn có ID này, -1 nếu không có.</summary>
        public int IndexOf(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return -1;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && levels[i].Id == levelId) return i;
            return -1;
        }

        /// <summary>Màn mặc định; nếu chưa cấu hình hoặc sai thì là màn đầu tiên.</summary>
        public int DefaultIndex
        {
            get
            {
                int index = IndexOf(defaultLevelId);
                return index >= 0 ? index : 0;
            }
        }
    }
}
