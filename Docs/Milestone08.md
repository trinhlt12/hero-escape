# Milestone 08 — Tay bài tương tác (DOTween)

Ngày: 2026-10-02 • Trạng thái: **Đã có code và test ngoài Unity; chờ setup DOTween trong Editor, dựng lại scene và kiểm tra Play Mode/điện thoại**

Gameplay, Tactical AI, Level Select và bản thử trên điện thoại **đã được Game Designer nghiệm thu**. Milestone này chỉ thay cách kích hoạt kỹ năng/thẻ: nút Dash/Stasis được thay bằng tay bài ở đáy màn hình. Không đổi luật, AI, level hay cân bằng; không tăng `rulesVersion`.

## 1. Phạm vi

- Cài DOTween **bản miễn phí** (xem mục 2).
- Tay bài ở đáy màn hình dọc: **thẻ Hero (Dash)** và **thẻ phụ trợ (Stasis)** hiện có.
- Animation chọn, bỏ chọn, dùng hết (biến mất) và sắp xếp lại tay bài.
- Dùng chung dữ liệu, highlight, preview và thực thi hiện tại; chuột và cảm ứng.
- Cập nhật scene builder và tài liệu.
- Ngoài phạm vi: thẻ mới (Lure, Barrier…), level mới, chỉnh Level 01–05, chọn bộ thẻ trước trận, rút/xáo bài, tier/nâng cấp/mở khóa/lưu tiến trình, art mới, âm thanh, Undo/Confirm/Cancel, đổi AI.

## 2. DOTween

| Mục | Giá trị |
|---|---|
| Phiên bản | **DOTween v1.3.030** (bản miễn phí, phát hành 2026-06-23) |
| Nguồn | Trang chính thức Demigiant: `https://dotween.demigiant.com/download.php` → `https://dotween.demigiant.com/downloads/DOTween_1_3_030.zip` |
| SHA-256 file zip | `62a0ececd274e1587eb0dea15f3afab392fbda5a0f8cac7287fbf7f64925a1ba` |
| Vị trí | `Assets/Plugins/Demigiant/DOTween/` (đúng đường dẫn mặc định của gói) |
| Tương thích | Bản tải từ trang chính thức hỗ trợ Unity 2020.3 trở lên; 1.3.030 sửa cảnh báo UDR0001 trên Unity 6000.3 |

**Cách cài đã làm:** giải nén `DOTween_1_3_030.unityPackage` và chép đúng các file + `.meta` gốc của gói (giữ GUID của Demigiant), tương đương *Import Package*. Thêm `Assets/Plugins.meta` và `Assets/Plugins/Demigiant.meta` (gói không có).
- **Không chép** 3 file `*.dll.mdb` (debug symbol của Mono). `.gitignore` của project đã loại `*.mdb`; Unity không cần chúng để chạy.
- Không dùng DOTween Pro, không thêm thư viện tween khác.
- Kiểm tra Git: 48 file (gồm `.meta`) đều **không bị `.gitignore` loại**; `.dll` là binary thường, `.cs`/`.meta` là text, **không vào LFS**.

**Asmdef:**
- `HeroEscape.Unity`: `overrideReferences: true`, `precompiledReferences: ["DOTween.dll"]` (tham chiếu tường minh).
- `HeroEscape.Core`, `HeroEscape.Search`: `overrideReferences: true`, danh sách rỗng. DOTween.dll là DLL auto-reference; bật override ở hai tầng này bảo đảm **Core/Search không thể dùng DOTween** (trước đây cũng không tham chiếu DLL nào).
- `HeroEscape.Editor`, test: không đổi. Test không dùng kiểu DOTween; API công khai của `HandView` không lộ kiểu DOTween.
- Code chỉ dùng phần lõi trong `DOTween.dll` (`DOTween.To`, `Sequence`, `DOLocalRotate`, `DOScale`), **không phụ thuộc module** (`DOTweenModuleUI`…), nên không cần asmdef cho module.

**Bước Game Designer cần làm trong Editor** (mục 5, bước 1): mở *Tools > Demigiant > DOTween Utility Panel* → **Setup DOTween…** → giữ module mặc định → Apply. Bước này tạo `Assets/Resources/DOTweenSettings.asset` và có thể ghi scripting define vào `ProjectSettings/ProjectSettings.asset`; các file đó cần được commit cùng milestone.

