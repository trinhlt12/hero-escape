# Milestone 02 — Level 02 với Dash, UI tiếng Anh

Ngày: 2026-10-01 • Trạng thái: **Đã có code, level và test; chờ dựng lại scene và playtest trong Editor**

Tài liệu này khóa phạm vi task Milestone 02. Không tự triển khai các phần khác của prototype.

## 1. Phạm vi

### In-scope

- **Level 01 giữ nguyên gameplay:** cùng bố cục, vị trí xuất phát, Cửa, Tường, AI; không có Dash. File `M01_TestLevel.txt` **không bị sửa**. Chỉ đổi text sang tiếng Anh và thêm điều hướng màn.
- **Level 02 mới** (`M02_DashLevel.txt`): bàn 5x5, một Hero H01, một X01 (AI Basic), một Cửa, Dash tối đa 2 lần mỗi lần chơi. Không có passive.
- **Luật Dash** trong Core (C# thuần), cấu hình số lần theo từng level (`dash: N` trong file level).
- **UI Dash:** nút `Dash (n/2)`, chế độ chọn hướng, hủy bằng nút Dash hoặc Escape.
- **Điều hướng:** `Level 01` / `Level 02`, nút `Next Level` sau khi thắng Level 01, nút `Back to Level 01` ở Level 02.
- **Toàn bộ text trong game bằng tiếng Anh**, gom trong `UiText`.

### Out-of-scope

Passive, Hero thứ hai, X02, nhiều X, Boss • thẻ phụ trợ, trạng thái bất lợi, tường tạm • Tactical AI, Undo, replay, preview • generator, 12 màn, solver sản phẩm • menu chọn level, lưu tiến trình • âm thanh, VFX, backend, build Android • art mới • localization.

## 2. Luật Dash đã triển khai (`Simulation.Resolve`, `HeroActionKind.Dash`)

- Dash là hành động chính, thay cho Move hoặc Wait. Đi thẳng đúng 2 bước, mỗi bước 1 ô, cùng một hướng trong 8 hướng.
- Mỗi bước dùng chung `GridRules.CheckStep` với Move: biên, Tường, ô X01, chặn góc chéo (Tường và X01 chặn góc). Không nhảy qua hoặc đáp lên X01, không xuyên Tường.
- **Cửa:** bước 1 tới Cửa thì thắng tại bước 1, không kiểm tra bước 2. Bước 2 tới Cửa thì thắng tại bước 2. Thắng thì không chạy X01.
- **Toàn vẹn:** kiểm tra trọn đường trên tọa độ trước khi đổi trạng thái. Có bước sai (và bước 1 không phải Cửa) thì từ chối cả Dash: Hero đứng yên, không trừ lượt dùng, không tăng vòng, không chạy X01. `TurnResult.FailedDashStep` cho biết bước bị chặn (1 hoặc 2).
- **Dash hợp lệ:** trừ đúng 1 lượt dùng, sau đó chạy đúng 1 lượt X01 (nếu chưa thắng). X01 không đi giữa hai bước.
- Từ chối khi màn không có Dash (`DashNotAvailable`), hết lượt (`NoDashUsesLeft`) hoặc màn đã kết thúc (`GameOver`).
- `GameState.DashUsesLeft` nằm trong trạng thái; Restart khôi phục về `LevelData.DashMaxUses`.

## 3. Input và UI

- `TurnInputController` (C# thuần, không dùng UnityEngine) giữ chế độ chọn hướng và gửi hành động tới `GameSession`. UI chỉ đọc trạng thái và gửi thao tác.
- Bấm **Dash** → vào chế độ chọn hướng, hiện `Choose an adjacent tile to dash in that direction.`, nút Dash đổi màu vàng.
  - Bấm một trong 8 ô kề Hero: ô đó **chỉ xác định hướng**; đích cuối cách Hero 2 ô.
  - Bấm ô xa, ô Hero hoặc ngoài bàn: giữ chế độ chọn, hiện phản hồi, không mất lượt dùng.
  - Hướng bị chặn: giữ chế độ chọn, hiện lý do và bước bị chặn (ví dụ `Can't dash that way: a wall blocks step 2.`).
  - Bấm Dash lần nữa hoặc **Escape**: hủy (`Dash cancelled.`), không mất lượt.
  - Trong chế độ chọn hướng, **Wait bị vô hiệu hóa**.
  - Dash thành công, Restart, chuyển màn hoặc kết thúc màn đều xóa chế độ chọn hướng.
- Nút Dash: chỉ hiện ở màn có Dash (Level 02), bị vô hiệu hóa khi hết lượt dùng hoặc khi trận đã kết thúc.
- Lần bấm trên UI không tác động bàn cờ: `BoardInput` raycast UI và gửi cờ `pointerOverUi`; `TurnInputController.PressBoard` bỏ qua khi cờ bật.

## 4. Điều hướng

- Danh sách màn nằm trong `GameController.levels` (scene builder điền sẵn): `Level 01` → `M01_TestLevel.txt`, `Level 02` → `M02_DashLevel.txt`. Dùng chung một scene và một `GameController`.
- Tiêu đề hiển thị `Hero Escape · Level 01` / `Hero Escape · Level 02`.
- `Next Level` chỉ hiện khi đã thắng Level 01. `Back to Level 01` luôn hiện ở Level 02.
- Vào màn luôn tạo `GameSession` và `TurnInputController` mới, hủy coroutine của lượt trước. Callback cũ bị chặn bằng kiểm tra session và `Revision`.
- Restart chỉ khởi động lại màn đang chơi; ở Level 02 khôi phục đủ 2 Dash.

## 5. Level 02 (`M02_DASH`)

Gốc (0,0) ở góc dưới trái; x tăng sang phải, y tăng lên trên; N = y + 1.

```
y=4   .  .  .  .  E
y=3   .  #  #  #  .
y=2   .  .  X  #  .
y=1   #  .  .  #  .
y=0   H  .  .  .  .
     x=0 1  2  3  4
```

- Hero (0,0) • X01 (2,2) • Cửa (4,4) • Dash: 2.
- Tường: (1,3), (2,3), (3,3), (3,2), (0,1), (3,1).

Chuỗi thắng tham chiếu (trong file, 1 Dash): **E, DASH-E, E, N, N, N, N**

| Lượt | Hành động | Hero | X01 sau lượt | Dash còn |
|---|---|---|---|---|
| 1 | E | (1,0) | (2,1) | 2 |
| 2 | **DASH-E** (bấm ô (2,0)) | (3,0) | (2,0) | 1 |
| 3 | E | (4,0) | (3,0) | 1 |
| 4 | N | (4,1) | (4,0) | 1 |
| 5 | N | (4,2) | (4,1) | 1 |
| 6 | N | (4,3) | (4,2) | 1 |
| 7 | N | (4,4) Cửa → **You Win!** | không đi | 1 |

Lời giải ngắn nhất dùng 2 Dash (6 lượt): E, **DASH-E**, E, N, N, **DASH-N**.

Dash sai bị bắt ngay:
- **WAIT, DASH-E:** sau Wait, X01 xuống (2,1). Dash E đưa Hero tới (2,0), ngay dưới X01, và X01 bắt Hero trong lượt địch kế tiếp. Dash hợp lệ, trừ 1 lượt, kết quả `You Lose!`.
- **E, DASH-N:** Hero (1,0) Dash lên (1,2); X01 ở (2,1) bắt chéo.

## 6. Kết quả kiểm tra

Quy ước: Passed = đã chạy và đạt. Failed = đã chạy và không đạt. Not run = chưa chạy. Unknown = chưa kết luận được.

| # | Kiểm tra | Trạng thái | Bằng chứng / ghi chú |
|---|---|---|---|
| M02-C1 | Compile Core, Search, Unity, Editor | Passed (ngoài Unity) | Roslyn đi kèm Unity 6000.3.8f1, tham chiếu DLL Unity 6000.3.8f1, `UnityEngine.UI`, `Unity.InputSystem`; .NET Standard 2.1. Chỉ có cảnh báo CS0649 cho field `[SerializeField]`. |
| M02-C2 | Unity Editor compile bản này | Not run | Editor đang mở project; sẽ compile khi focus cửa sổ Unity. |
| M02-T1 | 75 test EditMode (33 cũ + 42 mới) | Passed (ngoài Unity) | Bộ chạy reflection trên .NET 6 + `nunit.framework.dll` của Unity: 75 passed, 0 failed. Lần chạy đầu bị Smart App Control chặn nạp DLL test (`0x800711C7`), build lại thì chạy được; không lách chính sách. |
| M02-T2 | Test Runner trong Unity | Not run | Cần chạy trong Editor. |
| M02-V1 | Level 02 có lời giải với tối đa 2 Dash | Passed | Duyệt BFS: Solved, 18 trạng thái; lời giải ngắn nhất 6 lượt (2 Dash). Test `Solvable_WithAtMostTwoDashes`. |
| M02-V2 | Level 02 có lời giải chỉ với 1 Dash | Passed | Solved, 10 trạng thái. Test `Solvable_WithOneDash_SoUsingBothIsNotRequired`. |
| M02-V3 | Level 02 **không** có lời giải khi chỉ Move/Wait | Passed (duyệt hết) | Unsolvable sau khi duyệt hết **4** trạng thái (Hero, X01, Dash còn lại) đạt tới được, giới hạn 100.000. Không phải Unknown. Test `Unsolvable_WithOnlyMoveAndWait_ExhaustiveSearch`. |
| M02-V4 | Không Dash từ xuất phát vào Cửa thắng ngay | Passed | 7/8 hướng không hợp lệ, DASH-E hợp lệ nhưng chưa thắng. Test `NoDashFromStart_WinsImmediately`. |
| M02-V5 | Có Dash hợp lệ dẫn tới bị bắt ngay | Passed | WAIT, DASH-E → Lost. Test `TrapDash_WaitThenDashEast_IsValidButCaughtImmediately`. |
| M02-R1 | Level 01 giữ nguyên hành vi, không Dash được | Passed | Vị trí Hero/X01 sau từng lượt của E E E NE N N N trùng với Milestone 01; 8 hướng Dash đều `DashNotAvailable`. |
| M02-S1 | Menu dựng scene tạo UI mới | Not run | Cần chạy menu trong Editor. |
| M02-P1 | Play Mode Level 01 và Level 02, Dash, điều hướng, Escape | Not run | Logic đã có test; nối UI/input chưa chạy trong Editor. Chưa playtest. |
| M02-P2 | Bố cục 9:16 và 9:20 với panel dưới cao hơn (540) | Not run | Đã tính: ở 9:16, bàn cần ~1080 px, vùng trống 1080 px (vừa khít, camera tự co). |

Danh sách test mới:
- `DashTests` (18): thẳng/chéo; bước 1 hoặc 2 bị Tường; bước 2 ra ngoài bàn; chặn góc ở bước 1, bước 2, X01 chặn góc; không nhảy qua/đáp lên X01; Cửa ở bước 1 (bước 2 ngoài bàn hoặc Tường); Cửa ở bước 2; hết lượt; màn không có Dash; sau khi kết thúc; Restart khôi phục Dash; đọc `dash:` và `DASH-…`.
- `TurnInputControllerTests` (15): bật/tắt, Escape, Wait bị khóa, click UI không tác động bàn (cả hai chế độ), ô xa/ô Hero/ngoài bàn, hướng bị chặn giữ chế độ, ô bấm chỉ xác định hướng, Move thường, hết lượt, màn không Dash, Restart, vào màn mới, Busy, kết thúc trận.
- `Level02Tests` (7) và `Level01RegressionTests` (2).

Công cụ kiểm chứng: `Tests/EditMode/LevelReachability.cs`. Đây là BFS nhỏ chỉ trong assembly test, dùng `Simulation` và `BasicChasePolicy` thật, chống lặp theo khóa (Hero, X01, Dash còn lại), trả `Unknown` khi vượt giới hạn. Không phải solver sản phẩm, không vào build.

## 7. Cách chạy và nghiệm thu

1. Focus Unity, chờ compile xong, Console không có lỗi đỏ.
2. **Hero Escape > Milestone 01 > Dựng scene chơi thử** → chọn **Dựng lại**. Scene hiện có được tạo lại với UI mới (cùng đường dẫn, cùng GUID, vẫn trong Build Settings).
3. Play. Các bước nghiệm thu nằm trong báo cáo bàn giao.

## 8. Hạn chế

- Scene `M01_TestLevel.unity` cũ phải dựng lại; nếu chưa dựng lại, Console báo `No levels assigned`.
- Tên scene và menu vẫn mang chữ "M01"/"Milestone 01" để không đổi GUID và đường dẫn đã có trong Build Settings.
- Chưa có highlight ô/hướng hợp lệ (cần art Overlay, ngoài phạm vi); phản hồi bằng chữ.
- Level 02 khá tuyến tính (10–18 trạng thái đạt tới): đủ để dạy Dash, chưa phải màn khó.
- Tooltip Inspector và log Console vẫn tiếng Việt (công cụ designer, không hiện trong game).
