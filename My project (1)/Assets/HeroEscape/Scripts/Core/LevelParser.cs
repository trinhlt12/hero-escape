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
    /// H . . . X      (X = một X01; nhiều X01 dùng 1, 2, 3 theo thứ tự hành động)
    /// dash: 2        (tùy chọn; số lần Dash mỗi lần chơi, mặc định 0 = không có Dash)
    /// stasis: 1      (tùy chọn; số lần dùng thẻ Stasis, mặc định 0 = không có Stasis)
    /// barrier: 1     (tùy chọn; số lần dùng thẻ Barrier, mặc định 0 = không có Barrier)
    /// ai: tactical   (tùy chọn; profile AI của X01: basic hoặc tactical, mặc định basic)
    /// solution: N NE DASH-E STASIS+WAIT BARRIER-N+E
    /// </code>
    /// Ký hiệu: . Sàn, # Tường, E Cửa, H Hero (trên Sàn), X X01 (trên Sàn); 1/2/3 = X01 có ID X1/X2/X3 hành động theo thứ tự đó.
    /// Dấu cách trong hàng được bỏ qua.
    /// Bước lời giải (một lượt): một hướng (N NE E SE S SW W NW), WAIT, hoặc DASH-hướng (ví dụ DASH-NE).
    /// Tiền tố STASIS+ nghĩa là dùng Stasis lên X01 trước hành động chính của lượt đó (ví dụ STASIS+DASH-E);
    /// với nhiều X01 phải ghi mục tiêu: STASIS(X2)+DASH-E.
    /// Tiền tố BARRIER-hướng+ nghĩa là đặt Barrier ở ô kề Hero theo hướng đó trước hành động chính (ví dụ BARRIER-NE+WAIT).
    /// Có thể ghi nhiều dòng solution: (mỗi dòng một lời giải độc lập); dòng đầu là lời giải tham chiếu.
    /// </summary>
    public static class LevelParser
    {
        public const string WaitToken = "WAIT";
        public const string DashPrefix = "DASH-";
        public const string StasisPrefix = "STASIS+";
        public const string BarrierPrefix = "BARRIER-";
        const string StasisWord = "STASIS";

        public static LevelData Parse(string text)
        {
            if (text == null) throw new LevelFormatException("Nội dung level rỗng.");

            string id = null;
            var solutionLines = new List<string>();
            int dashMaxUses = 0;
            int stasisMaxUses = 0;
            int barrierMaxUses = 0;
            AiProfile aiProfile = AiProfile.Basic;
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
                    solutionLines.Add(solutionValue);
                    inGrid = false;
                }
                else if (TryReadKey(line, "dash:", out string dashValue))
                {
                    if (!int.TryParse(dashValue, out dashMaxUses))
                        throw Error(i, $"Giá trị 'dash:' phải là số nguyên, đang là '{dashValue}'.");
                    inGrid = false;
                }
                else if (TryReadKey(line, "stasis:", out string stasisValue))
                {
                    if (!int.TryParse(stasisValue, out stasisMaxUses))
                        throw Error(i, $"Giá trị 'stasis:' phải là số nguyên, đang là '{stasisValue}'.");
                    inGrid = false;
                }
                else if (TryReadKey(line, "barrier:", out string barrierValue))
                {
                    if (!int.TryParse(barrierValue, out barrierMaxUses))
                        throw Error(i, $"Giá trị 'barrier:' phải là số nguyên, đang là '{barrierValue}'.");
                    inGrid = false;
                }
                else if (TryReadKey(line, "ai:", out string aiValue))
                {
                    if (string.Equals(aiValue, "basic", StringComparison.OrdinalIgnoreCase)) aiProfile = AiProfile.Basic;
                    else if (string.Equals(aiValue, "tactical", StringComparison.OrdinalIgnoreCase)) aiProfile = AiProfile.Tactical;
                    else throw Error(i, $"Giá trị 'ai:' phải là basic hoặc tactical, đang là '{aiValue}'.");
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
            var enemies = new List<EnemySpawn>();

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
                        case '1':
                        case '2':
                        case '3':
                        {
                            // 'X' (định dạng cũ) = X01 hành động thứ 1. '1'..'3' = X01 với thứ tự hành động tương ứng.
                            int spawnIndex = c == 'X' ? 1 : c - '0';
                            if (enemies.Exists(e => e.SpawnIndex == spawnIndex))
                                throw Error(line, $"Trùng X01 thứ tự {spawnIndex} ('{c}'). Mỗi thứ tự chỉ khai báo một lần; nhiều X01 thì dùng 1, 2, 3.");
                            enemies.Add(new EnemySpawn(LevelData.DefaultEnemyId(spawnIndex), spawnIndex, pos));
                            cells[y * width + x] = CellType.Floor;
                            break;
                        }
                        default:
                            throw Error(line, $"Ký hiệu '{c}' không hợp lệ. Dùng . # E H X (hoặc 1 2 3 cho nhiều X01).");
                    }
                }
            }

            if (!hero.HasValue) throw new LevelFormatException($"[{id}] Lưới thiếu Hero (H).");
            if (enemies.Count == 0) throw new LevelFormatException($"[{id}] Lưới thiếu X01 (X hoặc 1, 2, 3).");

            var solutions = new List<List<SolutionTurn>>();
            foreach (string line in solutionLines) solutions.Add(ParseTurns(id, line, enemies));

            // Lời giải đầu tiên giữ dạng cũ (chuỗi hành động + bảng phụ trợ theo lượt) để test và công cụ cũ dùng tiếp.
            List<SolutionTurn> reference = solutions.Count > 0 ? solutions[0] : new List<SolutionTurn>();
            var actions = new List<HeroAction>();
            var stasisTargets = new Dictionary<int, string>();
            var barrierDirections = new Dictionary<int, Direction>();
            for (int t = 0; t < reference.Count; t++)
            {
                actions.Add(reference[t].Main);
                if (reference[t].StasisTarget != null) stasisTargets[t] = reference[t].StasisTarget;
                if (reference[t].BarrierDirection.HasValue) barrierDirections[t] = reference[t].BarrierDirection.Value;
            }

            var alternatives = new List<IReadOnlyList<SolutionTurn>>();
            for (int s = 1; s < solutions.Count; s++) alternatives.Add(solutions[s]);

            return new LevelData(id, width, height, cells, hero.Value, enemies, actions, dashMaxUses, stasisMaxUses, stasisTargets, aiProfile,
                barrierMaxUses, barrierDirections, alternatives);
        }

        public static IReadOnlyList<HeroAction> ParseActions(string levelId, string text)
        {
            var actions = new List<HeroAction>();
            foreach (SolutionTurn turn in ParseTurns(levelId, text, null)) actions.Add(turn.Main);
            return actions;
        }

        /// <summary>Đọc một dòng lời giải thành các lượt (phụ trợ tùy chọn + hành động chính).</summary>
        /// <param name="enemies">X01 của level; dùng để xác định mục tiêu khi viết tắt STASIS+ (chỉ hợp lệ khi có đúng một X01).</param>
        public static List<SolutionTurn> ParseTurns(string levelId, string text, List<EnemySpawn> enemies)
        {
            var turns = new List<SolutionTurn>();
            if (string.IsNullOrWhiteSpace(text)) return turns;

            foreach (string rawToken in text.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = rawToken;
                string target = null;
                Direction? barrier = null;
                // Tiền tố phụ trợ. Luật tối đa một phụ trợ mỗi lượt do Simulation kiểm tra khi phát lại, không phải parser.
                while (true)
                {
                    if (target == null && TryReadStasis(levelId, token, enemies, out string stasisTarget, out string afterStasis))
                    {
                        target = stasisTarget;
                        token = afterStasis;
                    }
                    else if (!barrier.HasValue && TryReadBarrier(levelId, token, out Direction barrierDirection, out string afterBarrier))
                    {
                        barrier = barrierDirection;
                        token = afterBarrier;
                    }
                    else break;
                }

                HeroAction main;
                if (string.Equals(token, WaitToken, StringComparison.OrdinalIgnoreCase))
                    main = HeroAction.Wait;
                else if (Directions.TryParse(token, out Direction direction))
                    main = HeroAction.Move(direction);
                else if (token.StartsWith(DashPrefix, StringComparison.OrdinalIgnoreCase) &&
                         Directions.TryParse(token.Substring(DashPrefix.Length), out Direction dashDirection))
                    main = HeroAction.Dash(dashDirection);
                else
                    throw new LevelFormatException($"[{levelId}] Lời giải có bước '{rawToken}' không hợp lệ. Dùng N NE E SE S SW W NW, " +
                                                   $"{WaitToken}, {DashPrefix}<hướng>, có thể kèm tiền tố {StasisPrefix} hoặc {BarrierPrefix}<hướng>+.");

                turns.Add(new SolutionTurn(target, barrier, main));
            }

            return turns;
        }

        /// <summary>Đọc tiền tố "BARRIER-hướng+" của một bước lời giải.</summary>
        static bool TryReadBarrier(string levelId, string token, out Direction direction, out string rest)
        {
            direction = default;
            rest = token;
            if (!token.StartsWith(BarrierPrefix, StringComparison.OrdinalIgnoreCase)) return false;

            int plus = token.IndexOf('+');
            if (plus < 0) throw new LevelFormatException($"[{levelId}] Bước '{token}' thiếu dấu '+' trước hành động chính.");
            string head = token.Substring(BarrierPrefix.Length, plus - BarrierPrefix.Length);
            if (!Directions.TryParse(head, out direction))
                throw new LevelFormatException($"[{levelId}] Bước '{token}': hướng Barrier '{head}' không hợp lệ (N NE E SE S SW W NW).");
            rest = token.Substring(plus + 1);
            return true;
        }

        /// <summary>
        /// Đọc tiền tố Stasis của một bước lời giải: "STASIS(X2)+N" (ghi rõ mục tiêu) hoặc "STASIS+N"
        /// (chỉ khi level có đúng một X01).
        /// </summary>
        static bool TryReadStasis(string levelId, string token, List<EnemySpawn> enemies, out string target, out string rest)
        {
            target = null;
            rest = token;
            if (!token.StartsWith(StasisWord, StringComparison.OrdinalIgnoreCase)) return false;

            int plus = token.IndexOf('+');
            if (plus < 0) throw new LevelFormatException($"[{levelId}] Bước '{token}' thiếu dấu '+' trước hành động chính.");

            string head = token.Substring(StasisWord.Length, plus - StasisWord.Length);
            if (head.Length == 0)
            {
                if (enemies != null && enemies.Count != 1)
                    throw new LevelFormatException($"[{levelId}] Bước '{token}': level có {enemies.Count} X01, hãy ghi mục tiêu, ví dụ STASIS(X2)+N.");
                target = enemies != null ? enemies[0].Id : LevelData.DefaultEnemyId(1);
            }
            else if (head.Length > 2 && head[0] == '(' && head[head.Length - 1] == ')')
            {
                target = head.Substring(1, head.Length - 2);
            }
            else
            {
                throw new LevelFormatException($"[{levelId}] Bước '{token}' không hợp lệ. Dùng STASIS+<hành động> hoặc STASIS(<ID>)+<hành động>.");
            }

            rest = token.Substring(plus + 1);
            return true;
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