## 3. Dữ liệu và luồng

- **Nguồn dữ liệu duy nhất là `GameState`.** `CardHand.From(input)` dựng tay bài mỗi lần vẽ lại:
  - Thẻ Hero có mặt khi `dash > 0` trong file level **và** `DashUsesLeft > 0`; số trên thẻ = `DashUsesLeft`. Hai lần Dash là **một** thẻ.
  - Thẻ Stasis có mặt khi `stasis > 0` **và** `StasisUsesLeft > 0`.
  - Level 01 (không Dash/Stasis): tay rỗng, không thẻ giả.
  - UI không giữ bộ đếm riêng.
- **Chọn thẻ = chế độ chọn mục tiêu hiện có.** Thẻ Hero được chọn ⇔ `InputMode.ChoosingDashTarget`; thẻ Stasis ⇔ `ChoosingStasisTarget`. Chạm thẻ gọi đúng API cũ `TurnInputController.PressDash()` / `PressStasis()`:
  - Bật chế độ.
  - Chạm thẻ khác thì chuyển thẳng sang chế độ của thẻ đó.
  - Chạm lại thẻ đang chọn thì về Move.
  - Escape (desktop) gọi `Cancel()`.
  - Không tiêu hao, không tăng lượt, không chạy pha địch, không đổi `GameState`.
- **Mục tiêu và thực thi** không đổi:
  - Dash highlight và nhận **ô đích** hợp lệ.
  - Stasis nhận click lên X trong tầm/LOS.
  - Mục tiêu sai: không tiêu hao, giữ lựa chọn.
  - Thành công: Core trừ một lần dùng; `TurnInputController` về Move. Tay bài vẽ lại từ trạng thái mới: thẻ còn lượt hạ về chỗ nghỉ, thẻ hết lượt chạy animation biến mất.
  - Stasis không chạy pha địch; người chơi tiếp tục hành động chính.
- Highlight, preview và thực thi vẫn dùng chung `Simulation.TryGetDestination` / `LegalTargets` / `ApplyStasis`. `CardView`/`HandView` không có logic dự đoán.
- **Trạng thái mờ:** thẻ còn trên tay nhưng luật chưa cho dùng (trận kết thúc, hoặc đã dùng phụ trợ trong lượt khi còn lượt Stasis) hiển thị mờ. Pha địch (Busy) không làm mờ để tránh nhấp nháy, nhưng input vẫn bị chặn.

## 4. Bố cục và animation

**Cấu trúc scene (builder):**
- `SafeArea > BottomPanel`, cao 676:
  - `Controls`, cao 406, neo cạnh trên. Từ trên xuống: hàng điều hướng (Next/Back/Level Select, cao 120), hàng hành động (Wait, Restart, cao 120), Hint (cao 120).
  - Phần đáy 270 dành cho tay bài.
- `SafeArea > Hand` (`HandView`, không có graphic) nằm sau `BottomPanel` nên vẽ trên panel. Gồm 2 thẻ `HeroCard`, `StasisCard` (`CardView`).
- Màn không có thẻ (Level 01): `HudView.SetHandSpace(false)` thu panel còn 406 khi vào màn. Panel không đổi giữa màn, nên bàn cờ không nhảy khi thẻ cuối rời tay.
- Thẻ tạm: sprite tích hợp `UI/Skin/UISprite.psd` + màu phẳng + text.
  - Hero: viền xanh `#247FAD`, nền `#154660`.
  - Support: viền tím `#714D9A`, nền `#3A2852`.
  - Đang chọn: viền vàng `#E8B849` (cùng màu nút Dash "đang chọn" cũ). Không dùng màu xanh lá để tránh ngụ ý "an toàn".
  - Phần nhìn thấy khi nghỉ: nhãn `HERO`/`SUPPORT`, tên, số lần dùng còn lại (`2 uses left`). Mô tả nằm ở nửa dưới, đọc được khi thẻ nâng lên.

