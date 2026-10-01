using System;
using System.Collections.Generic;

namespace HeroEscape.Core
{
    /// <summary>
    /// Đọc level dạng text. Định dạng:
    /// <code>
    /// // chú thích
    /// id: M01_TEST
    /// grid:
    /// . . # . E      (dòng đầu là hàng trên cùng, Y lớn nhất)
    /// H . . . X
    /// dash: 2        (tùy chọn; số lần Dash mỗi lần chơi, mặc định 0 = không có Dash)
    /// solution: N NE DASH-E WAIT
    /// </code>
    /// Ký hiệu: . Sàn, # Tường, E Cửa, H Hero (trên Sàn), X X01 (trên Sàn). Dấu cách trong hàng được bỏ qua.
    /// Bước lời giải: một hướng (N NE E SE S SW W NW), WAIT, hoặc DASH-hướng (ví dụ DASH-NE).
    /// </summary>
    public static class LevelParser
    {
        public const string WaitToken = "WAIT";
        public const string DashPrefix = "DASH-";

        public static LevelData Parse(string text)
        {
            if (text == null) throw new LevelFormatException("Nội dung level rỗng.");

            string id = null;
            string solution = null;
            int dashMaxUses = 0;
            var rows = new List<(int line, string row)>();
            bool inGrid = false;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal)) continue;

                if (TryReadKey(line, "id:", out string idValue))
                {
                    id = idValue;
                    inGrid = false;
                }
                else if (TryReadKey(line, "grid:", out string gridRest))
                {
                    if (gridRest.Length > 0) throw Error(i, "Hàng đầu tiên của lưới phải nằm ở dòng sau 'grid:'.");
                    inGrid = true;
                }
                else if (TryReadKey(line, "solution:", out string solutionValue))
                {
                    solution = solutionValue;
                    inGrid = false;
                }
                else if (TryReadKey(line, "dash:", out string dashValue))
                {
                    if (!int.TryParse(dashValue, out dashMaxUses))
                        throw Error(i, $"Giá trị 'dash:' phải là số nguyên, đang là '{dashValue}'.");
                    inGrid = false;
                }
                else if (inGrid)
                {
                    rows.Add((i, line.Replace(" ", string.Empty).Replace("\t", string.Empty)));
                }
                else
                {
                    throw Error(i, $"Không hiểu dòng '{line}'.");
                }
            }

            if (rows.Count == 0) throw new LevelFormatException($"[{id}] Thiếu lưới (mục 'grid:').");

            int height = rows.Count;
            int width = rows[0].row.Length;
            var cells = new CellType[width * height];
            GridPos? hero = null;
            GridPos? enemy = null;

            for (int r = 0; r < height; r++)
            {
                (int line, string row) = rows[r];
                if (row.Length != width) throw Error(line, $"Hàng có {row.Length} ô, cần {width} ô như hàng đầu.");

                int y = height - 1 - r; // dòng đầu là hàng trên cùng
                for (int x = 0; x < width; x++)
                {
                    char c = row[x];
                    var pos = new GridPos(x, y);
                    switch (c)
                    {
                        case '.': cells[y * width + x] = CellType.Floor; break;
                        case '#': cells[y * width + x] = CellType.Wall; break;
                        case 'E': cells[y * width + x] = CellType.Exit; break;
                        case 'H':
                            if (hero.HasValue) throw Error(line, "Chỉ được có một Hero (H).");
                            hero = pos;
                            cells[y * width + x] = CellType.Floor;
                            break;
                        case 'X':
                            if (enemy.HasValue) throw Error(line, "Milestone 01 chỉ có đúng một X01 (X).");
                            enemy = pos;
                            cells[y * width + x] = CellType.Floor;
                            break;
                        default:
                            throw Error(line, $"Ký hiệu '{c}' không hợp lệ. Dùng . # E H X.");
                    }
                }
            }

            if (!hero.HasValue) throw new LevelFormatException($"[{id}] Lưới thiếu Hero (H).");
            if (!enemy.HasValue) throw new LevelFormatException($"[{id}] Lưới thiếu X01 (X).");

            return new LevelData(id, width, height, cells, hero.Value, enemy.Value, ParseActions(id, solution), dashMaxUses);
        }

        public static IReadOnlyList<HeroAction> ParseActions(string levelId, string text)
        {
            var actions = new List<HeroAction>();
            if (string.IsNullOrWhiteSpace(text)) return actions;

            foreach (string token in text.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(token, WaitToken, StringComparison.OrdinalIgnoreCase))
                    actions.Add(HeroAction.Wait);
                else if (Directions.TryParse(token, out Direction direction))
                    actions.Add(HeroAction.Move(direction));
                else if (token.StartsWith(DashPrefix, StringComparison.OrdinalIgnoreCase) &&
                         Directions.TryParse(token.Substring(DashPrefix.Length), out Direction dashDirection))
                    actions.Add(HeroAction.Dash(dashDirection));
                else
                    throw new LevelFormatException($"[{levelId}] Lời giải có bước '{token}' không hợp lệ. Dùng N NE E SE S SW W NW, {WaitToken} hoặc {DashPrefix}<hướng>.");
            }

            return actions;
        }

        static bool TryReadKey(string line, string key, out string value)
        {
            if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                value = line.Substring(key.Length).Trim();
                return true;
            }

            value = null;
            return false;
        }

        static LevelFormatException Error(int lineIndex, string message) =>
            new LevelFormatException($"Dòng {lineIndex + 1}: {message}");
    }
}
