# Milestone 03 — Thẻ phụ trợ Stasis và Level 03

Ngày: 2026-10-01 • Trạng thái: **Hoàn thành.** Game Designer đã playtest Level 01–03 thành công (báo cáo 2026-10-01). Bảng mục 5 giữ nguyên kết quả do Claude ghi lúc bàn giao.

> Từ Milestone 04: Core dùng danh sách X01 (Level 03 nạp thành danh sách một phần tử, hành vi không đổi); nhãn Frozen được quản lý theo từng X (xem `Docs/Milestone04.md`).

Khóa phạm vi task Stasis + Level 03. Level 01 và Level 02 đã được Game Designer playtest thành công (báo cáo 2026-10-01); gameplay và file level của hai màn này giữ nguyên.

## 1. Phạm vi

### In-scope

- Thẻ phụ trợ **Stasis** (GDD S01), tách khỏi hành động chính, cấu hình số lần theo level (`stasis: N`, mặc định 0).
- Nút `Stasis (1/1)`, chế độ chọn mục tiêu, nhãn `Frozen` trên X01, mô tả ngắn.
- **Level 03** (`M03_StasisLevel.txt`): 5x5, Hero H01, một X01 Basic AI, Dash 2, Stasis 1.
- Điều hướng: `Next Level` sau khi thắng Level 02; `Back to Level 02` ở Level 03 (nút Back luôn về màn liền trước, nên Level 02 vẫn là `Back to Level 01`).

### Out-of-scope

Các thẻ khác, bộ bài, chọn loadout, rarity, nâng cấp • passive, H02, X02, nhiều quái, Boss • Tactical AI, preview, Undo, replay • generator, solver sản phẩm, các màn khác • âm thanh, build Android, backend • art mới.

## 2. Luật Stasis đã triển khai

Trong Core (`Simulation.ApplyStasis`, `GameSession.ApplyStasis`), độc lập với `Simulation.Resolve`.

- **Điều kiện dùng:**
  - Đang chơi.
  - Level có Stasis và còn lượt dùng.
  - Chưa dùng phụ trợ trong lượt (`SupportUsedThisTurn`).
  - Ô được chọn là ô X01, và X01 chưa bị khóa.
  - Chebyshev ≤ 3 (`Simulation.StasisRange`).
  - Có đường nhìn.
- **Đường nhìn** (`GridRules.HasLineOfSight`): supercover nguyên từ tâm ô tới tâm ô.
  - Chỉ Tường chắn; Hero, X01 và Cửa không chắn. Bỏ qua ô nguồn và ô đích.
  - Khi tia đi đúng qua một góc, cả hai ô cạnh tại góc đều được xét; một trong hai là Tường thì bị chắn.
  - Không dùng physics. Không liên quan tới luật đi đến mục tiêu.
- **Khi thành công:**
  - Trừ 1 lần dùng, đặt `SupportUsedThisTurn = true`, `EnemyLockPhases = 1` (`Simulation.StasisLockPhases`).
  - Không chạy AI, không kết thúc lượt, không tăng vòng. Hero vẫn chọn Move, Wait hoặc Dash.
- **Pha địch:**
  - Nếu X01 bị khóa: bỏ qua toàn bộ hành động (sự kiện `EnemySkipped`), rồi giảm khóa 1 → 0.
  - Pha sau X01 hoạt động bình thường.
  - Khóa chỉ giảm ở pha địch; không giảm khi chọn thẻ, chọn hướng hoặc khi hành động bị từ chối. Không phụ thuộc thời gian thực.
- **Kết thúc vòng:** xóa `SupportUsedThisTurn`.
- **X01 bị khóa vẫn chiếm ô và chặn góc:** Move/Dash vào hoặc xuyên X01 bị từ chối như cũ (dùng chung `GridRules.CheckStep`).
- **Thắng ở Cửa:** thắng ngay, không chạy pha địch.
- **Toàn vẹn:**
  - Chọn thẻ hoặc chọn mục tiêu sai không tiêu hao gì.
  - Sau khi Stasis đã áp, hành động chính sai không hoàn thẻ, không kết thúc lượt, không làm hết khóa.
  - Hủy chọn hướng Dash sau đó cũng không hoàn tác Stasis. Không có Undo.

`GameState` thêm `StasisUsesLeft`, `EnemyLockPhases`, `SupportUsedThisTurn`; Restart trả về trạng thái đầu màn.

## 3. Input và UI

