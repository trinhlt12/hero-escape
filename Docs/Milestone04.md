# Milestone 04 — Nhiều kẻ địch và Level 04 (hai X01)

Ngày: 2026-10-01 • Trạng thái: **Đã có code, level và test; chờ dựng lại scene và playtest trong Editor**

Khóa phạm vi task nhiều X + Level 04. Level 01–03 đã được Game Designer playtest thành công (báo cáo 2026-10-01). Dữ liệu, bố cục, tài nguyên và hành vi của ba màn này giữ nguyên.

## 1. Phạm vi

### In-scope

- Mô hình nhiều X01 trong Core:
  - Danh sách `EnemyState` gồm ID ổn định, `SpawnIndex`, vị trí, thời hạn khóa riêng.
  - Pha địch xử lý từng X theo `SpawnIndex`.
  - Stasis chọn mục tiêu theo ID.
- **Level 04** (`M04_TwoEnemiesLevel.txt`): 6x6, Hero H01, hai X01 Basic AI, Dash 2, Stasis 1.
- Hiển thị: nhãn số `1`/`2` gắn theo từng X (chỉ ở màn nhiều X), nhãn `Frozen` riêng từng X, hướng dẫn `Enemies act in order: 1, then 2.`
- Điều hướng: `Next Level` sau khi thắng Level 03; `Back to Level 03` ở Level 04.

### Out-of-scope

X02, Boss, AI mới hoặc phối hợp nhóm • Hero, passive, thẻ khác • preview, Undo, replay • art, âm thanh, package mới • build Android, backend • menu chọn màn, lưu tiến trình • generator, solver sản phẩm.

## 2. Mô hình dữ liệu

- `EnemySpawn` (level): `Id`, `SpawnIndex`, `Cell`. `EnemyState` (trận): `Id`, `SpawnIndex`, `Pos`, `LockPhases`.
- `LevelData.Enemies` và `GameState.Enemies` luôn sắp theo `SpawnIndex`. ID và thứ tự không đổi khi X di chuyển; không phụ thuộc thứ tự GameObject, Dictionary hay vị trí.
- **Định dạng file level:**
  - `X` (định dạng cũ) = một X01, ID `X1`, `SpawnIndex` 1, nên Level 01–03 nạp như trước mà không sửa file.
  - `1`, `2`, `3` = X01 có ID `X1`/`X2`/`X3` hành động theo thứ tự đó.
  - Lời giải nhiều X ghi rõ mục tiêu: `STASIS(X2)+DASH-N`. Viết tắt `STASIS+` chỉ hợp lệ khi màn có một X.
- **Kiểm tra dữ liệu** (`LevelData`), báo lỗi rõ, không tự sửa:
  - 1–3 X01; không trùng ID, `SpawnIndex` hoặc ô.
  - X01 không đứng trên Tường/Cửa, không chồng Hero.
  - Mục tiêu Stasis trong lời giải phải tồn tại.
- Lối tắt cho màn một X (`EnemyPos`, `EnemyLocked`, `EnemyStart`, `CheckStep(..., otherActor)`, `BasicChasePolicy.TryChooseStep(state, out dir)`) trỏ tới X đầu tiên. Nhờ vậy test và code Level 01–03 giữ nguyên.

## 3. Luật đã triển khai

**Pha địch** (`Simulation.ResolveEnemyPhase`):
- Hero tới Cửa: thắng ngay, không X nào hành động.
- Ngược lại, xử lý X theo `SpawnIndex` tăng dần, mỗi X đúng một lần:
  - Đang khóa: bỏ qua và giảm khóa của riêng X đó đúng 1.
  - Không khóa: `BasicChasePolicy` tính BFS **từ trạng thái hiện tại**, tức là sau khi X trước đã đi, rồi đi tối đa 1 ô.
  - Cập nhật trạng thái ngay sau mỗi X. X vào ô Hero thì thua ngay và dừng cả pha (X sau không được xử lý).

**BFS:**
- Các X khác (kể cả X đang khóa) là vật cản tại vị trí hiện tại; ô của chính X đang tìm đường thì không.
- Ô Hero là đích; Cửa bị chặn đối với X.
- Không lập kế hoạch xuyên qua X khác; không có đường thì đứng yên.

**Va chạm:**
- `GridRules.CheckStep(..., heroPos, enemyCells)` dùng chung cho Hero, X và BFS.
- Không chồng, không xuyên, không đổi chỗ với X khác.
- Mọi nhân vật khác (và Cửa đối với X) chặn góc chéo.
- Move và từng bước Dash của Hero kiểm tra tất cả X, kể cả X đang khóa. Toàn vẹn Dash giữ nguyên.

