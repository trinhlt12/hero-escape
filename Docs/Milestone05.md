# Milestone 05 — Highlight lựa chọn hợp lệ, hover preview, Dash chọn ô đích

Ngày: 2026-10-01 • Trạng thái: **Hoàn thành.** Game Designer đã playtest Level 01–04 với highlight/hover preview thành công (báo cáo 2026-10-01). Bảng mục 5 giữ nguyên kết quả do Claude ghi lúc bàn giao.

Áp dụng cho Level 01–04 (đã được Game Designer playtest thành công trước task này). Không đổi bố cục, tài nguyên, AI hay luật gameplay của các màn. Không thêm màn, kỹ năng, thẻ hoặc quái mới.

## 1. Quyết định tương tác (thay thế hướng dẫn cũ)

Các quyết định dưới đây **thay thế** mọi mô tả tương tác khác trong tài liệu gốc và tài liệu trước. Cụ thể là:
- GDD mục 4 (Undo) và mục 9 ("Nhấn Xác nhận để thực hiện; Hủy giữ nguyên trạng thái").
- TDD mục 10 (Undo, preview cần xác nhận).
- Art Guide các mục về nút Xác nhận/Hủy/Hoàn tác và overlay O01/O02 cho highlight.
- `Milestone02.md` (Dash chọn hướng bằng ô kề Hero).

Đã ghi vào `TechnicalDecisions.md` (D8) và `CLAUDE.md`.

- **Không có Undo, không có nút Confirm, không có nút Cancel.**
- **Highlight luôn hiện** trong lượt Hero, cho mọi lựa chọn hợp lệ của chế độ hiện tại.
- **Hover chỉ dự đoán** (Editor, chuột); không tiêu hao lượt hay tài nguyên.
- **Click ô đích/mục tiêu hợp lệ là thực hiện ngay.**
- **Dash chọn bằng ô đích** (ô sẽ đáp xuống), không chọn hướng bằng ô kề.
- Bấm nút kỹ năng chỉ đổi chế độ. Bấm lại nút kỹ năng đang chọn hoặc nhấn Escape để trở về Move.

## 2. Chế độ và highlight

| Chế độ | Ô được highlight | Click |
|---|---|---|
| **Move** (mặc định) | Mọi ô Move hợp lệ: không ra ngoài bàn, không vào Tường, không vào ô X, đúng luật chặn góc. Không loại ô chỉ vì sẽ bị bắt sau pha địch. | Đi ngay. |
| **Dash** | Ô **đích** Dash hợp lệ: cách đúng 2 bước theo 8 hướng, kiểm tra cả hai bước, mọi X, góc chéo; không tự rút về 1 bước. Ngoại lệ: bước 1 là Cửa thì đích là Cửa. Ô trung gian không phải mục tiêu. | Dash ngay, trừ đúng 1 Dash. Click ô kề hoặc ô không highlight: phản hồi, không làm gì. |
| **Stasis** | Ô của từng X là mục tiêu hợp lệ: còn thẻ, chưa dùng phụ trợ lượt này, X chưa khóa, Chebyshev ≤ 3, đường nhìn không bị Tường chắn. | Khóa đúng X đó (theo ID), trở về Move; Hero vẫn còn hành động chính. |

- Highlight được tính bằng **cùng bộ kiểm tra với thực thi**: `Simulation.TryGetDestination` / `LegalTargets` và `ApplyStasis`. Không có luật Dash thứ hai: ô đích được đổi thành `HeroAction.Dash(hướng)` rồi gọi luật hiện có.
- Không highlight trong pha địch hoặc sau thắng/thua. Sau Stasis thành công và sau mỗi lượt (nếu còn chơi): trở về Move, highlight từ trạng thái mới.
- **Hiển thị:** sprite sàn của ô hợp lệ được **thay** bằng `Assets/Art/Board/tile_highlight.asset`; không tint, không phủ lớp lên nhân vật hay cửa.
  - Cửa, Tường và nhân vật vẫn vẽ phía trên (sàn order 0 < cửa 10 < ghost 15 < nhân vật 20/21).
  - Hết highlight thì trả lại sprite sàn và scale gốc.
  - Chỉ đổi View, không đổi dữ liệu địa hình.

### Kiểm tra asset `tile_highlight`

- Là Sprite asset (`.asset`), cắt từ `Assets/Art/Gạch sàn vuông xanh lá pastel.png`; rect 484x455 px, pivot (0.5, 0.5), PPU 100.
- Ảnh nguồn PNG RGBA 1536x1024, alpha thật: góc rect alpha 0, mép chuyển sắc gọn, không phải nền caro. Texture Type Sprite, mipmap tắt, Alpha Is Transparency bật.
- `tile_floor` là 301x292 px, tỷ lệ khác `tile_highlight` khoảng 3%. `BoardView` scale `tile_highlight` **riêng từng trục** để kích thước hiển thị trùng khít ô sàn; pivot cùng ở giữa nên lưới không nhảy hoặc lệch. Không sửa ảnh gốc hay GUID.

## 3. Hover preview (Editor, chuột)

| Hover | Hiển thị |
|---|---|
| Ô Move hợp lệ | Ghost Hero ở ô đích và ghost mỗi X ở vị trí sau toàn bộ pha địch (theo `spawnIndex`). |
| Ô đích Dash hợp lệ | Như trên, cho Dash. |
| X là mục tiêu Stasis hợp lệ | Nhãn `Will be frozen` trên X đó. Không mô phỏng pha địch, không giả định Wait. |
| Nút Wait (chỉ ở chế độ Move) | Hero đứng yên và ghost các X sau pha địch. |

