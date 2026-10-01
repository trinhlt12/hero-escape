using System;
using System.IO;
using HeroEscape.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroEscape.EditorTools
{
    /// <summary>
    /// Dựng scene chơi thử Milestone 01 với đầy đủ object, component và reference.
    /// Luôn tạo scene mới từ đầu nên chạy lại không sinh object trùng; nếu scene đã tồn tại thì hỏi trước khi ghi đè.
    /// </summary>
    public static class M01SceneBuilder
    {
        const string MenuRoot = "Hero Escape/Milestone 01/";
        public const string ScenePath = "Assets/HeroEscape/Scenes/M01_TestLevel.unity";
        public const string LevelPath = "Assets/HeroEscape/Data/Levels/M01_TestLevel.txt";
        public const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        // Bảng màu theo Art Guide mục 2.
        static readonly Color Background = new Color32(0x17, 0x24, 0x2C, 0xFF);
        static readonly Color Panel = new Color32(0x0F, 0x19, 0x1F, 0xF2);
        static readonly Color HeroBlue = new Color32(0x24, 0x7F, 0xAD, 0xFF);
        static readonly Color Neutral = new Color32(0x52, 0x63, 0x6E, 0xFF);
        static readonly Color Highlight = new Color32(0xE8, 0xB8, 0x49, 0xFF);
        static readonly Color SoftText = new Color32(0xC8, 0xD3, 0xD9, 0xFF);

        // Kích thước UI theo canvas tham chiếu 1080 x 1920.
        const float TopPanelHeight = 300f;
        const float BottomPanelHeight = 360f;

        [MenuItem(MenuRoot + "Dựng scene chơi thử", priority = 0)]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Info("Hãy thoát Play Mode trước khi dựng scene.");
                return;
            }

            if (!M01ArtCheck.Run(out M01ArtCheck.ArtSet art))
            {
                Info("Thiếu sprite trong Assets/Art. Xem chi tiết trong Console.");
                return;
            }

            var levelFile = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelPath);
            var unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (levelFile == null || unlitMaterial == null)
            {
                Info("Không tìm thấy:\n" + (levelFile == null ? LevelPath + "\n" : "") + (unlitMaterial == null ? UnlitMaterialPath : ""));
                return;
            }

            if (File.Exists(ScenePath))
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "Scene M01 đã tồn tại",
                    $"{ScenePath} đã có.\n\n\"Dựng lại\" sẽ tạo lại scene từ đầu. Mọi chỉnh sửa tay trong scene này sẽ mất.",
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
            GameController controller = Populate(art, levelFile, unlitMaterial);

            EnsureFolder(Path.GetDirectoryName(ScenePath));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Info("Không lưu được scene. Xem Console.");
                return;
            }

            Selection.activeObject = controller.gameObject;
            Debug.Log($"[M01] Đã dựng {ScenePath}. Bấm Play để chơi thử.");
        }

        [MenuItem(MenuRoot + "Mở scene chơi thử", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                Info("Chưa có scene. Chạy \"Hero Escape > Milestone 01 > Dựng scene chơi thử\" trước.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static GameController Populate(M01ArtCheck.ArtSet art, TextAsset levelFile, Material unlitMaterial)
        {
            // Camera
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            cameraGo.AddComponent<AudioListener>();
            var fitter = cameraGo.AddComponent<CameraFitter>();

            // Bàn cờ
            var boardGo = new GameObject("Board");
            var board = boardGo.AddComponent<BoardView>();
            Wire(board,
                ("floorSprite", art.Floor), ("wallSprite", art.Wall), ("exitSprite", art.Exit),
                ("heroSprite", art.Hero), ("enemySprite", art.Enemy), ("spriteMaterial", unlitMaterial));

            // UI. Không gọi AssignDefaultActions() ở edit mode: hàm đó tạo action tạm trong bộ nhớ, không lưu được vào scene.
            // InputSystemUIInputModule tự gán action mặc định (Reset trong Editor hoặc OnEnable khi chạy).
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var hud = canvasGo.AddComponent<HudView>();

            RectTransform safeArea = CreateRect("SafeArea", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            RectTransform top = CreatePanel("TopPanel", safeArea, new Vector2(0f, 1f), new Vector2(1f, 1f), TopPanelHeight);
            Text title = CreateText("Title", top, 40, SoftText, FontStyle.Normal, new Vector2(40f, -90f), new Vector2(-40f, -20f));
            Text status = CreateText("Status", top, 58, Color.white, FontStyle.Bold, new Vector2(40f, -190f), new Vector2(-40f, -95f));
            Text notice = CreateText("Notice", top, 36, Highlight, FontStyle.Normal, new Vector2(40f, -290f), new Vector2(-40f, -195f));

            RectTransform bottom = CreatePanel("BottomPanel", safeArea, new Vector2(0f, 0f), new Vector2(1f, 0f), BottomPanelHeight);
            Text hint = CreateText("Hint", bottom, 32, SoftText, FontStyle.Normal, new Vector2(40f, -140f), new Vector2(-40f, -20f));
            Button wait = CreateButton("WaitButton", bottom, HeroBlue, new Vector2(-230f, 50f));
            Button restart = CreateButton("RestartButton", bottom, Neutral, new Vector2(230f, 50f));

            Wire(hud,
                ("titleText", title), ("statusText", status), ("noticeText", notice), ("hintText", hint),
                ("waitButton", wait), ("restartButton", restart));

            // Điều khiển
            var gameGo = new GameObject("Game");
            var controller = gameGo.AddComponent<GameController>();
            var input = gameGo.AddComponent<BoardInput>();
            Wire(controller, ("levelFile", levelFile), ("boardView", board), ("hud", hud));
            Wire(input, ("worldCamera", camera), ("board", board), ("controller", controller));
            Wire(fitter, ("board", board), ("topPanel", top), ("bottomPanel", bottom));

            return controller;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        static RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float height)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0.5f, anchorMin.y);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = Panel; // nhận raycast: click lên panel không xuống bàn cờ
            return rect;
        }

        /// <summary>Text neo theo cạnh trên của parent; offset tính từ cạnh trên.</summary>
        static Text CreateText(string name, Transform parent, int fontSize, Color color, FontStyle style, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), offsetMin, offsetMax);
            return ConfigureText(rect.gameObject.AddComponent<Text>(), fontSize, color, style);
        }

        static Text ConfigureText(Text text, int fontSize, Color color, FontStyle style)
        {
            // Font tích hợp của Unity (dynamic). Không có font tiếng Việt riêng trong project ở Milestone 01.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        static Button CreateButton(string name, Transform parent, Color color, Vector2 anchoredPosition)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(420f, 150f);
            rect.anchoredPosition = anchoredPosition;

            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            button.colors = colors;

            RectTransform labelRect = CreateRect("Label", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ConfigureText(labelRect.gameObject.AddComponent<Text>(), 50, Color.white, FontStyle.Bold);
            return button;
        }

        /// <summary>Gán field [SerializeField] private qua SerializedObject; báo lỗi rõ nếu sai tên field.</summary>
        static void Wire(Component target, params (string field, Object value)[] references)
        {
            var so = new SerializedObject(target);
            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property == null)
                    throw new InvalidOperationException($"[M01] {target.GetType().Name} không có field '{field}'.");
                property.objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static void Info(string message) => EditorUtility.DisplayDialog("Hero Escape · M01", message, "Đóng");
    }
}
