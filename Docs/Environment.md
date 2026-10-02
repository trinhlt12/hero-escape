# Environment — Hero Escape Prototype

Mốc khởi tạo môi trường theo TDD v0.1 mục 2 và mục 16 ("Checklist khởi tạo"), đã điều chỉnh theo `Docs/TechnicalDecisions.md`.

- Ngày kiểm tra: 2026-10-01
- Máy: Windows 11 Pro 10.0.26200 (máy phát triển của Game Designer)
- Repo: `D:\Projects\hero-escape` • Project Unity: `My project (1)/` • Nhánh `main`, chưa có commit

Quy ước trạng thái: **Passed** = đã chạy hoặc quan sát trực tiếp và đạt. **Failed** = đã chạy và không đạt. **Not run** = chưa chạy; **không** được coi là đạt.

## 1. Phiên bản công cụ

| Công cụ | Phiên bản ghi nhận | Nguồn xác minh |
|---|---|---|
| Unity Editor | 6000.3.8f1 (`1c7db571dde0`) | `ProjectSettings/ProjectVersion.txt`; header `Editor.log` |
| Unity Hub | 3.20.1 | Danh sách ứng dụng đã cài của Windows |
| Unity Editor khác trên máy | 6000.5.8f1 (đang mở project khác, **không** dùng cho Hero Escape) | `C:\Program Files\Unity\Hub\Editor` |
| JetBrains Rider | 2026.2.0.2 (build 262.8665.400) | `rider64.exe` |
| Fork | 2.23.2 | `Fork.exe` |
| Git | 2.55.0.windows.4 | `git --version` |
| Git LFS | 3.7.1 | `git lfs version` |

## 2. Package (theo `Packages/packages-lock.json`)

| Package | Phiên bản | Yêu cầu TDD |
|---|---|---|
| com.unity.inputsystem | 1.18.0 | Tối thiểu — có |
| com.unity.ugui (gồm TextMeshPro) | 2.0.0 | Tối thiểu — có |
| com.unity.ide.rider | 3.0.39 | Tối thiểu — có |
| com.unity.test-framework | 1.6.0 | Tối thiểu — có |
| com.unity.render-pipelines.universal | 17.3.0 | Theo TechnicalDecisions D2 |
| com.unity.render-pipelines.core | 17.3.0 | Phụ thuộc của URP |
| com.unity.burst | 1.8.27 | Phụ thuộc gián tiếp |

Các package thêm từ template URP 2D được giữ nguyên theo TechnicalDecisions D3.

**Plugin ngoài Package Manager** (thêm ở Milestone 08, TechnicalDecisions D11):

| Plugin | Phiên bản | Nguồn | Vị trí |
|---|---|---|---|
| DOTween (bản miễn phí) | 1.3.030 (2026-06-23) | `https://dotween.demigiant.com/downloads/DOTween_1_3_030.zip`, SHA-256 `62a0ececd274e1587eb0dea15f3afab392fbda5a0f8cac7287fbf7f64925a1ba` | `Assets/Plugins/Demigiant/DOTween/` (không gồm `*.dll.mdb`) |

Kiểm tra: E17 (import + Setup DOTween trong Editor) — **Not run**. Chi tiết ở `Docs/Milestone08.md`.

- SHA-256 `manifest.json`: `eadb2409e74e98314259257a36e15be39d62670f2c8e7b3dcd6fe80eb6388242`
- SHA-256 `packages-lock.json`: `d5652561baf9d7412c4917a17bd8067b41e959a81fd8539d73655175273c95e7`

## 3. Cấu hình project

| Mục | Giá trị | Trạng thái |
|---|---|---|
| Asset Serialization | Force Text (`m_SerializationMode: 2`) | Passed |
| Version Control mode | Visible Meta Files | Passed |
| Active Input Handling | Input System Package (New) (`activeInputHandler: 1`) | Passed |
| Render pipeline | URP 2D, `UniversalRP.asset` gán cho mọi Quality level | Passed (đúng D2) |
| Default orientation | Auto Rotation (`defaultScreenOrientation: 4`) | Not run — TDD yêu cầu khóa portrait khi build Android; chưa chỉnh |
| Product/Company name | `My project (1)` / `DefaultCompany` | Ghi nhận, chưa đổi |

## 4. Kết quả kiểm tra

