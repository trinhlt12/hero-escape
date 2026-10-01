# Milestone 06 — Tactical AI và Level 05

Ngày: 2026-10-01 • Trạng thái: **Hoàn thành.** Game Designer đã nghiệm thu Level 05 và Tactical AI (báo cáo 2026-10-01). Bảng mục 4 giữ nguyên kết quả do Claude ghi lúc bàn giao.

Level 01–04 và highlight/hover preview đã được Game Designer playtest thành công trước task này. Dữ liệu và Basic AI của Level 01–04 giữ nguyên. Tương tác giữ nguyên theo D8: highlight luôn hiện, hover dự đoán, click thực hiện, Dash chọn ô đích; không Undo/Confirm/Cancel.

## 1. Phạm vi

- Profile **Tactical** cho X01 (GDD mục 5, TDD mục 9), là dữ liệu cấu hình theo level: `ai: tactical`. Màn không khai báo thì là `basic`.
- **Level 05** (`M05_TacticalLevel.txt`): 6x6, Hero H01, một X01 Tactical, Dash 2, Stasis 1. Tiêu đề `Hero Escape · Level 05 · Tactical`.
- Tactical được tích hợp vào gameplay, preview và công cụ kiểm chứng; tất cả dùng cùng `Simulation`, ngân sách và fallback.
- Out-of-scope: art, quái, thẻ, passive mới; Undo; API ngoài/LLM; học từ người chơi; build Android; generator/solver sản phẩm.

## 2. Tactical AI (`Scripts/Search/TacticalChasePolicy.cs`)

**Thông tin dùng:** chỉ `GameState` công khai (bàn, vị trí, Dash/Stasis còn lại, khóa, luật). Không đọc con trỏ, hover hay input chưa thực hiện. Không ngẫu nhiên, không thời gian máy. Cùng trạng thái và cấu hình luôn cho cùng quyết định.

**Độ sâu** (cho X đang không bị khóa; X bị khóa thì bỏ lượt và không gọi Tactical):

1. Ứng viên: các bước hợp lệ theo N, NE, E, SE, S, SW, W, NW (đủ luật di chuyển và chặn góc), Wait cuối cùng.
2. Có bước bắt Hero thì chọn ngay, không tìm kiếm.
3. Với mỗi ứng viên:
   - X thực hiện; các X sau trong pha hiện tại chạy Basic (`Simulation.ResolveRemainingEnemyPhase`).
   - Rồi xét **mọi** phản ứng hợp lệ của Hero: không dùng thẻ, hoặc Stasis lên từng X hợp lệ; sau đó 8 Move + Wait + 8 Dash nếu còn Dash.
4. Sau mỗi phản ứng: Hero thắng thì dừng nhánh. Ngược lại mô phỏng **một pha địch Basic** (Stasis vẫn khiến X bỏ pha). Không gọi Tactical đệ quy. Dừng đánh giá.

**Chọn nước** (so sánh từ điển, không cộng trọng số):

1. Bắt ngay.
2. Không để Hero thắng ở phản ứng tiếp theo (nếu còn ứng viên làm được).
3. Ít **phản ứng an toàn** của Hero hơn. An toàn = thắng, hoặc còn sống sau pha Basic. Đếm theo trạng thái kết quả khác nhau: gộp kết quả trùng hoàn toàn, nhưng không gộp khi tài nguyên hoặc khóa khác.
4. Khoảng cách BFS X→Hero nhỏ hơn, giả định Hero chọn phản ứng an toàn xa nhất. Không có đường = vô hạn; không còn phản ứng an toàn thì hòa.
5. Thứ tự cố định N…NW, Wait.

**Ngân sách:** 20.000 chuyển trạng thái cho một quyết định (`TacticalChasePolicy.DefaultTransitionBudget`).
- Một chuyển trạng thái = một lần gọi `ResolveRemainingEnemyPhase`, `ApplyStasis` hoặc `Resolve` trong tìm kiếm, kể cả lần bị từ chối. BFS đo khoảng cách không tính.
- Hết ngân sách: bỏ kết quả dở dang, chọn bằng Basic từ trạng thái gốc của quyết định.
- `GameController` ghi `Debug.LogWarning` chỉ khi fallback xảy ra trong **nước đi thật**; preview khi hover không ghi Console.

