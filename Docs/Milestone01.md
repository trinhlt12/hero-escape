# Milestone 01 — Màn chơi thử đầu tiên

Ngày bắt đầu: 2026-10-01 • Trạng thái: **Đã có code và test; chờ dựng scene + kiểm tra Play Mode trong Editor**

Tài liệu này khóa phạm vi Milestone 01. Các tính năng khác trong GDD/TDD thuộc các mốc sau, **không được tự triển khai** khi làm tiếp M01. Muốn mở rộng phải có yêu cầu mới từ Game Designer.

## 1. Phạm vi

### In-scope

- Một scene chơi thử, bàn cố định 5x5, đọc từ file level text.
- Một Hero dùng sprite H01, **không** có kỹ năng hoặc nội tại.
- Đúng một X01 dùng AI Basic (BFS).
- Sàn, Tường cố định, đúng một Cửa thoát.
- Một test level, có chuỗi nước đi thắng đã kiểm chứng bằng test. Đây **không** phải một trong 12 màn chính thức.
- Luật:
  - Hero luôn đi trước, mỗi lượt đi một ô theo 8 hướng hoặc Chờ (chỉ qua nút Chờ).
  - Hero không ra ngoài bàn, không vào Tường, không vào ô X01.
  - Bước chéo cần cả hai ô cạnh trực giao trống. Tường và nhân vật chặn góc; với X01, Cửa cũng chặn góc.
  - Click ô không hợp lệ không đổi trạng thái và không mất lượt. Click ô Hero không tính là Chờ.
  - Hero đến Cửa thì thắng ngay, X01 không đi lượt đó.
  - X01 đi tối đa một ô theo BFS 8 hướng, chi phí đều, phá hòa theo thứ tự N, NE, E, SE, S, SW, W, NW (N = y + 1).
  - X01 không vào hoặc đi xuyên Cửa. Không có đường thì X01 đứng yên. X01 vào ô Hero thì thua.
  - Không có ngẫu nhiên.
- Restart dùng được khi đang chơi và sau Thắng/Thua, hủy phần trình bày của lượt cũ.
- UI tạm bằng uGUI: nút Chờ, nút Chơi lại, text trạng thái/Thắng/Thua, một dòng hướng dẫn, một dòng thông báo.
- Input chuột qua Input System. Click lên UI không kích hoạt ô phía sau. Chặn input khi đang giải quyết lượt và sau Thắng/Thua.
- Bố cục dọc: bàn cờ và nút nhìn thấy ở 9:16 và 9:20; UI không che bàn.
- Art: chỉ 5 sprite trong `Assets/Art` (floor, wall, exit, hero_h01, enemy_x01).

### Out-of-scope (không làm trong M01)

H02, X02, nhiều X, Boss • active/passive Hero, thẻ phụ trợ, hiệu ứng trạng thái • tường tạm • Tactical AI, preview, Undo • solver sản phẩm, procedural generation, 12 màn • chọn Hero/loadout, menu nhiều màn, lưu tiến trình, replay • âm thanh, VFX, animation sheet • build Android, backend, monetization • art mới, icon, overlay, panel/button sprite.

## 2. Quyết định kỹ thuật riêng của M01

Các quyết định này cũng được ghi trong `TechnicalDecisions.md` (D4).

| Hạng mục | Quyết định M01 | Ghi chú so với TDD |
|---|---|---|
| Tọa độ | `GridPos(X, Y)`, gốc dưới-trái, Y tăng lên trên, **N = Y + 1** | TDD mục 6 dùng row/col tăng xuống dưới. Ý nghĩa hướng như nhau (N là phía trên màn hình); chỉ khác ký hiệu. |
| Dữ liệu level | File text `Assets/HeroEscape/Data/Levels/M01_TestLevel.txt` | TDD chọn ScriptableObject cho LevelDefinition; sẽ xem lại khi có nhiều màn. |
| Art | Dùng 5 **Sprite asset (.asset)** cắt từ `Assets/Art/artboard.png`; giữ PPU 100 của sprite; view scale đều theo cạnh dài nhất để tile vừa ô (100%), nhân vật 80% ô | Prompt ghi `.png`; thực tế là `.asset`. Không đổi import, không đổi GUID. |
| Material | `Sprite-Unlit-Default` của URP (package) | Theo D2. |
| Chữ UI | `UnityEngine.UI.Text` + font tích hợp `LegacyRuntime.ttf` (dynamic) | TDD/Art Guide yêu cầu TMP + font tiếng Việt riêng; chưa có font được bàn giao. |