**Stasis** (`Simulation.ApplyStasis(state, enemyId)`):
- Khóa đúng X có ID đó. Giữ nguyên các điều kiện cũ: tầm 3, đường nhìn supercover, một lần mỗi màn, trước hành động chính, không lên X đang khóa.
- Overload theo ô vẫn còn, khóa X đang đứng ở ô đó; ô trống thì báo `NoEnemyAtTarget`, không mặc định X đầu tiên hay X gần nhất.

## 4. Input và hiển thị

- `TurnInputController`: trong chế độ chọn Stasis, bấm ô có X nào thì gửi **ID của X đó** vào Core; ô trống thì phản hồi và giữ chế độ chọn.
- `BoardView`: một sprite `enemy_x01` cho mỗi X, theo thứ tự `SpawnIndex`.
- `EnemyStatusLabel`:
  - Nhân bản từ hai mẫu nhãn khi nạp màn: số thứ tự (vàng, góc trên trái ô của X) và `Frozen` (phía trên X).
  - Nhãn bám theo đúng X; không nhận raycast; nằm dưới các panel UI.
  - Số thứ tự chỉ hiện khi màn có nhiều X.
- Hướng dẫn Level 04: `Enemies act in order: 1, then 2.` / `Stasis freezes only the enemy you click. Dash moves 2 tiles in a straight line.`
- Pha địch vẫn dùng một nhịp dừng 0,25 giây rồi hiện trạng thái cuối. Hai X hiện vị trí mới cùng lúc; logic vẫn tuần tự 1 rồi 2. Input bị chặn trong suốt pha địch.
- Level 01–03 giữ UI như trước: không có số thứ tự, hướng dẫn và nút giữ nguyên.

## 5. Level 04 (`M04_TWO_ENEMIES`)

Gốc (0,0) ở góc dưới trái; x tăng sang phải, y tăng lên trên; N = y + 1.

```
y=5   2  .  .  .  .  E
y=4   .  #  #  .  #  .
y=3   .  .  .  .  .  .
y=2   .  #  .  #  #  .
y=1   .  .  .  .  1  .
y=0   H  .  .  .  .  .
     x=0 1  2  3  4  5
```

- Hero (0,0) • X1 (4,1), hành động trước • X2 (0,5), hành động sau • Cửa (5,5).
- Tường: (1,4), (2,4), (4,4), (1,2), (3,2), (4,2) • Dash 2 • Stasis 1.

Lời giải tham chiếu: `N STASIS(X2)+DASH-N E E E N N DASH-E`

| Lượt | Phụ trợ | Hành động | Hero | X1 | X2 |
|---|---|---|---|---|---|
| 1 | — | N | (0,1) | (3,0) | (0,4) |
| 2 | **Stasis → X2** | **DASH-N** (bấm ô (0,2)) | (0,3) | (2,0) | (0,4), bị khóa, bỏ lượt |
| 3 | — | E | (1,3) | (2,1) | (0,3) |
| 4 | — | E | (2,3) | (2,2) | (1,3) |
| 5 | — | E | (3,3) | (2,3) | (0,3) |
| 6 | — | N | (3,4) | (3,3) | (0,4) |
| 7 | — | N | (3,5) | (3,4) | (0,5) |
| 8 | — | **DASH-E** | (5,5) Cửa → **You Win!** | — | — |

**Chọn mục tiêu Stasis**, sau nước 1 (`N`): Hero (0,1), X1 (3,0), X2 (0,4); cả hai cách Hero 3 ô và có đường nhìn.

| Cùng hành động | Khóa X1 | Khóa X2 |
|---|---|---|
| **DASH-N** | **Bị bắt:** X2 đi (0,4)→(0,3) | **Sống**, còn thắng (chuỗi trên) |
| **DASH-E** | **Sống**, còn thắng: `N STASIS(X1)+DASH-E DASH-N E N N E E` | **Bị bắt:** X1 đi (3,0)→(2,1); X2 không được xử lý |

Ghi chú: màn vẫn có lời giải chỉ dùng Dash (`DASH-E DASH-N N E N N E E`); Stasis không bắt buộc trong mọi lời giải (đúng yêu cầu). Không Dash thì vô nghiệm.

## 6. Kết quả kiểm tra