**Bố cục quạt** (`HandLayout`, hàm thuần, đơn vị canvas 1080 x 1920):
- Có Hero: Hero ở giữa và thẳng, to 115%. Phụ trợ thứ nhất bên phải, thứ hai bên trái… Không dịch Hero để căn giữa cả nhóm.
- Không có Hero: phụ trợ căn giữa; một thẻ thì ở giữa và thẳng.
- Thẻ hai bên nghiêng ra ngoài và thấp dần theo đường cong.
- Thẻ được chọn nâng theo trục của chính nó: phụ trợ giữ góc nghiêng, Hero nâng thẳng. Thẻ đó vẽ trên cùng; các thẻ khác giữ chỗ.
- Thứ tự vẽ = thứ tự nhận chạm của uGUI: thẻ xa tâm dưới, Hero trên phụ trợ, thẻ đang chọn trên cùng.

**Thông số chỉnh được trong Inspector** (`Hand` → `HandView`):

| Nhóm | Field | Mặc định | Ý nghĩa |
|---|---|---|---|
| Bố cục | `slotSpacing` | 230 | Khoảng cách tâm hai vị trí kề nhau |
| | `heroExtraSpacing` | 18 | Cộng thêm giữa Hero và thẻ kề |
| | `tiltPerSlot` | 8° | Góc nghiêng mỗi vị trí |
| | `dropPerSlot` | 16 | Độ hạ theo bình phương vị trí |
| | `restY` | −130 | Cạnh dưới thẻ khi nghỉ (âm = thụt dưới mép vùng an toàn) |
| | `heroScale` | 1.15 | Hero so với phụ trợ |
| | `liftHeight` | 145 | Độ nâng khi chọn |
| | `selectedScale` | 1.0 | Phóng to thêm khi chọn |
| | `unavailableAlpha` | 0.5 | Độ mờ thẻ chưa dùng được |
| Animation | `selectDuration` | 0.17 s | Nâng/hạ khi chọn, bỏ chọn, đổi thẻ (OutCubic) |
| | `reflowDuration` | 0.20 s | Sắp xếp lại sau khi có thẻ rời tay |
| | `removeDuration` | 0.22 s | Biến mất: tụt `removeDrop` = 80, thu `removeScale` = 0.8, mờ dần (InQuad) |
| Input | `pressCooldown` | 0.08 s | Bỏ qua lần chạm thẻ quá sát lần trước |

Kích thước thẻ (builder): phụ trợ 240 x 330, Hero hiển thị 276 x 380, viền 10. `HudView.handSpace` = 270.

**Đo trên bố cục mặc định** (đơn vị canvas, tính từ đáy vùng an toàn):
- Nghỉ: Hero lộ 250, phụ trợ lộ khoảng 184. Phần lộ chứa nhãn, tên, số lần dùng, nằm trong vùng an toàn nên chạm được.
- Hero nâng lên: cạnh dưới +15, đỉnh 395.
- Hàng Wait/Restart bắt đầu ở 400, nên thẻ nâng chỉ che Hint, không che nút và bàn cờ.

**Ảnh hưởng tới bàn cờ** (bàn chỉ nằm giữa panel trên 300 và panel dưới):

| Tỷ lệ | Màn | Vùng trống cho bàn | Bàn 6x6 (ô) | Bàn 5x5 (ô) | So với M07 |
|---|---|---|---|---|---|
| 9:16 (1080x1920) | có thẻ | 944 | 145 | 172 | nhỏ hơn khoảng 12–13% (M07: 166 / 196) |
| 9:16 | Level 01 | 1214 | — | 196 | không đổi |
| 9:20 (1080x2400) | có thẻ | 1424 | 166 | 196 | không đổi (giới hạn bởi chiều ngang) |

**Đúng đắn của animation:**
- Vị trí luôn tính lại từ `HandLayout` (vị trí chuẩn). Tween cũ của thẻ bị kill trước khi tạo tween mới, nên bấm đổi liên tục không cộng dồn và không trôi.
- Thẻ hết lượt: tắt raycast ngay, chạy animation rồi tắt object. Các thẻ còn lại đợi `removeDuration` rồi mới dời về bố cục mới.
- Tài nguyên gameplay đã cập nhật trước đó; animation chỉ thể hiện kết quả.
- Restart / vào màn: `Show(reset: true)` kill mọi tween, tăng `generation` (callback cũ bị bỏ qua), đặt thẳng vào bố cục.
- `OnDisable` / `OnDestroy` cũng kill tween. Tween gắn `SetLink(gameObject)`.
- Ngoài Play Mode (test EditMode) không tạo tween.