- Dự đoán bị bắt: `You will be caught`. Dự đoán thắng: `You will escape`. Không chặn click nước đi dẫn đến thua.
- Preview dừng đúng chỗ: Hero tới Cửa thì không mô phỏng địch; X1 bắt Hero thì không mô phỏng X2. Đây là hành vi của chính `Simulation`.
- Ghost dùng lại sprite quân, độ đục 0,4, vẽ **dưới** quân thật. Ghost không nhận input và không tham gia occupancy, vì chỉ là View và không có trong `GameState`. Số thứ tự `1`/`2` vẫn gắn trên quân thật.
- Rời mục tiêu hoặc hover ô không hợp lệ: xóa ghost và cảnh báo, giữ highlight. Không tự chọn nước mặc định.
- Chưa có preview cho màn hình cảm ứng.

## 4. Tính nhất quán và vòng đời

- Preview gọi `GameSession.Preview` / `PreviewStasis`, tức chính `Simulation` và `BasicChasePolicy` của trận, trên `GameState` bất biến.
  - `GameState.WithEnemy` sao chép mảng X trước khi sửa, nên preview không thể đổi danh sách X, khóa hay tài nguyên của trạng thái thật.
  - Hover không tăng `Revision`, không phát event gameplay hay animation thật.
- Preview sau Stasis tính từ trạng thái đã khóa thực tế.
- Cache:
  - `TurnInputController` cache highlight theo (revision, chế độ, khả năng hành động) và cache preview theo (revision, chế độ, khả năng hành động, ô, Wait).
  - `GameController` chỉ cập nhật View khi khóa (màn, revision, chế độ, khả năng hành động, ô hover, hover Wait) đổi. Nhờ vậy không mô phỏng lại mỗi frame; nếu chuột đứng yên mà trạng thái đổi thì preview vẫn được cập nhật.
- Click luôn kiểm tra lại với trạng thái hiện tại (`LegalTargets` tính mới), không áp kết quả preview cũ.
- Restart, chuyển màn, bắt đầu pha địch, kết thúc trận: xóa preview và highlight cũ, tạo lại từ trạng thái mới.
- Click lên UI không xuống bàn; input bị chặn trong pha địch (giữ nguyên cơ chế cũ).

## 5. Kết quả kiểm tra

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M05-C1 | Compile Core, Search, Unity, Editor và toàn bộ test | Passed (ngoài Unity) | Roslyn của Unity 6000.3.8f1 + DLL Unity 6000.3.8f1 (+ `UnityEngine.TestRunner`); C# 9. Chỉ cảnh báo CS0649. |
| M05-T1 | 166 test EditMode không cần UnityEngine | Passed (ngoài Unity) | 166 passed, 0 failed (142 cũ, trong đó test Dash đã sửa theo cách chọn ô đích, + 24 mới). |
| M05-T2 | 4 test `UnityBoardViewTests` (đổi sprite sàn, khôi phục, căn kích thước, thứ tự vẽ, ghost) | Not run | Cần Unity Test Runner; đã compile với DLL Unity. |
| M05-T3 | Test Runner trong Unity (toàn bộ) | Not run | |
| M05-C2 | Unity Editor compile bản này | Not run | Editor đang mở project. |
| M05-S1 | Dựng lại scene | Not run | |
| M05-P1 | Kiểm tra trực quan Play Mode | Not run | Chưa playtest. |

Test mới (`InteractionTests`, 24 trường hợp chạy được ngoài Unity):
- **Highlight:**
  - Move đúng biên, Tường, X, chặn góc; vẫn highlight nước dẫn tới bị bắt.
  - Dash chỉ highlight ô đích; X chặn góc ở bước 2; bước 2 bị chặn thì không highlight và click không thực hiện.
  - Stasis theo tầm và đường nhìn (hai X).
  - Đổi chế độ không tiêu hao; không highlight trong pha địch hoặc sau khi thua; sau Stasis trở về Move.
- **Preview:**
  - Hover nhiều lần ở mọi chế độ không đổi trạng thái.
  - Preview khớp thực thi cho **mọi** ô highlight Move/Dash và Wait ở Level 01, 02, 04.
  - Sau Stasis bỏ qua đúng X, đúng một pha.
  - Dừng khi thắng; dừng khi X1 bắt (không mô phỏng X2).
  - Hover Stasis không mô phỏng pha địch.
  - Cache không tính lại khi không đổi, có tính lại khi trạng thái đổi dưới cùng ô.
- **Click:** Restart dựng lại highlight; click thực hiện đúng một lần.
- **Hồi quy:** chuỗi tham chiếu Level 01–04 thắng khi **chơi bằng click ô highlight** (và Stasis bằng click X); trạng thái cuối trùng với chuỗi đã kiểm chứng.

`TurnInputControllerTests` đã sửa: Dash không còn nhận ô kề; click ô đích Dash thực hiện ngay, trừ 1 Dash, đúng một lần; bước 1 là Cửa thì đích là Cửa.

## 6. Cách chạy

1. Focus Unity, chờ compile xong.
2. **Hero Escape > Milestone 01 > Dựng scene chơi thử** → **Dựng lại**. Bắt buộc, để `BoardView` nhận sprite `tile_highlight`. Menu kiểm tra art giờ kiểm cả `tile_highlight`.
3. Play.

## 7. Hạn chế

- Scene cũ chưa có tham chiếu `tile_highlight`: nếu chưa dựng lại thì highlight không hiện (preview vẫn chạy).
- Ghost X chỉ hiện khi X đổi vị trí; X đứng yên hoặc bị khóa không có ghost.
- Cảnh báo dự kiến hiện ở dòng thông báo phía trên, thay tạm thông báo đang có khi hover.
- Không có preview cho cảm ứng.
- File art mới (`tile_highlight.asset`, ảnh nguồn) chưa được commit; cần commit cùng `.meta`.
