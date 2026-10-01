# Technical Decisions — Hero Escape Prototype

Ngày chốt: 2026-10-01 • Người quyết định: Game Designer (chủ dự án)
Áp dụng cho: `TDD_Hero_Escape_Prototype_v0_1.docx` (baseline GDD v0.1) và `Art_Guide_Hero_Escape_Prototype_v0_2 (1).docx`.

## Phạm vi hiệu lực

Tài liệu này **thay thế đúng hai phần** của TDD v0.1:

1. Phiên bản Unity (TDD mục 1 "Nguồn kiểm soát thiết kế", mục 2, mục 12 "Build thử", mục 14 "M3 Hạ tầng phát triển", mục 16 T1).
2. Render pipeline (TDD mục 2, dòng "Built in Render Pipeline"; và Art Guide mục 10 "Target", phần nói Built-in Render Pipeline).

**Mọi phần khác của TDD v0.1, GDD v0.1 và Art Guide v0.2 giữ nguyên hiệu lực**: luật chơi, kiến trúc assembly, dữ liệu, AI, preview/Undo/replay, solver, Git/Fork/LFS, scope, kiểm thử, manifest 26 PNG, quy cách import. Khi có mâu thuẫn ở hai điểm trên, tài liệu này được ưu tiên; ở mọi điểm khác, GDD quyết định gameplay và TDD quyết định kỹ thuật như cũ.

## D1 — Phiên bản Unity: 6000.3.8f1

| Hạng mục | Trước (TDD v0.1) | Quyết định mới |
|---|---|---|
| Editor | Unity 6000.6.3f1 | **Unity 6000.3.8f1** (revision `1c7db571dde0`), đúng `ProjectSettings/ProjectVersion.txt` |
| Nâng/hạ phiên bản | Không tự nâng | **Không tự đổi phiên bản** theo bất kỳ hướng nào. Mọi thay đổi editor cần quyết định mới được ghi vào tài liệu này. |
| Known issues | Release notes 6000.6.3f1 [T1] | Tham chiếu release notes của **6000.3.8f1**; chỉ áp workaround khi tái hiện được trên dự án. |

Hệ quả:
- Dùng API, compiler và package do Package Manager giải quyết cho 6000.3.8f1. Phiên bản package ghi trong `Packages/packages-lock.json` và `Docs/Environment.md`.
- Art Guide trích tài liệu import sprite của Unity 6.6; khi khác biệt, theo hành vi thực tế của 6000.3.8f1 và ghi chú lại.

## D2 — Render pipeline: giữ URP 2D, sprite Unlit

| Hạng mục | Trước (TDD v0.1) | Quyết định mới |
|---|---|---|
| Pipeline | Built-in Render Pipeline, chưa thêm URP | **Giữ URP 2D** đang có (`com.unity.render-pipelines.universal` 17.3.0, `Assets/Settings/UniversalRP.asset`, `Renderer2D.asset`) |
| Vật liệu sprite | SpriteRenderer mặc định | SpriteRenderer dùng **material Sprite Unlit** (`Sprite-Unlit-Default` của URP) cho bàn, actor, overlay |
| Ánh sáng 2D | Không cần | **Không dùng**: không thêm Light2D cho gameplay, không normal map, không shadow caster |
| Post-processing | Không cần | **Không dùng**: không bật post-processing trên camera, không thêm Volume override |

Giữ nguyên:
- Phạm vi gameplay theo GDD v0.1.
- Số lượng và quy cách asset theo Art Guide v0.2 (26 PNG, PPU, pivot, 9-slice). Không tạo normal map, secondary texture hoặc asset ánh sáng.
- uGUI + TextMeshPro cho UI, Input System, C# thuần cho luật như TDD mục 2–3.

Ghi chú triển khai (không đổi luật):
- Asset mặc định của template URP 2D (`DefaultVolumeProfile.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `Lit2DSceneTemplate`, `URP2DSceneTemplate`) được giữ nguyên, không xóa ở bước này.
- Khi tạo scene Gameplay: dùng material Unlit cho sprite; nếu template sinh Global Light 2D thì không phụ thuộc vào nó cho hiển thị.

## D3 — Quản lý repo và package

- Project Unity nằm tại `My project (1)/` trong repo. **Chưa đổi tên thư mục project.**
- **Chưa gỡ hoặc nâng cấp package.** Các package thừa so với danh sách tối thiểu của TDD (2D animation/aseprite/psdimporter/spriteshape/tilemap, visualscripting, timeline, collab-proxy, multiplayer.center, ide.visualstudio) được giữ nguyên cho đến khi có quyết định riêng.
- `.gitignore` của project nằm tại `My project (1)/.gitignore`; `Packages/manifest.json` và `Packages/packages-lock.json` phải được commit. `.gitattributes` ở gốc repo quy định LFS cho nguồn nhị phân lớn; script, scene, prefab, asset YAML và `.meta` luôn là text, không vào LFS.

## D4 — Quy ước triển khai từ Milestone 01

Áp dụng từ Milestone 01 (`Docs/Milestone01.md`). Không đổi luật GDD.

- **Tọa độ:** `GridPos(X, Y)`, gốc (0,0) ở góc dưới trái, X tăng sang phải, Y tăng lên trên; **N = Y + 1**. Thứ tự hướng phá hòa: N, NE, E, SE, S, SW, W, NW. Quy ước này thay cách ghi row/col của TDD mục 6; ý nghĩa hướng giữ nguyên.
- **Dữ liệu level M01:** file text (`.txt`, đọc bằng `LevelParser` trong Core). Quyết định ScriptableObject của TDD mục 2/4 được xem lại khi có nhiều màn.
- **Art M01:** 5 Sprite asset trong `Assets/Art` (cắt từ `artboard.png`), không phải PNG riêng như manifest Art Guide; scale hiển thị tính trong `BoardView`, không đổi PPU/GUID.
- **Chữ UI tạm:** `UnityEngine.UI.Text` + font tích hợp `LegacyRuntime.ttf` cho tới khi có font tiếng Việt được bàn giao (Art Guide mục 6).

## D5 — Ngôn ngữ trong game và cấu hình Dash (từ Milestone 02)

- **Text hiển thị trong game bằng tiếng Anh**, gom trong `Assets/HeroEscape/Scripts/Unity/UiText.cs`. Không có lựa chọn ngôn ngữ hoặc localization package. Thuật ngữ: Wait, Restart, Your Turn, Enemy Turn, You Win!, You Lose!, Next Level, Back to Level 01, Dash. Tài liệu, comment và báo cáo cho Game Designer vẫn bằng tiếng Việt.
- **Dash cấu hình theo level:** khóa `dash: N` trong file level (mặc định 0 = không có Dash). Bước lời giải `DASH-<hướng>`.
- Trạng thái chọn hướng Dash là trạng thái giao diện (`TurnInputController`), không nằm trong `GameState`; số lần Dash còn lại nằm trong `GameState`.

## Lịch sử

| Ngày | Thay đổi |
|---|---|
| 2026-10-01 | Tạo tài liệu: D1 Unity 6000.3.8f1, D2 URP 2D Unlit, D3 quản lý repo/package. |
| 2026-10-01 | Thêm D4: quy ước tọa độ, dữ liệu level, art và chữ UI từ Milestone 01. |
| 2026-10-01 | Thêm D5: text trong game tiếng Anh, cấu hình Dash theo level (Milestone 02). |