## 5. Input và vòng đời

- Thẻ nhận chạm qua `IPointerClickHandler` (chuột trái hoặc chạm; chuột phải/giữa bị bỏ qua).
- `BoardInput` coi con trỏ trên thẻ là "trên UI", nên chạm thẻ **không xuyên xuống bàn cờ**.
- Gốc `Hand` không có graphic. Thẻ không có trên tay bị tắt và tắt `blocksRaycasts`, nên tay rỗng không chặn gì.
- Chống chạm lặp:
  - Chỉ nhận một lần chạm thẻ mỗi frame (nhiều ngón cùng lúc).
  - Bỏ qua lần chạm cách lần trước < `pressCooldown`.
  - Thẻ đang biến mất không nhận chạm.
- Luật vẫn do `TurnInputController` chặn: pha địch (`Busy`), sau thắng/thua, đã dùng phụ trợ lượt này. Ngoài ra `GameController` bỏ qua thẻ khi đang rời scene.
- Restart khôi phục tài nguyên và tay bài đầu màn, bỏ lựa chọn, preview và tween cũ. Next/Back/Level Select tạo phiên và input mới nên không mang thẻ hoặc bộ đếm sang màn khác.

## 6. Cách chạy

1. Focus Unity 6000.3.8f1, chờ import + compile xong. Mở **Tools > Demigiant > DOTween Utility Panel** (thường tự bật lần đầu) → **Setup DOTween…** → Apply với module mặc định. Console không được có lỗi đỏ.
2. **Bắt buộc dựng lại gameplay scene:** **Hero Escape > Milestone 01 > Dựng scene chơi thử** → **Dựng lại**. Scene cũ vẫn còn nút Dash/Stasis kiểu cũ và chưa có `HandView`; nếu chưa dựng lại, Console báo `No HandView assigned`.
3. Mở `Assets/HeroEscape/Scenes/LevelSelect.unity` → Play. Thử Level 02–05 (Level 01 không có thẻ).
4. Game view: thử 1080x1920 (9:16) và 1080x2400 (9:20). Có thể dùng Device Simulator để xem safe area.

## 7. Kết quả kiểm tra

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M08-C1 | Compile Core, Search, Unity (+ `DOTween.dll`), Editor và toàn bộ test (kể cả test cần UnityEngine) | Passed (ngoài Unity) | Roslyn + DLL Unity 6000.3.8f1, C# 9. Chỉ cảnh báo CS0649 như cũ. |
| M08-C2 | Module `.cs` của DOTween 1.3.030 compile với DLL Unity 6000.3.8f1 | Passed (ngoài Unity) | Cùng công cụ. Unity thật vẫn cần xác nhận ở M08-U1. |
| M08-T1 | 221 test EditMode không cần UnityEngine | Passed (ngoài Unity) | 221 passed, 0 failed (194 cũ + 27 mới trong `CardHandTests`). |
| M08-T2 | `UnityHandViewTests` (6 test: bố cục, thứ tự vẽ, nâng/hạ, loại thẻ, tay rỗng không có raycast target, reset, mờ) | Not run | Cần Unity Test Runner; đã compile. |
| M08-U1 | Unity import DOTween, Setup DOTween, compile trong Editor không lỗi | Not run | Editor đang mở project nên không chạy batchmode. |
| M08-S1 | Dựng lại gameplay scene bằng builder | Not run | |
| M08-P1 | Checklist Play Mode (mục 8) | Not run | |
| M08-D1 | Kiểm thử trên điện thoại thật | Not run | |