`TurnInputController` (C# thuần) có thêm chế độ `ChoosingStasisTarget`.

- Bấm **Stasis** → `Select an enemy within 3 tiles.`, nút đổi màu vàng.
- Trong chế độ chọn Stasis, bấm bàn cờ **chỉ chọn mục tiêu**, không di chuyển Hero.
  - Bấm X01 hợp lệ: áp ngay, hiện `X01 is frozen. Now move, wait or dash.`, thoát chế độ chọn.
  - Bấm sai, ngoài tầm hoặc bị che: phản hồi tiếng Anh, giữ chế độ chọn.
- Bấm Stasis lần nữa hoặc **Escape**: hủy (`Stasis cancelled.`), không mất thẻ.
- Dash và Stasis loại trừ nhau: chọn Dash thì hủy chọn Stasis chưa áp, và ngược lại. Wait bị vô hiệu hóa khi đang chọn.
- Nút Stasis bị vô hiệu hóa khi hết thẻ hoặc đã dùng phụ trợ trong lượt. Level 01/02 không hiện nút Stasis.
- Nhãn **Frozen** (text uGUI, không nhận raycast) bám phía trên X01 khi X01 đang bị khóa. Trong pha địch bị bỏ qua, thông báo `X01 was frozen and skipped its turn.`
- Dòng hướng dẫn Level 03: `Stasis: Skip this enemy's next turn. You can still move or dash.`
- Restart, chuyển màn, thắng/thua đều xóa chế độ chọn; vào màn tạo `GameSession` và `TurnInputController` mới.
- Hàng nút hành động: Wait 200 • Dash 250 • Stasis 270 • Restart 220, cách 20, chữ 40. Tổng 1000 trên vùng 1000 của canvas 1080.

## 4. Level 03 (`M03_STASIS`)

Gốc (0,0) ở góc dưới trái; x tăng sang phải, y tăng lên trên; N = y + 1.

```
y=4   .  .  .  .  E
y=3   .  #  #  #  .
y=2   .  .  X  #  .
y=1   #  .  .  .  #
y=0   .  .  H  .  .
     x=0 1  2  3  4
```

- Hero (2,0) • X01 (2,2) • Cửa (4,4) • Dash 2 • Stasis 1.
- Tường: (1,3), (2,3), (3,3), (3,2), (0,1), (4,1).

Lời giải tham chiếu (trong file): `W STASIS+DASH-N W N N E E DASH-E`

| Lượt | Phụ trợ | Hành động chính | Hero | X01 sau lượt |
|---|---|---|---|---|
| 1 | — (giữ thẻ) | W | (1,0) | (2,1) |
| 2 | **Stasis → X01 tại (2,1)** | **DASH-N** (bấm ô (1,1)) | (1,2) | (2,1), bị khóa, bỏ lượt |
| 3 | — | W | (0,2) | (1,2) |
| 4 | — | N | (0,3) | (0,2) |
| 5 | — | N | (0,4) | (0,3) |
| 6 | — | E | (1,4) | (0,4) |
| 7 | — | E | (2,4) | (1,4) |
| 8 | — | **DASH-E** | (4,4) Cửa → **You Win!** | không đi |

**Dùng sai thời điểm:** ở lượt 1, X01 cách 2 ô và có đường nhìn, nên Stasis **hợp lệ**. Nhưng dùng lúc này thì không còn đường thắng (duyệt hết: Unsolvable). Ví dụ `STASIS+W` rồi `DASH-N`: Hero đáp xuống (1,2) ngay cạnh X01 (2,2) vừa hết khóa, bị bắt. Giữ thẻ đến lượt 2 thì thắng.

## 5. Kết quả kiểm tra

Quy ước: Passed • Failed • Not run • Unknown.

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M03-C1 | Compile Core, Search, Unity, Editor | Passed (ngoài Unity) | Roslyn của Unity 6000.3.8f1, DLL Unity 6000.3.8f1 + `UnityEngine.UI` + `Unity.InputSystem`; .NET Standard 2.1, C# 9. Chỉ cảnh báo CS0649. |
| M03-C2 | Unity Editor compile bản này | Not run | Editor đang mở project. |
| M03-T1 | 114 test EditMode | Passed (ngoài Unity) | Bộ chạy reflection trên .NET 6 + `nunit.framework.dll` của Unity: 114 passed, 0 failed (75 cũ + 39 mới). |
| M03-T2 | Test Runner trong Unity | Not run | |
| M03-V1 | Level 03 có lời giải (Dash ≤ 2, Stasis ≤ 1) | Passed | BFS: Solved, 50 trạng thái; lời giải phát lại bằng Simulation thắng. |
| M03-V2 | Level 03 **không** có lời giải khi tắt Stasis (vẫn 2 Dash) | Passed (duyệt hết) | Unsolvable sau khi duyệt hết **11** trạng thái, giới hạn 100.000. Thêm: chỉ Move/Wait 7 trạng thái và chỉ Stasis (0 Dash) 22 trạng thái, đều Unsolvable. |
| M03-V3 | Không thắng ở lượt đầu (mọi Move/Dash/Wait, có hoặc không có Stasis) | Passed | Test `NoImmediateWin_OnFirstTurn_WithAnyMainActionOrStasisCombo`. |
| M03-V4 | Lời giải có Stasis hợp lệ | Passed | Lượt 2: Stasis lên X01 (2,1) rồi DASH-N. |
| M03-V5 | Thời điểm Stasis có ý nghĩa | Passed | Stasis hợp lệ ở 11 trạng thái đạt tới được, 10 trong số đó dùng xong thì hết đường thắng. Trạng thái đầu màn là trạng thái "then chốt": dùng ngay thì Unsolvable, giữ lại thì thắng. Test `Timing_*`. |
| M03-R1 | Level 01/02 giữ nguyên kết quả | Passed | Vị trí Hero/X01 sau từng lượt của chuỗi tham chiếu trùng với M01/M02; cả hai màn có Stasis = 0 và từ chối Stasis. |
| M03-S1 | Menu dựng lại scene | Not run | Cần chạy trong Editor. |
| M03-P1 | Play Mode Level 03, nhãn Frozen, chuyển màn | Not run | Chưa playtest. |
| M03-P2 | Bố cục 4 nút ở 9:16 và 9:20 | Not run | Đã tính: hàng nút rộng 1000/1000 đơn vị tham chiếu. |

Công cụ kiểm chứng `Tests/EditMode/LevelReachability.cs`:
- BFS chỉ trong assembly test, dùng `Simulation` và `BasicChasePolicy` thật.
- Mỗi cạnh là một lượt: không dùng thẻ, hoặc Stasis lên X01, rồi một hành động chính hợp lệ.
- Khóa trạng thái: (Hero, X01, Dash còn, Stasis còn, thời hạn khóa, cờ đã dùng phụ trợ).
- Trả `Unknown` khi chạm giới hạn; lời giải tìm được được phát lại bằng `Replay`.
- Không phải solver sản phẩm hay generator.

Test mới:
- `StasisTests` (20): tầm 3, đường chéo, tầm 4; Tường chắn; góc chắn ở từng cạnh và góc thông; Cửa không chắn; mục tiêu sai; không dùng hai lần (cùng lượt và hết thẻ); Stasis + Move/Wait/Dash; hành động sai sau Stasis giữ khóa; X01 khóa vẫn chặn và chặn góc; thắng không chạy địch; màn không có Stasis và sau kết thúc; Restart; parser.
- `TurnInputControllerTests` (+10): chế độ chọn và hủy; bấm bàn chỉ chọn mục tiêu; click UI; áp lên X01 rồi vẫn hành động; ngoài tầm/bị che; Dash và Stasis loại trừ nhau; hủy Dash không hoàn tác Stasis; nút bị khóa sau khi dùng phụ trợ; màn không Stasis; Restart.
- `Level03Tests` (7) và `Level02RegressionTests` (2).

## 6. Cách chạy

1. Focus Unity, chờ compile xong.
2. **Hero Escape > Milestone 01 > Dựng scene chơi thử** → **Dựng lại** (thêm nút Stasis, nhãn Frozen, Level 03 vào danh sách).
3. Play → thắng Level 01 → Next Level → thắng Level 02 → Next Level → Level 03.

## 7. Hạn chế

- Scene phải dựng lại. Nếu chưa dựng lại thì Level 03, nút Stasis và nhãn Frozen không có.
- `TargetAlreadyLocked` có trong Core nhưng không thể gặp với khóa 1 pha hiện tại (khóa hết trước lượt Hero kế tiếp, và cùng lượt thì `SupportAlreadyUsedThisTurn` chặn trước).
- Level 03 khá tuyến tính (51 trạng thái đạt tới); lời giải gần như duy nhất.
- Muốn qua Level 03 phải thắng lần lượt Level 01 và 02 (không có menu chọn màn, theo đúng phạm vi).