Đây là heuristic nhìn trước một vòng, **không phải AI bất bại**.

### Hành vi quan sát được (cần Game Designer đánh giá khi playtest)

Vì các phản ứng có Stasis được đếm riêng (theo đúng quy tắc không gộp khi tài nguyên/khóa khác), khi Hero còn thẻ Stasis thì Tactical thường **đứng ngoài tầm 3 hoặc nấp sau góc tường chắn đường nhìn**, thay vì áp sát.

Ví dụ ở Level 05: Hero (3,0), X01 (0,1). Tiến xuống S thì X01 lọt vào tầm Stasis, số phản ứng an toàn của Hero tăng từ 4 lên 8, nên Tactical chọn Wait. Trên cả chuỗi thắng tham chiếu, Stasis không lúc nào hợp lệ. Nước đi có thể trông thụ động; đây là hệ quả trực tiếp của tiêu chí 3.

## 3. Level 05 (`M05_TACTICAL`)

Gốc (0,0) ở góc dưới trái; x tăng sang phải, y tăng lên trên; N = y + 1.

```
y=5   .  .  .  .  .  E
y=4   #  #  #  #  .  .
y=3   .  .  .  #  .  .
y=2   .  X  .  .  .  .
y=1   .  #  #  .  #  #
y=0   H  .  .  .  .  .
     x=0 1  2  3  4  5
```

- Hero (0,0) • X01 (1,2), Tactical • Cửa (5,5) • Dash 2 • Stasis 1.
- Tường: (0,4), (1,4), (2,4), (3,4), (3,3), (1,1), (2,1), (4,1), (5,1).

Chuỗi thắng tham chiếu, phát lại với Tactical thật, 0 fallback:
`DASH-E DASH-W E E E N N E N N NE`

| Lượt | Hero | X01 |
|---|---|---|
| 1 DASH-E | (2,0) | (1,3) |
| 2 DASH-W | (0,0) | (1,2) |
| 3 E | (1,0) | (0,2) |
| 4 E | (2,0) | (0,1) |
| 5 E | (3,0) | (0,1), Wait |
| 6 N | (3,1) | (0,2) |
| 7 N | (3,2) | (0,1) |
| 8 E | (4,2) | (0,2) |
| 9 N | (4,3) | (1,3) |
| 10 N | (4,4) | (2,3) |
| 11 NE | (5,5) Cửa → **You Win!** | — |

Hai lần Dash đầu dùng để dụ X01 lên rồi quay lại; sau đó X01 giữ khoảng cách nên Hero đi vòng qua cột (3,·) và (4,·).

**Tactical khác Basic** (Tactical thật, không fallback). Sau nước đầu `N`: Hero (0,1), X01 (1,2). X01 không bắt được vì Tường (1,1) chặn góc.

| | Basic | Tactical |
|---|---|---|
| Chọn | **W** → (0,2), ngay phía trên Hero | **Wait** (đứng sau góc tường) |
| Phản ứng an toàn của Hero | 3: S; Stasis+S; Stasis+Wait | **2**: S; Wait |
| Có Stasis được không | Có (X01 kề, đường nhìn thẳng) | Không (Tường (1,1) chắn đường nhìn) |
| Hero thắng ngay | Không | Không |

Các ứng viên khác của Tactical tại đó: N 6, NE 4, E 4, NW 5 phản ứng an toàn. Tactical chọn Wait vì ít phản ứng an toàn nhất (tiêu chí 3).

Toàn màn: 248 điểm quyết định Tactical khác Basic, 192 trong đó do giảm phản ứng an toàn. **Không tìm thấy** trường hợp "Basic để lọt nước thắng mà Tactical chặn" trên Level 05 hay các bố cục đã thử. Lý do: đường BFS ngắn nhất của Basic gần Cửa thường đi qua đúng ô chặn.

**Tactical làm màn khó hơn:** cùng bố cục, nếu X01 dùng Basic thì đi bộ 9 lượt là thắng (`E E E N N E N N NE`). Với Tactical, chỉ Move/Wait thì **vô nghiệm** (duyệt hết 47 trạng thái).

## 4. Kết quả kiểm tra