Quy ước: Passed • Failed • Not run • Unknown.

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M04-C1 | Compile Core, Search, Unity, Editor | Passed (ngoài Unity) | Roslyn của Unity 6000.3.8f1 + DLL Unity; C# 9. Chỉ cảnh báo CS0649. |
| M04-C2 | Unity Editor compile bản này | Not run | Editor đang mở project. |
| M04-T1 | 142 test EditMode | Passed (ngoài Unity) | 142 passed, 0 failed (114 cũ + 28 mới). Smart App Control chặn chập chờn DLL test mới build; build lại thì chạy được, không lách chính sách. |
| M04-T2 | Test Runner trong Unity | Not run | |
| M04-V1 | Level 04 có lời giải (Dash 2, Stasis 1) | Passed | BFS: Solved, 197 trạng thái; lời giải phát lại bằng Simulation thắng. |
| M04-V2 | Không thắng ngay ở lượt đầu | Passed | Mọi Move/Wait/Dash, có hoặc không có Stasis lên X1/X2. |
| M04-V3 | Cả hai X tham gia đuổi | Passed | Lúc đầu X1 có đường BFS 4 bước, X2 5 bước; cả hai di chuyển trong lời giải. |
| M04-V4 | Trạng thái còn Stasis, cả hai X là mục tiêu hợp lệ, và lựa chọn tạo khác biệt | Passed | Sau nước `N`: cùng DASH-N (hoặc DASH-E), một mục tiêu thì sống và còn thắng, mục tiêu kia thì bị bắt ngay. Có 21 trạng thái mà cả hai X là mục tiêu hợp lệ. |
| M04-V5 | Không Dash thì vô nghiệm | Passed (duyệt hết) | 36 trạng thái (Stasis ≤ 1). |
| M04-R1 | Level 01–03 giữ kết quả | Passed | Vị trí sau từng lượt của các chuỗi tham chiếu trùng với trước; số liệu duyệt trạng thái của Level 01–03 không đổi. |
| M04-S1 | Menu dựng lại scene | Not run | |
| M04-P1 | Play Mode Level 04, nhãn 1/2, Frozen theo từng X | Not run | Chưa playtest. |
| M04-P2 | Bàn 6x6 ở 9:16 và 9:20 | Not run | Đã tính: ở 9:16, bàn 6x6 cần khoảng 997 px chiều cao (6,5 ô × 166 px kể cả lề), vùng trống 1080 px. |

Công cụ kiểm chứng `Tests/EditMode/LevelReachability.cs`:
- Mỗi cạnh là một lượt: không dùng thẻ, hoặc Stasis lên từng X hợp lệ theo ID, rồi một hành động chính.
- Khóa trạng thái: Hero; vị trí và khóa từng X theo thứ tự `SpawnIndex`, nên hai X hoán đổi vị trí là hai trạng thái khác nhau; Dash và Stasis còn; cờ đã dùng phụ trợ.
- Trả `Unknown` khi chạm giới hạn; lời giải được phát lại bằng `Replay`.
- Không phải solver sản phẩm.

Test mới:
- `MultiEnemyTests` (16): thứ tự theo `SpawnIndex` (không theo vị trí trong lưới); X2 tính lại sau X1 và vào ô X1 vừa rời; X2 không lập kế hoạch xuyên X1; X bị chặn đứng yên còn X kia vẫn đi; X không vào hay lách góc qua X khác; X1 bắt thì X2 không xử lý; X2 bắt thì thua; khóa riêng từng X và hết sau pha bị bỏ qua (cho cả X1 và X2); Stasis theo ID/ô, không mặc định; Hero không Move/Dash xuyên X nào (kể cả khi khóa, bước 1 và 2); tới Cửa thì không X nào đi; Restart; định dạng cũ; dữ liệu sai; mục tiêu Stasis bắt buộc khi nhiều X.
- `Level04Tests` (9).
- `TurnInputControllerTests` (+3): bấm X nào chỉ khóa X đó; bấm ô trống giữ chế độ chọn.

## 7. Cách chạy

1. Focus Unity, chờ compile xong.
2. **Hero Escape > Milestone 01 > Dựng scene chơi thử** → **Dựng lại**. Thêm Level 04 và nhóm nhãn `EnemyLabels`; thay nhãn `FrozenLabel` cũ.
3. Play → thắng Level 01 → 02 → 03 → Next Level → Level 04.

## 8. Hạn chế

- Scene phải dựng lại. Scene cũ có `FrozenLabel` với field cũ nên nhãn Frozen sẽ không hoạt động cho tới khi dựng lại.
- Pha địch hiện vị trí mới của hai X cùng lúc (không có animation từng X); thứ tự 1→2 thể hiện bằng nhãn số và hướng dẫn.
- Thông báo `X01 has no path and stays put.` / `X01 was frozen and skipped its turn.` không nói rõ X nào khi có hai X.
- Chưa playtest; độ hay và cân bằng của Level 04 cần Game Designer đánh giá.
