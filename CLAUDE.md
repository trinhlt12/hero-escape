# CLAUDE.md — Hero Escape Prototype

Trả lời người dùng bằng **tiếng Việt**. Người dùng là Game Designer, chủ dự án.

## Nguồn sự thật (đọc trước khi làm việc)

Thứ tự ưu tiên khi có mâu thuẫn:

1. **`Docs/TechnicalDecisions.md`**: quyết định kỹ thuật đã chốt. Tài liệu này **thay thế phần phiên bản Unity và render pipeline của TDD v0.1**.
2. **`Docs/GDD_Hero_Escape_Prototype_v0_1 (1).docx`**: quyết định mọi luật gameplay và scope.
3. **`Docs/TDD_Hero_Escape_Prototype_v0_1.docx`**: kiến trúc, dữ liệu, AI, preview/Undo, solver, Git, kiểm thử. Áp dụng đầy đủ, **trừ** hai điểm đã bị thay ở mục 1.
4. **`Docs/Art_Guide_Hero_Escape_Prototype_v0_2 (1).docx`**: manifest 26 PNG, quy cách import, hiển thị trạng thái.
5. **`Docs/Environment.md`**: phiên bản công cụ thực tế và kết quả kiểm tra môi trường (Passed / Failed / Not run).
6. **`Docs/Milestone01.md`** … **`Docs/Milestone04.md`**: phạm vi khóa và tiến độ từng milestone (Milestone04 là mới nhất). Không triển khai tính năng ngoài phạm vi milestone khi chưa có yêu cầu mới.

Text hiển thị trong game bằng **tiếng Anh** (gom trong `UiText.cs`); trả lời Game Designer bằng tiếng Việt.

File `.docx` là nhị phân. Muốn đọc, giải nén `word/document.xml` (PowerShell `System.IO.Compression`) vào scratchpad, giữ cả các bảng. Không tự đoán nội dung tài liệu.

## Cấu hình đã chốt — KHÔNG dùng cấu hình cũ của TDD

- **Unity 6000.3.8f1**, không phải 6000.6.3f1. Không tự nâng hoặc hạ phiên bản. Máy còn cài 6000.5.8f1; không dùng bản đó cho project này.
- **URP 2D** (giữ nguyên), không phải Built-in. Sprite dùng material **Unlit**. Không dùng Light2D cho gameplay, không normal map, không post-processing.
- Giữ nguyên scope gameplay (GDD v0.1) và số lượng asset (Art Guide v0.2).

## Cấu trúc repo

- Project Unity: `My project (1)/` (gồm Assets, Packages, ProjectSettings). **Không đổi tên thư mục này** khi chưa có quyết định.
- Code dự án theo TDD mục 13: `My project (1)/Assets/HeroEscape/`:
  - `Scripts/Core` (`HeroEscape.Core`, không tham chiếu UnityEngine), `Scripts/Search`, `Scripts/Unity`, `Scripts/Editor`
  - `Tests/EditMode`, `Data/Levels`, `Scenes`
- Art: `My project (1)/Assets/Art/` (Sprite asset cắt từ `artboard.png`). Không di chuyển, đổi tên hay ghi đè art gốc.
- Tài liệu: `Docs/`.
- `.gitignore` của Unity nằm tại `My project (1)/.gitignore`. `.gitignore` và `.gitattributes` (LFS) nằm ở gốc repo.

## Quy tắc làm việc

- Không tự commit hoặc push khi người dùng chưa yêu cầu. Không force push `main`.
- Không gỡ hoặc nâng cấp package khi chưa có quyết định. `Packages/manifest.json` và `packages-lock.json` phải được commit.
- Luôn commit asset kèm `.meta`. Không đưa `.cs`, `.meta`, scene, prefab, YAML hoặc JSON vào Git LFS.
- Không xóa file hoặc ghi đè thay đổi sẵn có của người dùng.
- Nếu Unity Editor đang mở project (có `My project (1)/Temp/UnityLockfile`), không chạy Unity batchmode trên cùng project.
- Thay đổi luật phải cập nhật GDD/TDD trước, rồi tăng `rulesVersion` (TDD mục 1). Thay đổi quyết định kỹ thuật phải ghi vào `Docs/TechnicalDecisions.md`.
- Khi báo cáo kiểm thử, bước chưa chạy là **Not run**, không được coi là đạt.
