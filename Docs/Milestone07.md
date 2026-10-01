# Milestone 07 — Màn chọn Level (Level Select)

Ngày: 2026-10-01 • Trạng thái: **Đã có code và test; chờ dựng scene và kiểm tra Play Mode**

**Level 05 (Tactical AI) đã được Game Designer nghiệm thu** (báo cáo 2026-10-01). Không đổi thiết kế, thuật toán, cách chấm điểm Tactical hay bố cục của Level 01–05.

## 1. Phạm vi

- Scene **`LevelSelect`** riêng làm điểm bắt đầu: tiêu đề `Select Level`, 5 nút xếp một cột, **tất cả mở sẵn**.
- Click nút → ghi nhận ID màn → nạp gameplay scene hiện có (`M01_TestLevel`, dùng chung cho cả 5 màn) → tạo phiên chơi mới. Không Confirm.
- Trong gameplay có nút **`Level Select`**: về màn chọn ngay, không popup. Bị khóa trong lúc đang xử lý pha địch.
- Giữ nguyên Restart, Next Level, Back to Level trước; Level 05 không có Next Level.
- Out-of-scope: level mới, chỉnh AI/cân bằng, save/load, resume, khóa màn, sao, điểm, phần thưởng, chọn Hero/loadout, settings, âm thanh, localization, Undo/Confirm/Cancel, backend, build Android.

## 2. Catalog và dữ liệu

`Assets/HeroEscape/Data/LevelCatalog.asset` (`LevelCatalog`, ScriptableObject) dùng chung cho Level Select và gameplay:

| ID (trùng `id:` trong file level) | Tên | Mô tả | File level |
|---|---|---|---|
| `M01_TEST` | Level 01 | Basic Movement | `M01_TestLevel.txt` |
| `M02_DASH` | Level 02 | Dash | `M02_DashLevel.txt` |
| `M03_STASIS` | Level 03 | Stasis | `M03_StasisLevel.txt` |
| `M04_TWO_ENEMIES` | Level 04 | Two Enemies | `M04_TwoEnemiesLevel.txt` |
| `M05_TACTICAL` | Level 05 | Tactical Enemy | `M05_TacticalLevel.txt` |

- Định danh là ID, không phải tên hiển thị. Khi nạp màn, `GameController` kiểm tra ID của catalog trùng `id:` của file level; sai thì báo lỗi rõ.
- Bố cục, Dash/Stasis, profile AI và X01 chỉ nằm trong file level; UI không chép cấu hình.
- Catalog còn chứa: màn mặc định (`M01_TEST`), tên gameplay scene (`M01_TestLevel`) và tên scene chọn màn (`LevelSelect`).
- Builder chỉ **tạo** catalog khi chưa có; catalog đã có thì giữ nguyên chỉnh sửa của người dùng.

