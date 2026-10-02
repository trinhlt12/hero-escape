# Milestone 09 — Thẻ Barrier và Level 06

Ngày: 2026-10-02 • Trạng thái: **Đã có code, Level 06 và test ngoài Unity; chờ dựng lại gameplay scene, Test Runner và Play Mode/điện thoại**

Tay bài DOTween (Milestone 08) đã được Game Designer nghiệm thu. Milestone này thêm thẻ phụ trợ **Barrier** (vật cản tạm) và **Level 06** để thử hệ thống thẻ.

Không đổi:
- Luật và cấu hình Level 01–05.
- Heuristic, độ sâu, ngân sách Tactical AI.
- Unity, render pipeline, DOTween.

Không thêm art, package hoặc thư viện.

## 1. Phạm vi

- Thẻ Support **Barrier**: trạng thái vật cản tạm trong Simulation; tích hợp di chuyển, Dash, góc chéo, LOS, BFS của X, AI Basic/Tactical, preview, tay bài.
- **Level 06** (6×6, một X01 Basic, Dash 2 / Stasis 1 / Barrier 1), có trong Level Select. Next Level từ Level 05 dẫn tới Level 06. Level 06 là màn cuối.
- Ngoài phạm vi: Lure/thẻ khác, chọn bộ thẻ, rút bài/ngẫu nhiên, tier, Hero/X mới, generator, Undo/Confirm/Cancel, chỉnh Level 01–05, đổi Tactical.

## 2. Luật Barrier (đã triển khai)

| Mục | Luật |
|---|---|
| Loại | Support. Tên hiển thị `Barrier`. Level 06: 1 lần dùng. Màn không khai báo `barrier:` → 0 (không có thẻ, không có vật cản). |
| Mục tiêu hợp lệ | Ô **Sàn trống kề Hero theo 8 hướng**, trong bàn. Không phải ô Hero, X, Tường, Cửa hoặc ô đã có vật cản. Ô chéo phải lách góc được theo **đúng luật bước của Hero** (`GridRules.CheckStep`): Tường, vật cản tạm và X ở một trong hai ô cạnh trực giao đều chặn góc. |
| Hiệu ứng | Đặt vật cản tạm ngay tại ô đó, trên lớp riêng (`GameState.Barriers`). Địa hình gốc không đổi. Vật cản chặn Hero và X đi vào/đi xuyên, chặn Dash (không nhảy qua), chặn lách góc và chặn đường nhìn như Tường. |
| Thời hạn | Có hiệu lực ngay. Tồn tại qua hành động chính kế tiếp và **toàn bộ** pha địch kế tiếp (X sau vẫn tính đường với vật cản). Bị xóa **một lần** ở cuối pha đó, kể cả khi mọi X bị khóa hoặc không có đường. Hover, chọn thẻ, mục tiêu sai, hành động chính bị từ chối đều không làm giảm thời hạn. Nhãn trên bàn là `1` = còn một pha địch. |
| Tiêu hao | Thành công: trừ 1 lần dùng, đánh dấu đã dùng phụ trợ. Không tăng vòng, không chạy pha địch; Hero tiếp tục Move/Wait/Dash. Mục tiêu sai: không đổi gì. Hành động chính sau đó bị từ chối: vật cản vẫn còn, thẻ đã mất, không hoàn tác. |
| Trận kết thúc sớm | Hero tới Cửa: thắng ngay, không chạy pha địch để xóa vật cản. X bắt Hero giữa pha: pha dừng theo luật hiện tại, không xử lý thêm. Restart/đổi màn tạo trạng thái mới (không còn vật cản). |
| Không chặn đặt "nguy hiểm" | Được phép bịt hết đường của X hoặc của Hero. Highlight chỉ biểu thị hợp lệ, không bảo đảm an toàn. |

**Giới hạn phụ trợ mỗi lượt: tối đa một thẻ phụ trợ mỗi lượt Hero, dùng chung cho Stasis và Barrier.**
- GDD mục 4 ("Cho phép dùng tối đa một phụ trợ") và mục 7 ("Mỗi lượt Hero chỉ dùng tối đa một lá ở bước Chuẩn bị") khớp với code (`GameState.SupportUsedThisTurn`, xóa khi sang vòng mới).
- Không có mâu thuẫn tài liệu–code về giới hạn này, nên giữ nguyên.
- Hệ quả: không thể Stasis + Barrier trong cùng một lượt. Vì Barrier hết ở cuối pha địch, Barrier của chính người chơi không bao giờ chắn LOS của Stasis ở lượt sau. Luật "Barrier chặn LOS" vẫn được cài và kiểm tra bằng trạng thái dựng tay.

