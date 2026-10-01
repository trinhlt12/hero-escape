using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HeroEscape.EditorTools
{
    /// <summary>
    /// Kiểm tra (chỉ đọc) 5 sprite dùng trong Milestone 01. Không sửa, di chuyển hay reimport art.
    /// </summary>
    public static class M01ArtCheck
    {
        public const string FloorPath = "Assets/Art/Board/tile_floor.asset";
        public const string WallPath = "Assets/Art/Board/tile_wall.asset";
        public const string ExitPath = "Assets/Art/Board/tile_exit.asset";
        public const string HeroPath = "Assets/Art/Actors/hero_h01.asset";
        public const string EnemyPath = "Assets/Art/Actors/enemy_x01.asset";

        public sealed class ArtSet
        {
            public Sprite Floor, Wall, Exit, Hero, Enemy;
        }

        [MenuItem("Hero Escape/Milestone 01/Kiểm tra art M01", priority = 20)]
        static void RunFromMenu()
        {
            bool ok = Run(out _);
            EditorUtility.DisplayDialog("Kiểm tra art M01",
                ok ? "Đủ 5 sprite. Xem chi tiết và cảnh báo (nếu có) trong Console." : "Thiếu sprite. Xem Console.", "Đóng");
        }

        /// <summary>Trả về false nếu thiếu sprite (lỗi chặn). Cảnh báo cấu hình không chặn.</summary>
        public static bool Run(out ArtSet art)
        {
            var report = new StringBuilder("[M01 Art] Kiểm tra 5 sprite:\n");
            var errors = new List<string>();
            var warnings = new List<string>();

            art = new ArtSet
            {
                Floor = Check(FloorPath, report, errors, warnings),
                Wall = Check(WallPath, report, errors, warnings),
                Exit = Check(ExitPath, report, errors, warnings),
                Hero = Check(HeroPath, report, errors, warnings),
                Enemy = Check(EnemyPath, report, errors, warnings),
            };

            foreach (string w in warnings) report.AppendLine("  CẢNH BÁO: " + w);
            foreach (string e in errors) report.AppendLine("  LỖI: " + e);

            if (errors.Count > 0) Debug.LogError(report.ToString());
            else if (warnings.Count > 0) Debug.LogWarning(report.ToString());
            else Debug.Log(report.ToString());

            return errors.Count == 0;
        }

        static Sprite Check(string path, StringBuilder report, List<string> errors, List<string> warnings)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                var candidates = new List<string>();
                foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
                    candidates.Add(AssetDatabase.GUIDToAssetPath(guid));
                errors.Add($"Không tìm thấy Sprite tại {path}." +
                           (candidates.Count > 0 ? " Ứng viên: " + string.Join(", ", candidates) : string.Empty));
                return null;
            }

            Texture2D texture = sprite.texture;
            string texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) : "(không có)";
            Vector2 pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            report.AppendLine($"  {path}: rect {sprite.rect.width:0}x{sprite.rect.height:0}px, PPU {sprite.pixelsPerUnit:0}, " +
                              $"pivot ({pivot.x:0.##},{pivot.y:0.##}), texture {texturePath}");

            if (Mathf.Abs(pivot.x - 0.5f) > 0.01f || Mathf.Abs(pivot.y - 0.5f) > 0.01f)
                warnings.Add($"{path}: pivot không ở giữa.");

            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                if (importer.textureType != TextureImporterType.Sprite)
                    warnings.Add($"{texturePath}: Texture Type không phải Sprite (2D and UI).");
                if (importer.mipmapEnabled)
                    warnings.Add($"{texturePath}: Mipmap đang bật.");
                if (!importer.DoesSourceTextureHaveAlpha())
                    warnings.Add($"{texturePath}: ảnh nguồn không có kênh alpha.");
                else if (!importer.alphaIsTransparency)
                    warnings.Add($"{texturePath}: Alpha Is Transparency đang tắt.");
            }
            else
            {
                warnings.Add($"{path}: không đọc được TextureImporter của {texturePath}.");
            }

            return sprite;
        }
    }
}