**Truyền giữa scene:** `LevelSelection` (lớp tĩnh, C# thuần) chỉ giữ **ID màn** cần mở.
- Gameplay đọc một lần rồi xóa (`TakePending`).
- `LevelSelectionReset` xóa nó khi bắt đầu mỗi lần Play (`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`), nên không sót lựa chọn của phiên Play trước kể cả khi tắt Domain Reload.
- Không dùng `DontDestroyOnLoad`; bàn cờ và GameSession không sống qua scene.

**Mở thẳng gameplay scene:** không có ID chờ thì mở màn mặc định của catalog (Level 01). ID lạ thì cảnh báo và mở màn mặc định.

## 3. Vòng đời scene và input

- Mỗi lần vào màn (từ Level Select, Next, Back): `GameSession`, `TurnInputController` và AI mới (Basic/Tactical theo file level), bàn cờ dựng lại; khóa, thắng/thua, chế độ chọn và preview cũ bị xóa. Ngoài ra scene gameplay được nạp lại hoàn toàn khi đi qua Level Select.
- Chuyển scene ở chế độ Single: mỗi scene có đúng một EventSystem; mọi object và callback của scene cũ bị hủy. `GameController`, `HudView`, `LevelSelectView` hủy đăng ký event trong `OnDestroy`.
- **Chặn nạp trùng:**
  - Level Select khóa mọi nút sau click đầu tiên.
  - Nút Level Select trong gameplay có cờ `leavingScene` và bị vô hiệu khi đang xử lý pha địch.
- **Không để click chọn màn thành nước đi:** `BoardInput` bỏ qua con trỏ cho tới khi nút chuột được thả ra lần đầu sau khi scene mở; Board chỉ phản ứng với lần nhấn mới.
- Cache preview/highlight gắn với phiên chơi mới (epoch màn + revision); AI Tactical được tạo mới mỗi lần vào màn.

## 4. UI

- Level Select:
  - Canvas 1080x1920 (Expand), safe area.
  - Tiêu đề 64 đậm.
  - 5 nút cao 220, cách nhau 28 (tổng 1212 đơn vị). Tên màn 56 đậm, mô tả 38.
  - Nền và nút màu trơn của uGUI, font tích hợp như các màn khác; không art mới.
- Gameplay: hàng điều hướng giờ có tối đa 3 nút `Next Level` (240) • `Back to Level 0x` (330) • `Level Select` (260), chữ 36, tổng 890/1000. Hàng hành động, bàn cờ và panel trên không đổi.

## 5. Cách chạy

1. Focus Unity, chờ compile xong.
2. Chạy **Hero Escape > Milestone 01 > Dựng scene Level Select**: tạo `LevelCatalog.asset`, scene `Assets/HeroEscape/Scenes/LevelSelect.unity`, và đưa LevelSelect lên đầu Build Settings.
3. Chạy **Hero Escape > Milestone 01 > Dựng scene chơi thử** → **Dựng lại**: gameplay scene nối với catalog và có nút Level Select. Bắt buộc, vì scene cũ còn danh sách màn kiểu cũ nên sẽ báo "No level catalog assigned".
4. Mở **`Assets/HeroEscape/Scenes/LevelSelect.unity`** (hoặc menu **Mở scene Level Select**) rồi bấm Play để chạy toàn bộ luồng.

Build Settings sau khi dựng: `LevelSelect` (bật) → `M01_TestLevel` (bật) → `SampleScene` (giữ trạng thái tắt như cũ). Chưa build Android.

## 6. Kết quả kiểm tra

| # | Kiểm tra | Trạng thái | Bằng chứng |
|---|---|---|---|
| M07-C1 | Compile Core, Search, Unity, Editor và toàn bộ test | Passed (ngoài Unity) | Roslyn + DLL Unity 6000.3.8f1, C# 9. |
| M07-T1 | 194 test EditMode không cần UnityEngine | Passed (ngoài Unity) | 194 passed, 0 failed (186 cũ + 8 mới trong `LevelSelectionTests`). |
| M07-T2 | `UnityLevelCatalogTests` (catalog 5 màn, ID trùng file level, tên/mô tả, profile, màn mặc định, Build Settings) | Not run | Cần Unity Test Runner sau khi dựng scene; đã compile. |
| M07-S1 | Dựng LevelSelect và dựng lại gameplay scene | Not run | |
| M07-P1 | Toàn bộ checklist Play Mode | Not run | Chưa playtest. |

`LevelSelectionTests`: ID được đọc một lần rồi xóa; không chọn thì dùng mặc định; reset khi bắt đầu Play; mỗi màn trong 5 màn khởi tạo phiên mới đúng vị trí, số X, profile AI, Dash/Stasis, không khóa, vòng 1.

## 7. Lỗi đã sửa sau bàn giao

- **Triệu chứng:** `[LevelSelect] Missing level catalog...` khi Play. Field `catalog` của `LevelSelectView` và `GameController` lưu thành `{fileID: 0}` trong cả hai scene, dù `LevelCatalog.asset` đầy đủ 5 màn.
- **Nguyên nhân:** builder nạp catalog **trước** `EditorSceneManager.NewScene(…, Single)`. Tạo scene mới giải phóng asset không còn được dùng (Editor.log: `Unloading … unused Assets` ngay trước dòng "Đã dựng"), nên object catalog đã bị giải phóng lúc gán vào scene. Sprite và material không bị ảnh hưởng.
- **Sửa:**
  - Cả hai builder nạp lại catalog sau `NewScene` (`M01SceneBuilder.ReloadCatalog`).
  - `Wire` kiểm tra lại sau khi gán: reference truyền vào mà bị rỗng thì báo lỗi và **không lưu scene**.
- **Cần làm lại:** dựng lại cả hai scene bằng menu (mục 5).

## 8. Hạn chế

- Tên menu vẫn nằm dưới "Milestone 01" để giữ đường dẫn quen thuộc.
- Gameplay scene vẫn tên `M01_TestLevel` (giữ GUID và Build Settings).
- Không có hiệu ứng chuyển scene (đúng phạm vi).