| # | Bước | Trạng thái | Bằng chứng / ghi chú |
|---|---|---|---|
| E01 | Editor đúng patch 6000.3.8f1 đã cài | Passed | `C:\Program Files\Unity\Hub\Editor\6000.3.8f1\Editor\Unity.exe` |
| E02 | Android Build Support có SDK, NDK, OpenJDK | Passed (chỉ kiểm tra có file) | Thư mục `PlaybackEngines/AndroidPlayer/{SDK,NDK,OpenJDK}` tồn tại. Chưa build. |
| E03 | Package resolve | Passed | `packages-lock.json` có 65 mục; `Logs/Packages-Update.log` |
| E04 | Import asset lần đầu | Passed, có cảnh báo | `Editor.log` (phiên mở lúc 11:16, 2026-10-01): Asset Pipeline Refresh hoàn tất, không có lỗi importer. Xem R1, R2. |
| E05 | Compile script | Passed (chỉ package/template) | `Editor.log`: `*** Tundra build success (40.52 seconds), 1014 items updated`. Không có `error CS`. Project **chưa có script của dự án**, nên phép kiểm tra compile code Hero Escape vẫn chưa thực hiện. |
| E06 | Compile lại bằng batchmode | Not run | Project đang mở trong Editor (PID 31800, có `Temp/UnityLockfile`). Không chạy instance thứ hai để tránh xung đột lock. |
| E07 | Mở `SampleScene` và vào/thoát Play Mode, Console không lỗi | Not run | Cần thao tác trong Editor. |
| E08 | Rider là External Script Editor | Not run | Chưa xác minh trong Preferences. |
| E09 | Rider debugger attach vào Editor, dừng tại breakpoint | Not run | Cần thao tác thủ công. |
| E10 | Test Runner chạy EditMode | Not run | Chưa có test assembly. |
| E11 | Android Development build (IL2CPP ARM64, portrait) | Not run | Ngoài bước khởi tạo; cần thiết bị. |
| E12 | Font tiếng Việt cho TMP | Not run | Chưa có font được bàn giao (Art Guide mục 6). |
| E13 | Git LFS cài và hook repo | Passed | git-lfs 3.7.1; `git lfs update` đã cài hook `pre-push`, `post-checkout`, `post-commit`, `post-merge`. |
| E14 | `.gitignore` đúng cho project lồng | Passed | `git check-ignore`: Library/Temp/Logs/UserSettings/.sln/.csproj bị ignore; `Packages/manifest.json`, `packages-lock.json`, `Assets/**/*.dll`, `.meta`, thư mục `Assets/**/Debug|Build` **không** bị ignore. `git ls-files -ci --exclude-standard` rỗng (không file staged nào khớp ignore). |
| E15 | `.gitattributes` | Passed | `git check-attr`: `.unity`, `.meta`, `.cs`, `.prefab` là text, không LFS; `.psd`, `.wav`, `ArtSource/**/*.png` dùng LFS; `.png` trong Assets là binary thường. Cả 45 file staged đều LF nên `text=auto` không làm đổi nội dung index. |
| E16 | Build từ clean checkout | Not run | Chưa có commit. |
| E17 | DOTween 1.3.030: Unity import, Setup DOTween, compile trong Editor không lỗi | Not run | File đã chép vào `Assets/Plugins/Demigiant/DOTween`. Module `.cs` compile được ngoài Unity với DLL 6000.3.8f1. Editor đang mở project nên chưa chạy batchmode. |

## 5. Rủi ro môi trường quan sát được

- **R1. Smart App Control chặn DLL Burst JIT.** `Editor.log`: `Unexpected error in Burst compilation ... DllNotFoundException ... Library\BurstCache\JIT\*.dll, error code 4551` (ERROR_SYSTEM_INTEGRITY_POLICY_VIOLATION). Registry `CI\Policy\VerifiedAndReputablePolicyState = 1` cho thấy Smart App Control đang bật chế độ Enforce. Hiện tại không chặn công việc, vì luật Hero Escape là C# thuần và không dùng Burst (TDD mục 12). Nếu gặp lỗi Burst trong Play Mode hoặc build, xử lý theo quyết định riêng. Tắt Smart App Control trên Windows 11 không bật lại được nếu không cài lại Windows.
- **R2. Lỗi TLS khi Editor gọi mạng.** `Editor.log` có 6 dòng `Curl error 35: Cert verify failed ... UnityTls error code: 7`. Licensing vẫn kết nối thành công và package đã resolve. Nguyên nhân chưa xác định; có thể do phần mềm kiểm tra HTTPS hoặc proxy. Theo dõi nếu Package Manager hoặc Asset Store báo lỗi.
- **R3. Hai phiên bản Unity cùng cài.** Khi mở project từ Unity Hub, phải chọn **6000.3.8f1**. Không mở bằng 6000.5.8f1 để tránh tự nâng phiên bản (TechnicalDecisions D1).

## 6. Hướng dẫn thao tác thủ công (E07–E09)

Kết quả của từng bước được ghi lại vào bảng mục 4.

**E07 — Play Mode**
1. Trong Unity 6000.3.8f1 với project `My project (1)`, mở `Assets/Scenes/SampleScene.unity`.
2. Mở Console (`Window > General > Console`) và bấm **Clear**.
3. Bấm **Play**, chờ khoảng 3 giây, rồi bấm **Play** lần nữa để thoát.
4. Đạt khi Console không có dòng lỗi màu đỏ. Ghi lại cảnh báo nếu có.

**E08 — Rider làm External Script Editor**
1. Vào `Edit > Preferences > External Tools`.
2. Ở **External Script Editor**, chọn *Rider 2026.2.0.2*.
3. Bấm **Regenerate project files**.
4. Mở bằng `Assets > Open C# Project`. Rider phải mở solution `My project (1).sln` và không báo lỗi load project.

**E09 — Debug Rider**
1. Tạm tạo `Assets/_EnvCheck/EnvCheck.cs`, gắn vào một GameObject trong `SampleScene`:
   ```csharp
   using UnityEngine;
   public class EnvCheck : MonoBehaviour { void Start() { Debug.Log("EnvCheck"); } }
   ```
2. Trong Rider, đặt breakpoint ở dòng `Debug.Log` rồi bấm **Attach to Unity Editor** (biểu tượng con bọ trên toolbar, cấu hình *Attach to Unity Editor*).
3. Bấm Play trong Unity. Đạt khi Rider dừng ở breakpoint và Console in `EnvCheck` sau khi Continue.
4. Thoát Play Mode, **không lưu thay đổi scene**, rồi xóa thư mục `Assets/_EnvCheck` (cả file `.meta`). Script này chỉ dùng để kiểm tra, không commit.
5. Nếu Unity hỏi chế độ Debug, chọn *Switch to debug mode for this session*.