## 3. Test level `M01_TEST`

```
y=4   #  .  .  .  E
y=3   .  .  #  .  .
y=2   .  X  .  #  .
y=1   .  .  #  .  .
y=0   H  .  .  .  .
     x=0 1  2  3  4
```

- Hero bắt đầu ở (0,0), X01 ở (1,2), Cửa ở (4,4). Tường ở (0,4), (2,3), (3,2), (2,1).
- Chuỗi thắng tham chiếu, 7 lượt: **E E E NE N N N**

| Lượt | Hero | X01 sau lượt |
|---|---|---|
| 1 E | (1,0) | (1,1) |
| 2 E | (2,0) | (1,0): không cắt chéo SE được vì Tường (2,1) chặn góc |
| 3 E | (3,0) | (2,0) |
| 4 NE | (4,1) | (3,0) |
| 5 N | (4,2) | (4,1) |
| 6 N | (4,3) | (4,2) |
| 7 N | (4,4) = Cửa → **Thắng** | không đi |

- Bẫy thiết kế: nước đầu N hoặc NE bị bắt ngay; Chờ ở nước đầu thì mất đường thắng (kiểm bằng công cụ tạm ngoài project).

## 4. Cách chạy

1. Mở project bằng Unity **6000.3.8f1** và chờ Unity compile xong (Console không có lỗi đỏ).
2. Chọn menu **Hero Escape > Milestone 01 > Dựng scene chơi thử**. Menu sẽ:
   - kiểm tra 5 sprite (chỉ đọc), rồi tạo `Assets/HeroEscape/Scenes/M01_TestLevel.unity` với đủ object, component và reference;
   - hỏi trước khi ghi đè nếu scene đã tồn tại (Dựng lại / Hủy / Chỉ mở scene);
   - nếu scene đang mở có thay đổi chưa lưu, hỏi lưu trước.
3. Bấm **Play**.
4. Menu khác: **Mở scene chơi thử**, **Kiểm tra art M01** (in báo cáo vào Console).
5. Chạy test: **Window > General > Test Runner > EditMode > Run All**.

## 5. Tiến độ và kết quả kiểm tra

Quy ước: Passed = đã chạy và đạt. Failed = đã chạy và không đạt. Not run = chưa chạy, **không** được coi là đạt.