Quy ước: Passed • Failed • Not run • Unknown.

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M06-C1 | Compile Core, Search, Unity, Editor và toàn bộ test | Passed (ngoài Unity) | Roslyn + DLL Unity 6000.3.8f1; C# 9. |
| M06-T1 | 186 test EditMode không cần UnityEngine | Passed (ngoài Unity) | 186 passed, 0 failed (166 cũ + 20 mới: `TacticalTests` 8, `Level05Tests` 12). |
| M06-T2 | `UnityBoardViewTests` và Test Runner trong Unity | Not run | |
| M06-V1 | Level 05 có lời giải với Tactical thật (Dash 2, Stasis 1) | Passed | BFS: Solved, 194 trạng thái; lời giải phát lại thắng. Không dùng Basic để kết luận. |
| M06-V2 | Fallback trên lời giải đã kiểm chứng | Passed | 0 lần (test `DefaultBudget_IsEnough_...`). |
| M06-V3 | Không thắng ngay ở lượt đầu | Passed | |
| M06-V4 | Tactical khác Basic vì lợi ích chiến thuật | Passed | Ví dụ mục 3; test `TacticalDiffersFromBasic_...`. |
| M06-R1 | Basic Level 01–04 không đổi | Passed | Profile Basic; các test hồi quy theo từng lượt vẫn qua; số liệu duyệt trạng thái không đổi. |
| M06-S1 | Dựng lại scene | Not run | |
| M06-P1 | Play Mode Level 05 | Not run | Chưa playtest. |

**Hiệu năng** (đo bằng công cụ tạm trên .NET 6, máy phát triển, ngoài Unity):
- 1.095 điểm quyết định đạt tới được trên Level 05: 198 lần bắt ngay, 897 lần đánh giá, **0 fallback**.
- Chuyển trạng thái mỗi quyết định: trung bình **68**, tối đa **216** (ngân sách 20.000).
- Thời gian trung bình **0,106 ms** mỗi quyết định (gồm cả lần bắt ngay).
- Toàn bộ phân tích Level 05 (duyệt trạng thái + so sánh) mất khoảng 3,6 giây.
- **Chưa đo** trong Unity Editor (Mono) hay trên Android; không khẳng định đạt mục tiêu preview p95 < 100 ms của TDD trên thiết bị.

Test mới:
- `TacticalTests`: profile mặc định Basic, Level 01–04 là Basic; bắt ngay không tìm kiếm; phản ứng có Dash và Stasis chỉ khi còn tài nguyên; Hero chỉ thắng bằng Dash vẫn được thấy, nhánh thắng dừng ngay; X bị khóa không gọi Tactical; cùng trạng thái cho cùng quyết định; hết ngân sách fallback Basic từ trạng thái gốc; ngân sách mặc định đủ cho lời giải (0 fallback).
- `Level05Tests`:
  - bố cục và cấu hình;
  - lời giải thắng với Tactical thật;
  - BFS Solved và phát lại;
  - Tactical làm đi bộ vô nghiệm trong khi Basic thì không;
  - không thắng ngay;
  - Tactical khác Basic, có số liệu;
  - preview khớp thực thi Move/Dash/Wait từ đầu, sau Stasis, và khi fallback (ngân sách 5);
  - hover không đổi trạng thái hay quyết định;
  - Restart không dùng preview cũ;
  - thắng bằng click ô highlight.

## 5. Cách chạy

1. Focus Unity, chờ compile xong.
2. **Hero Escape > Milestone 01 > Dựng scene chơi thử** → **Dựng lại** (thêm Level 05 vào danh sách màn).
3. Play → thắng Level 01 → … → Level 04 → Next Level → Level 05.

## 6. Hạn chế

- Tactical ưu tiên ít phản ứng an toàn nên có xu hướng tránh tầm Stasis; có thể trông thụ động (mục 2).
- Chưa có ví dụ "ngăn nước thắng mà Basic để lọt" trên Level 05.
- Chưa đo hiệu năng trong Unity hay trên thiết bị.
- Pha địch vẫn hiện kết quả sau một nhịp dừng; không có animation riêng cho suy nghĩ của X.
- Lời giải tham chiếu dài 11 lượt, có nước dụ (DASH-E rồi DASH-W); độ hay và độ khó cần playtest.