`CardHandTests` (chạy ngoài Unity):
- Level 01–05 cấp đúng thẻ/số lần dùng theo file level.
- Chọn/đổi/bỏ chọn và Escape không đổi `GameState`/`Revision`.
- Đổi thẻ nhanh 9 lần: luôn ≤ 1 thẻ được chọn; highlight khớp chế độ.
- Mục tiêu sai không trừ, giữ lựa chọn. Click trên vùng thẻ (`pointerOverUi`) không tới bàn cờ.
- Dash đúng trừ đúng 1. Dash cuối làm thẻ Hero rời tay; Hero vẫn Move/Wait.
- Stasis xong thì rời tay, không tăng lượt, không chạy pha địch, vẫn có hành động chính. Đã dùng phụ trợ lượt này thì thẻ còn lượt hiện mờ.
- Pha địch / thua chặn thẻ.
- Restart và đổi màn khôi phục đúng tay bài.
- Preview khớp thực thi cho mọi ô đích Dash ở Level 02 và Level 05 (Tactical).
- Bố cục: Hero ở giữa và to 115%; cặp Hero + phụ trợ; một phụ trợ ở giữa và thẳng; tay rỗng; hai phụ trợ đối xứng; nâng theo trục; hàm thuần không cộng dồn; thứ tự vẽ.

## 8. Checklist nghiệm thu (Play Mode và điện thoại)

1. Level 01: không có thẻ, không có vùng trống dưới đáy, Move/Wait như cũ.
2. Level 02: một thẻ HERO "Dash · 2 uses left" ở giữa, thụt một phần dưới mép, đọc được tên và số lần dùng.
3. Level 03–05: Hero ở giữa, Stasis bên phải nghiêng ra ngoài; Hero to hơn.
4. Chạm Hero → nâng thẳng, viền vàng, đọc được mô tả. Highlight đổi sang ô đích Dash. Chạm lại → hạ, về highlight Move.
5. Chạm Stasis khi đang chọn Hero → Hero hạ, Stasis nâng (giữ góc nghiêng). Highlight chỉ còn X hợp lệ.
6. Bấm đổi thẻ thật nhanh hoặc double tap: thẻ không trôi, cuối cùng đúng một thẻ (hoặc không thẻ nào) được chọn, highlight đúng.
7. Chọn Dash, chạm ô không highlight: không mất lượt, không mất Dash, thẻ vẫn nâng. Chạm ô đích: Hero Dash. Thẻ hạ về, còn `1 use left`.
8. Dash lần 2: thẻ Hero tụt xuống mờ dần rồi biến mất, sau đó Stasis trượt về giữa và thẳng. Hero vẫn Move/Wait.
9. Stasis lên X: X bị khóa, không có pha địch, thẻ Stasis biến mất, vẫn đi được ngay trong lượt đó.
10. Trong 0,25 s pha địch và sau thắng/thua: chạm thẻ không có tác dụng.
11. Restart sau khi đã dùng hết thẻ: thẻ trở lại đầy đủ, không thẻ nào đang chọn. Next/Back/Level Select sang màn khác: tay bài đúng của màn mới.
12. Chạm thẻ không làm Hero di chuyển. Vùng hai bên tay bài và vùng tay rỗng không chặn gì trên bàn cờ.
13. Desktop: Escape bỏ chọn thẻ. Hover thẻ không hiện preview trên bàn.
14. Điện thoại có thanh điều hướng/cử chỉ: phần thẻ nhìn thấy chạm được, không bị thanh hệ thống che. Thẻ nâng lên không che nút Wait/Restart và các ô bàn cờ.
15. Kiểm tra cả 9:16 và 9:20: bàn cờ không bị panel che, ô vẫn dễ chạm.

## 9. File chính

- Mới: `Scripts/Unity/CardHand.cs`, `HandLayout.cs` (thuần C#), `CardView.cs`, `HandView.cs`; `Tests/EditMode/CardHandTests.cs`, `UnityHandViewTests.cs`; `Assets/Plugins/Demigiant/DOTween/**`.
- Sửa:
  - `GameController.cs`: nối thẻ, reset tay bài khi vào màn/Restart.
  - `HudView.cs`: bỏ nút Dash/Stasis, thêm `SetHandSpace`.
  - `UiText.cs`: chữ trên thẻ, hint Level 02.
  - `M01SceneBuilder.cs`: panel dưới mới, dựng tay bài.
  - 3 asmdef.

## 10. Hạn chế

- Hint của Level 02–05 bị thẻ đang chọn che (thẻ đã có mô tả riêng).
- Bàn cờ ở 9:16 nhỏ hơn M07 khoảng 12–13% ở màn có thẻ để chừa chỗ cho tay bài.
- Text trong game vẫn dùng chữ "Click" như các milestone trước.
- Không có hover peek cho thẻ trên desktop.
