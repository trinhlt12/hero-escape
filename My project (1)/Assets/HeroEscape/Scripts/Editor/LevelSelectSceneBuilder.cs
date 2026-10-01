using System.IO;
using HeroEscape.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using B = HeroEscape.EditorTools.M01SceneBuilder;

namespace HeroEscape.EditorTools
{
    /// <summary>
    /// Dựng scene LevelSelect: tiêu đề "Select Level" và một cột nút tạo từ LevelCatalog khi chạy.
    /// Luôn tạo scene mới từ đầu (không sinh UI trùng); scene đã tồn tại thì hỏi trước khi ghi đè.
    /// Cập nhật Build Settings: LevelSelect đứng trước gameplay scene, giữ các scene khác.
    /// </summary>
    public static class LevelSelectSceneBuilder
    {
        const string MenuRoot = "Hero Escape/Milestone 01/";

        [MenuItem(MenuRoot + "Dựng scene Level Select", priority = 2)]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                B.Info("Hãy thoát Play Mode trước khi dựng scene.");
                return;
            }

            LevelCatalog catalog = B.EnsureCatalog(out string catalogError);
            if (catalog == null)
            {
                B.Info(catalogError);
                return;
            }

            if (File.Exists(B.LevelSelectScenePath))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Scene Level Select đã tồn tại",
                    $"{B.LevelSelectScenePath} đã có.\n\n\"Dựng lại\" sẽ tạo lại scene từ đầu. Mọi chỉnh sửa tay trong scene này sẽ mất.",
                    "Dựng lại", "Hủy", "Chỉ mở scene");
                if (choice == 1) return;
                if (choice == 2)
                {
                    OpenScene();
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene (Single) giải phóng asset không còn được dùng, kể cả catalog nạp trước đó: nạp lại sau khi tạo scene.
            catalog = B.ReloadCatalog();
            LevelSelectView view = Populate(catalog);

            B.EnsureFolder(Path.GetDirectoryName(B.LevelSelectScenePath));
            if (!EditorSceneManager.SaveScene(scene, B.LevelSelectScenePath))
            {
                B.Info("Không lưu được scene. Xem Console.");
                return;
            }

            B.EnsureBuildSettings();
            Selection.activeObject = view.gameObject;
            Debug.Log($"[M01] Đã dựng {B.LevelSelectScenePath} và cập nhật Build Settings. Bấm Play ở scene này để chạy toàn bộ luồng.");
        }

        [MenuItem(MenuRoot + "Mở scene Level Select", priority = 3)]
        public static void OpenScene()
        {
            if (!File.Exists(B.LevelSelectScenePath))
            {
                B.Info("Chưa có scene. Chạy \"Hero Escape > Milestone 01 > Dựng scene Level Select\" trước.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(B.LevelSelectScenePath, OpenSceneMode.Single);
        }

        static LevelSelectView Populate(LevelCatalog catalog)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = B.Background;
            cameraGo.AddComponent<AudioListener>();

            // Một EventSystem cho scene này; chuyển scene ở chế độ Single nên không bị trùng.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("LevelSelectUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            RectTransform safeArea = B.CreateRect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            Text title = B.CreateText("Title", safeArea, 64, Color.white, FontStyle.Bold, new Vector2(40f, -210f), new Vector2(-40f, -70f),
                UiText.SelectLevelTitle);
            RectTransform messageRect = B.CreateRect("Message", safeArea, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, 30f), new Vector2(-40f, 100f));
            Text message = B.ConfigureText(messageRect.gameObject.AddComponent<Text>(), 32, B.Highlight, FontStyle.Normal, string.Empty);

            // Cột nút: 5 x 220 + 4 x 28 = 1212 đơn vị, nằm gọn trong vùng 1920 - 240 - 120 ở 9:16 (và rộng hơn ở 9:20).
            RectTransform list = B.CreateRect("LevelList", safeArea, Vector2.zero, Vector2.one, new Vector2(80f, 120f), new Vector2(-80f, -240f));
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 28f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            Button template = CreateLevelButtonTemplate(list);

            var view = canvasGo.AddComponent<LevelSelectView>();
            B.Wire(view, ("catalog", catalog), ("listRoot", list), ("buttonTemplate", template), ("titleText", title), ("messageText", message));
            return view;
        }

        /// <summary>Nút mẫu (tắt sẵn): nền màu trơn của uGUI, tên màn nổi bật ở trên, mô tả nhỏ hơn ở dưới.</summary>
        static Button CreateLevelButtonTemplate(Transform list)
        {
            var rect = new GameObject("LevelButtonTemplate", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(list, false);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 220f;
            element.minHeight = 220f;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = B.HeroBlue;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;

            RectTransform nameRect = B.CreateRect("Name", rect, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(30f, 0f), new Vector2(-30f, -10f));
            B.ConfigureText(nameRect.gameObject.AddComponent<Text>(), 56, Color.white, FontStyle.Bold, "Level 00");
            RectTransform descriptionRect = B.CreateRect("Description", rect, new Vector2(0f, 0f), new Vector2(1f, 0.45f), new Vector2(30f, 10f), new Vector2(-30f, 0f));
            B.ConfigureText(descriptionRect.gameObject.AddComponent<Text>(), 38, B.SoftText, FontStyle.Normal, "Description");

            rect.gameObject.SetActive(false);
            return button;
        }
    }
}