**Khác biệt với GDD/TDD — cần Game Designer xác nhận:**
- GDD mục 7 (S02 "Vách đá") và TDD mục 7 mô tả mục tiêu là "Sàn trống, **tầm 2, có đường ngắm**".
- Yêu cầu milestone này chốt "ô **kề Hero 8 hướng** + luật góc chéo", và code theo yêu cầu milestone.
- Thời hạn ("qua pha X kế tiếp, xóa ở EnemyPhaseEnd") và việc chặn LOS khớp với GDD/TDD.
- Chưa sửa file `.docx` GDD/TDD và chưa có `rulesVersion` trong code (project chưa triển khai RulesConfig). Đã ghi vào TechnicalDecisions D12.
- Nếu giữ luật mới, cần cập nhật GDD mục 7 / TDD mục 7 cho khớp.

## 3. Core, AI, preview

**Core** (`HeroEscape.Core`, C# thuần):
- `GridRules`: overload mới của `CheckStep(..., barriers)` và `HasLineOfSight(..., barriers)`. Vật cản chặn ô đích (`StepCheck.Barrier`) và chặn góc như Tường. Overload cũ giữ nguyên hành vi (không có vật cản), nên Level 01–05 và test cũ không đổi.
- `GameState`:
  - Thêm `BarrierUsesLeft` và `Barriers` (mảng bất biến, sắp theo Y rồi X), cùng `WithBarrierPlaced`, `WithBarriersCleared`.
  - `Equals`, `GetHashCode`, `ToString` tính cả vị trí vật cản và số lần dùng.
  - Thời hạn không cần lưu riêng: mọi vật cản luôn còn đúng một pha (`GameState.BarrierPhases = 1`).
- `Simulation.ApplyBarrier`:
  - Cùng kiểu kết quả `SupportResult` như Stasis. `SupportResult.Events` mới phát `BarrierPlaced`.
  - Cuối pha địch phát `BarrierExpired` rồi xóa vật cản.
  - Mọi kiểm tra Move/Dash/bước X/LOS của Stasis đều truyền `state.Barriers`.
- `GameSession.ApplyBarrier` / `PreviewBarrier`.
- `ActionError` mới: `Barrier`, `BarrierNotAvailable`, `NoBarrierUsesLeft`, `TargetNotAdjacent`, `TargetIsExit`.

**Dữ liệu level:**
- `barrier: N` (mặc định 0, giới hạn 0–9).
- Bước lời giải `BARRIER-<hướng>+<hành động>` (hướng từ Hero tới ô đặt).
- Cho phép **nhiều dòng `solution:`**: dòng đầu là lời giải tham chiếu, các dòng sau là lời giải thay thế (`LevelData.ReferenceSolutions`).

**AI:**
- Basic: BFS (`EnemyPathfinder`) tính vật cản hiện tại. Không có đường thì đứng yên như luật cũ.
- Tactical:
  - Ứng viên bước của X tôn trọng vật cản.
  - Phản ứng của Hero thêm nhánh **Barrier ở từng ô kề hợp lệ** (khi level có Barrier, còn lượt và chưa dùng phụ trợ), rồi Move/Wait/Dash. Phản ứng được mô phỏng bằng chính `Simulation` Basic nên vật cản hết hạn đúng thời điểm.
  - Mỗi lần gọi `ApplyBarrier` trong tìm kiếm tính 1 chuyển trạng thái (như `ApplyStasis`).
  - Chấm điểm, tie-break, ngân sách 20.000, fallback Basic từ trạng thái gốc và không đệ quy: **không đổi**. Màn không có Barrier đi đúng đường cũ (Level 05 không đổi).
- Công cụ kiểm chứng: project không có replay/solver sản phẩm. Dùng lại `LevelReachability` (BFS nhỏ trong test): khóa trạng thái thêm số Barrier còn lại và vị trí vật cản; lựa chọn phụ trợ thêm Barrier; `Replay` phát lại lượt có Barrier.

**Preview:**
- Chọn thẻ Barrier: highlight (thay sprite sàn bằng `tile_highlight`) đúng các ô `PreviewBarrier` chấp nhận.
- Hover ô hợp lệ trên desktop: ghost vật cản ở ô đó (`PreviewKind.BarrierTarget`). Không đoán hành động chính, không chạy pha địch.
- Sau khi đặt thật: preview Move/Wait/Dash dùng `session.Preview` trên trạng thái có vật cản, nên tính cả chặn đường lẫn thời điểm hết hiệu lực. Preview không commit.

## 4. Tay bài và hình ảnh

- Thẻ `SUPPORT · Barrier`, mô tả "Block an adjacent empty tile until the next enemy phase ends.". Viền cam đất `#E08A3C` để khác viền tím của Stasis; nền giống thẻ phụ trợ khác.
- Thứ tự tay bài: Hero (giữa, 115%) → Stasis (phải) → Barrier (trái), theo quy tắc bố cục M08. Hết lượt thì biến mất và tay bài sắp xếp lại; chọn/đổi/bỏ chọn, chặn input xuyên UI không đổi.
- Vật cản trên bàn:
  - Dùng sprite tường có sẵn, nhuộm `#E08A3C` (`BoardView.barrierColor`). Thứ tự vẽ 11: trên sàn/highlight, dưới ghost và nhân vật.
  - Nhãn vàng `1` (dùng lại mẫu nhãn số của X) đặt giữa ô.
  - DOTween: xuất hiện phóng từ 60% (OutBack), biến mất mờ dần, 0,15 s (`barrierFxDuration`). Tween gắn `SetLink` nên bị hủy cùng bàn cờ.
  - Restart xóa vật cản ngay, không chạy hiệu ứng. Đổi màn dựng lại bàn.
  - Animation không quyết định thời hạn.
- Hint Level 06: "Barrier blocks a tile next to the hero for you and X01 until the enemy turn ends. One support card per turn. …".

## 5. Level 06 — `M06_BARRIER` (`Data/Levels/M06_BarrierLevel.txt`)

Toạ độ `(x, y)`, gốc dưới trái, N = y + 1. X01 Basic, Hero đi trước, không ngẫu nhiên. Dash 2, Stasis 1, Barrier 1.

```
y5  . . # . E .      Tường (2,5); Cửa (4,5)
y4  . # . . . .      Tường (1,4)
y3  . . . . . X      X01 (5,3)
y2  . . . # . .      Tường (3,2)
y1  # . . # . .      Tường (0,1), (3,1)
y0  # . H . # .      Tường (0,0), (4,0); Hero (2,0)
    x=0 1 2 3 4 5
```

**Điều kiện thiết kế** (duyệt hết trạng thái bằng Simulation thật, test `Level06Tests.DesignConditions_*`):

| Thẻ được dùng | Kết quả |
|---|---|
| Không thẻ | Vô nghiệm |
| Chỉ Dash 2 | Vô nghiệm |
| Chỉ Stasis | Vô nghiệm |
| Chỉ Barrier | Vô nghiệm |
| Dash + Barrier | Có nghiệm |
| Dash + Stasis | Có nghiệm |
| Stasis + Barrier (không Dash) | Có nghiệm |

**Lời giải 1 — Barrier + Dash (dụ X01 sang trái, chặn góc):** `N N W NW BARRIER-E+WAIT DASH-E DASH-NE`

| Lượt | Hero | X01 sau pha địch | Ghi chú |
|---|---|---|---|
| 1 N | (2,1) | (4,3) | |
| 2 N | (2,2) | (3,3) | |
| 3 W | (1,2) | (2,3) | dụ X01 sang trái |
| 4 NW | (0,3) | (1,2) | X01 chéo ngay dưới Hero |
| 5 Barrier (1,3) + Wait | (0,3) | (0,2) | Barrier chặn góc (1,2)→(0,3): X01 phải xuống (0,2). Vật cản hết ở cuối pha. |
| 6 Dash E | (2,3) | (1,3) | hàng trên đã thông |
| 7 Dash NE | (4,5) | — | Thắng |

Mục tiêu của thẻ: Barrier ở **(1,3)**, lượt 5, để chặn bước chéo bắt Hero và đổi đường X01.

**Lời giải 2 — Stasis + Dash (lao thẳng lên giữa):** `DASH-N DASH-N STASIS+E NE`

| Lượt | Hero | X01 | Ghi chú |
|---|---|---|---|
| 1 Dash N | (2,2) | (4,3) | |
| 2 Dash N | (2,4) | (3,3) | |
| 3 Stasis X01 + E | (3,4) | (3,3) bỏ lượt | Stasis lên X01 khi nó chặn sát đường lên Cửa (tầm 1, có LOS) |
| 4 NE | (4,5) | — | Thắng |

**Lời giải 3 (bổ sung) — Barrier + Stasis, không Dash:** `N N S W BARRIER-N+WAIT STASIS+N NE NE NE`
- Lượt 5: Barrier ở **(1,2)** chặn góc (2,2)→(1,1) khi X01 áp sát.
- Lượt 6: Stasis để Hero đi lên.
- Sau đó đi bộ chéo tới Cửa.

**Khác nhau về chiến thuật:**
- Lời giải 1 vòng sang **trái**, dùng Barrier đổi đường X01 rồi dùng cả 2 Dash để vượt hàng trên. Không dùng Stasis.
- Lời giải 2 đi thẳng **giữa** ngay từ đầu bằng 2 Dash, dùng Stasis khóa X01 ở nút chặn cuối. Không dùng Barrier.
- Hai đường đi qua phần lớn các ô khác nhau (test kiểm tra số ô chung < một nửa).
- Lời giải 3 không dùng Dash. Không có cặp nào chỉ khác nhau vài nước Wait.

**Barrier có tác dụng thực tế** (test `Barrier_ChangesTheEnemyResponse_AtTheKeyMoment`):
- Lượt 5 của lời giải 1: Hero (0,3), X01 (1,2), cùng hành động **Wait**.
  - Không Barrier: X01 bước chéo vào (0,3), **Hero thua**.
  - Barrier ở (1,3): góc bị chặn, X01 phải đi (0,2), Hero sống và thắng sau 2 lượt.
- Lúc đó có 3 ô đặt hợp lệ (N, E, S):
  - N: không chặn được bước chéo, thua ngay.
  - S: sống qua pha nhưng (với Dash còn lại, không Stasis) hết đường thắng.
  - Chỉ **E** dẫn tới Cửa.
- Thời điểm cũng quan trọng (test `BarrierTiming_Matters_PlacingAtTheStartDoesNotWin`): đặt Barrier ở lượt 1 ở bất kỳ ô nào thì Dash còn lại không đủ để thắng.
- Công cụ tạm (ngoài project), trong các trạng thái đạt được khi chỉ dùng Move/Wait/Dash: chỉ **5 / 317** cặp (trạng thái, ô đặt Barrier hợp lệ) dẫn tới thắng mà không cần Stasis.

Solver tìm ra lời giải không có nghĩa là màn "vui"; phần đó cần Game Designer playtest.

## 6. Điều hướng

- `LevelCatalog.asset` thêm `M06_BARRIER` / `Level 06` / `Barrier` (file level có `.meta` mới). Builder cũng có Level 06 khi tạo catalog từ đầu.
- Level Select tạo nút từ catalog lúc chạy: 6 × 220 + 5 × 28 = 1460 ≤ 1560 đơn vị ở 9:16, **không cần dựng lại** scene LevelSelect.
- Thắng Level 05 → Next Level tới Level 06. Level 06 là màn cuối nên không có Next (`WonLast`). Back to Level 05 và Restart giữ nguyên.

## 7. Cách chạy

1. Focus Unity 6000.3.8f1, chờ compile (Console không lỗi đỏ).
2. **Bắt buộc dựng lại gameplay scene:** **Hero Escape > Milestone 01 > Dựng scene chơi thử → Dựng lại**. Scene hiện tại chỉ có 2 thẻ; chưa dựng lại thì vào Level 06 sẽ báo `No CardView for SupportBarrier`.
3. Mở `Assets/HeroEscape/Scenes/LevelSelect.unity` → Play → **Level 06**.

## 8. Kết quả kiểm tra

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M09-C1 | Compile Core, Search, Unity (+DOTween), Editor, toàn bộ test kể cả test cần UnityEngine | Passed (ngoài Unity) | Roslyn + DLL Unity 6000.3.8f1, C# 9; chỉ cảnh báo CS0649 cũ. |
| M09-T1 | 275 test EditMode không cần UnityEngine | Passed (ngoài Unity) | 275 passed, 0 failed (221 cũ, không sửa kỳ vọng nào, + 54 mới: `BarrierTests`, `Level06Tests`, thêm vào `CardHandTests`). |
| M09-T2 | Test cần Unity: `UnityBoardViewTests` (vật cản, ghost, dựng lại), `UnityHandViewTests` (3 thẻ), `UnityLevelCatalogTests` (6 màn) | Not run | Cần Unity Test Runner; đã compile. |
| M09-S1 | Dựng lại gameplay scene | Not run | |
| M09-P1 | Play Mode (checklist mục 9) | Not run | |
| M09-D1 | Điện thoại thật | Not run | |

Test chính:
- Mục tiêu hợp lệ/không hợp lệ, gồm cấm góc chéo do Tường hoặc X.
- Cửa, ngoài bàn, ô đã có vật cản, không có thẻ hoặc hết lượt.
- Dùng đúng trừ đúng 1, không chạy pha địch, phát `BarrierPlaced`. Giới hạn một phụ trợ chung với Stasis.
- Chặn Move, Dash (bước 1 và bước 2), góc chéo, đường X (X đứng yên), LOS của Stasis, BFS.
- Hai X: vật cản tồn tại cả pha, xóa đúng một lần ở cuối. X bị khóa vẫn xóa.
- Hành động sai không rút ngắn thời hạn. Thắng giữa chừng không chạy thêm pha.
- So sánh và hash trạng thái.
- Preview không commit và khớp thực thi. Restart xóa vật cản.
- Tactical xét Barrier chỉ khi có thẻ; quyết định deterministic; fallback = Basic từ trạng thái gốc.
- Parser.
- Level 06: cấu hình, 3 lời giải phát lại thắng, thắng được bằng chạm thẻ + ô highlight, 7 điều kiện thiết kế, tác dụng của Barrier ở lượt then chốt, thời điểm đặt.
- Tay bài và input: highlight đúng ô, chạm sai giữ lựa chọn, hover chỉ preview vị trí, thẻ rời tay, đổi thẻ, Restart, bố cục 3 thẻ.

## 9. Checklist nghiệm thu (điện thoại)

1. Level Select có 6 nút; Level 06 mở được. Thắng Level 05 → Next Level vào Level 06. Thắng Level 06 không có Next.
2. Level 06: tay bài Hero giữa, Stasis phải, Barrier trái (viền cam).
3. Chạm Barrier → thẻ nâng, highlight chỉ các ô trống kề Hero. Ô chéo bị Tường chặn góc không sáng. Chạm ô khác: không mất thẻ, thẻ vẫn nâng.
4. Chạm ô sáng → vật cản cam (sprite tường) có nhãn `1` xuất hiện. Thẻ Barrier biến mất, Stasis mờ (đã dùng phụ trợ lượt này). Hero vẫn đi/Wait/Dash được, không đi vào vật cản.
5. Hết pha địch → vật cản mờ dần biến mất; lượt sau Stasis sáng lại (nếu còn).
6. Thử lời giải 1: `N N W NW`, Barrier ô phải Hero, `Wait`, Dash phải, Dash chéo lên phải → thắng.
7. Thử lời giải 2: Dash lên, Dash lên, Stasis X01, phải, chéo lên phải → thắng.
8. Cùng tình huống lượt 5 nhưng không đặt Barrier mà Wait → bị bắt (so sánh).
9. Restart lúc đang có vật cản → vật cản biến mất ngay, tay bài đủ 3 thẻ. Back/Level Select không mang vật cản sang màn khác.
10. Level 01–05 chơi như cũ, không có thẻ Barrier.

## 10. File chính

- Core: `GridRules.cs`, `GameState.cs`, `Simulation.cs`, `GameSession.cs`, `LevelData.cs`, `LevelParser.cs`, `HeroAction.cs` (`SolutionTurn`).
- Search: `EnemyPathfinder.cs`, `TacticalChasePolicy.cs`.
- Unity: `TurnInputController.cs`, `CardHand.cs`, `CardView.cs`, `BoardView.cs`, `EnemyStatusLabel.cs`, `GameController.cs`, `UiText.cs`.
- Editor: `M01SceneBuilder.cs` (thẻ Barrier, Level 06), `LevelSelectSceneBuilder.cs` (comment bố cục).
- Dữ liệu: `Data/Levels/M06_BarrierLevel.txt` (+ `.meta`), `Data/LevelCatalog.asset`.
- Test: `BarrierTests.cs`, `Level06Tests.cs` (mới); `LevelReachability.cs`, `CardHandTests.cs`, `UnityBoardViewTests.cs`, `UnityHandViewTests.cs`, `UnityLevelCatalogTests.cs` (sửa).