| # | Kiểm tra | Trạng thái | Bằng chứng / ghi chú |
|---|---|---|---|
| M01-C1 | Compile 5 assembly (Core, Search, Unity, Editor, Tests) | Passed (ngoài Unity) | Roslyn `csc` đi kèm Unity 6000.3.8f1, tham chiếu DLL Unity 6000.3.8f1 + `UnityEngine.UI` + `Unity.InputSystem` của project, API .NET Standard 2.1. Chỉ có cảnh báo CS0649 cho field `[SerializeField]`. |
| M01-C2 | Unity Editor compile bản cuối | Not run | Editor đang mở chỉ mới compile bản sớm của Core/Search/Tests lúc 12:51 (`Tundra build success`). Bản cuối và assembly Unity/Editor sẽ compile khi cửa sổ Unity được focus. |
| M01-T1 | 33 test EditMode | Passed (ngoài Unity) | Chạy bằng bộ chạy reflection tạm trên .NET 6 với `nunit.framework.dll` của Unity: 33 passed, 0 failed. Bộ chạy đã được thử với test cố tình sai và báo FAIL đúng. |
| M01-T2 | 33 test EditMode trong Unity Test Runner | Not run | Cần chạy trong Editor. |
| M01-A1 | Alpha của art | Passed | `artboard.png` là PNG RGBA 8-bit; góc mỗi sprite alpha = 0, mép chuyển sắc gọn; không phải nền caro vẽ sẵn. |
| M01-A2 | Cấu hình import | Passed (đọc `.meta`) | Texture Type Sprite, mipmap tắt, Alpha Is Transparency bật, pivot sprite (0.5,0.5). `artboard.png` ở chế độ **Multiple** (16 lát cắt); 5 sprite dùng là Sprite asset đơn lẻ. Không đổi sang Single vì sẽ phá lát cắt. |
| M01-S1 | Menu dựng scene chạy thành công | Not run | Cần thao tác trong Editor. |
| M01-P1 | Play Mode: đi 8 hướng, chặn tường/biên/góc, Chờ, Thắng, Thua, Restart | Not run | Logic đã có test; phần nối input/UI chưa kiểm chứng trong Editor. |
| M01-P2 | Click UI không kích hoạt ô phía sau | Not run | Đã raycast UI trước khi đọc ô; chưa thử trong Editor. |
| M01-P3 | Bố cục 9:16 (1080x1920) và 9:20 (1080x2400) | Not run | Đã tính: ở 9:16 bàn cao khoảng 982 px trong vùng trống 1260 px; ở 9:20 vùng trống rộng hơn. Chưa nhìn trên Game view. |
| M01-F1 | Chữ tiếng Việt hiển thị đúng | Not run | Không có font tiếng Việt riêng trong project. `HudView` in cảnh báo Console nếu font thiếu glyph. |

### Danh sách test (EditMode)

- `GridRulesTests`: 8 hướng, N = y + 1 và thứ tự hướng, chặn biên, chặn tường/ô X01, chéo bị chặn bởi tường ở từng cạnh, chéo bị chặn bởi X01, Hero đi qua góc Cửa, X01 không vào Cửa và Cửa chặn góc X01, X01 bắt Hero nhưng Hero chặn góc chéo của X01.
- `EnemyPathfinderTests`: BFS ngắn nhất với bước chéo chi phí 1; phá hòa NE trước E và N trước NE, ổn định khi gọi lặp lại; đi vòng tường theo luật góc; không đi qua Cửa và không có đường thì trả null.
- `SimulationTests`: bước hợp lệ rồi X01 đi đúng một bước; nước sai không đổi trạng thái và không kích hoạt X01; tường/góc bị chặn bị từ chối; Chờ kích hoạt đúng một lượt X01; Hero đến Cửa thắng trước X01; X01 bắt Hero thì thua; không có đường thì đứng yên; sau kết thúc từ chối mọi hành động; cùng trạng thái và hành động cho cùng kết quả.
- `GameSessionTests`: nước sai không tăng Revision; Restart khôi phục đúng khi đang chơi, sau thua và sau thắng.
- `LevelParserTests`: hàng đầu là Y lớn nhất; từ chối dữ liệu sai (ký hiệu lạ, thiếu Hero, hai X, hàng lệch, thiếu Cửa, bàn 4x4).
- `M01TestLevelTests`: bố cục file level thật; chuỗi tham chiếu dẫn đến Thắng; đi N ở nước đầu bị bắt.

## 6. Hạn chế đã biết

- Chưa có highlight ô đi được (cần art Overlay, ngoài phạm vi). Ô sai hiện thông báo chữ.
- Font: dùng font tích hợp; nếu Console cảnh báo thiếu glyph thì kiểm tra bằng mắt trên Game view.
- Tile sàn 301x292 px và tường 301x293 px không vuông tuyệt đối; giữ tỷ lệ nên có khe khoảng 3% theo chiều dọc giữa các hàng.
- `ProjectSettings/ShaderGraphSettings.asset` được Unity tự thêm `overrideShaderVariantLimit: 0` lúc 12:51; không phải do M01 sửa.

## 7. Việc tiếp theo trong M01

1. Dựng scene bằng menu, chạy Play Mode theo mục 5, ghi kết quả vào bảng.
2. Chạy Test Runner EditMode trong Unity, ghi kết quả.
3. Sửa lỗi nếu có. **Không** thêm tính năng ngoài mục 1.
