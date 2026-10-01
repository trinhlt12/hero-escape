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
        public const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        /// <summary>Chuỗi màn trong scene chơi thử: tên hiển thị (tiếng Anh) và file level.</summary>
        public static readonly (string displayName, string path)[] Levels =
        {
            ("Level 01", "Assets/HeroEscape/Data/Levels/M01_TestLevel.txt"),
            ("Level 02", "Assets/HeroEscape/Data/Levels/M02_DashLevel.txt"),
            ("Level 03", "Assets/HeroEscape/Data/Levels/M03_StasisLevel.txt"),
            ("Level 04", "Assets/HeroEscape/Data/Levels/M04_TwoEnemiesLevel.txt"),
        };

        // Bảng màu theo Art Guide mục 2.
        static readonly Color Background = new Color32(0x17, 0x24, 0x2C, 0xFF);
        static readonly Color Panel = new Color32(0x0F, 0x19, 0x1F, 0xF2);
        static readonly Color HeroBlue = new Color32(0x24, 0x7F, 0xAD, 0xFF);
        static readonly Color Neutral = new Color32(0x52, 0x63, 0x6E, 0xFF);
        static readonly Color Highlight = new Color32(0xE8, 0xB8, 0x49, 0xFF);
        static readonly Color ExitGreen = new Color32(0x26, 0x73, 0x4D, 0xFF);
        static readonly Color SoftText = new Color32(0xC8, 0xD3, 0xD9, 0xFF);
        static readonly Color StasisPurple = new Color32(0x71, 0x4D, 0x9A, 0xFF);
        static readonly Color FrozenCyan = new Color32(0x9F, 0xE0, 0xF5, 0xFF);

        // Kích thước UI theo canvas tham chiếu 1080 x 1920.
        const float TopPanelHeight = 300f;
        const float BottomPanelHeight = 540f;

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

            var levelFiles = new TextAsset[Levels.Length];
            string missing = string.Empty;
            for (int i = 0; i < Levels.Length; i++)
            {
                levelFiles[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(Levels[i].path);
                if (levelFiles[i] == null) missing += Levels[i].path + "\n";
            }

            var unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
            if (unlitMaterial == null) missing += UnlitMaterialPath;
            if (missing.Length > 0)
            {
                Info("Không tìm thấy:\n" + missing);
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
            GameController controller = Populate(art, levelFiles, unlitMaterial);

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

        static GameController Populate(M01ArtCheck.ArtSet art, TextAsset[] levelFiles, Material unlitMaterial)
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
                ("heroSprite", art.Hero), ("enemySprite", art.Enemy), ("highlightSprite", art.Highlight),
                ("spriteMaterial", unlitMaterial));

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

            // Mọi text tạo sẵn trong scene là tiếng Anh, lấy từ UiText; GameController cập nhật khi chạy.
            RectTransform top = CreatePanel("TopPanel", safeArea, new Vector2(0f, 1f), new Vector2(1f, 1f), TopPanelHeight);
            Text title = CreateText("Title", top, 42, SoftText, FontStyle.Bold, new Vector2(40f, -90f), new Vector2(-40f, -20f),
                UiText.LevelTitle(Levels[0].displayName));
            Text status = CreateText("Status", top, 58, Color.white, FontStyle.Bold, new Vector2(40f, -190f), new Vector2(-40f, -95f),
                UiText.YourTurn(1));
            Text notice = CreateText("Notice", top, 36, Highlight, FontStyle.Normal, new Vector2(40f, -290f), new Vector2(-40f, -195f),
                string.Empty);

            // Panel dưới (từ trên xuống): hướng dẫn, hàng điều hướng màn, hàng hành động.
            RectTransform bottom = CreatePanel("BottomPanel", safeArea, new Vector2(0f, 0f), new Vector2(1f, 0f), BottomPanelHeight);
            Text hint = CreateText("Hint", bottom, 32, SoftText, FontStyle.Normal, new Vector2(40f, -170f), new Vector2(-40f, -40f),
                UiText.HintBasic);
            RectTransform navRow = CreateButtonRow("NavigationRow", bottom, 200f, 30f);
            Button next = CreateButton("NextLevelButton", navRow, ExitGreen, 420f, UiText.NextLevelButton, 46);
            Button back = CreateButton("BackButton", navRow, Neutral, 480f, UiText.BackToButton(Levels[0].displayName), 46);
            // Hàng hành động có tối đa 4 nút: tổng 940 + 3 x 20 = 1000 (vừa chiều rộng 1080 - 2 x 40).
            RectTransform actionRow = CreateButtonRow("ActionRow", bottom, 40f, 20f);
            Button wait = CreateButton("WaitButton", actionRow, HeroBlue, 200f, UiText.WaitButton, 40);
            Button dash = CreateButton("DashButton", actionRow, HeroBlue, 250f, UiText.DashButton(2, 2), 40);
            Button stasis = CreateButton("StasisButton", actionRow, StasisPurple, 270f, UiText.StasisButton(1, 1), 40);
            Button restart = CreateButton("RestartButton", actionRow, Neutral, 220f, UiText.RestartButton, 40);

            // Trạng thái hiển thị ban đầu giống Level 01: chưa có Dash/Stasis, chưa có điều hướng.
            dash.gameObject.SetActive(false);
            stasis.gameObject.SetActive(false);
            next.gameObject.SetActive(false);
            back.gameObject.SetActive(false);

            Wire(hud,
                ("titleText", title), ("statusText", status), ("noticeText", notice), ("hintText", hint),
                ("waitButton", wait), ("dashButton", dash), ("stasisButton", stasis), ("restartButton", restart),
                ("nextLevelButton", next), ("backButton", back));

            // Nhãn theo từng X01 (số thứ tự, "Frozen"): nằm dưới các panel, không nhận raycast.
            // Chỉ có hai mẫu tắt sẵn; nhãn thật được tạo khi nạp màn, nên dựng lại scene không sinh nhãn trùng.
            RectTransform labelsRoot = CreateRect("EnemyLabels", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelsRoot.SetSiblingIndex(0);
            Text frozenTemplate = CreateLabelTemplate("FrozenTemplate", labelsRoot, new Vector2(240f, 60f), new Vector2(0.5f, 0f),
                36, FrozenCyan, UiText.FrozenLabel);
            Text orderTemplate = CreateLabelTemplate("OrderTemplate", labelsRoot, new Vector2(60f, 60f), new Vector2(0.5f, 0.5f),
                34, Highlight, UiText.EnemyOrder(1));
            var enemyLabels = labelsRoot.gameObject.AddComponent<EnemyStatusLabel>();
            Wire(enemyLabels, ("worldCamera", camera), ("board", board), ("frozenTemplate", frozenTemplate), ("orderTemplate", orderTemplate));

            // Điều khiển
            var gameGo = new GameObject("Game");
            var controller = gameGo.AddComponent<GameController>();
            var input = gameGo.AddComponent<BoardInput>();
            Wire(controller, ("boardView", board), ("hud", hud), ("enemyStatus", enemyLabels));
            WireLevels(controller, levelFiles);
            Wire(input, ("worldCamera", camera), ("board", board), ("controller", controller));
            Wire(fitter, ("board", board), ("topPanel", top), ("bottomPanel", bottom));

            return controller;
        }

        static void WireLevels(GameController controller, TextAsset[] levelFiles)
        {
            var so = new SerializedObject(controller);
            SerializedProperty levels = so.FindProperty("levels")
                ?? throw new InvalidOperationException("[M01] GameController không có field 'levels'.");
            levels.arraySize = Levels.Length;
            for (int i = 0; i < Levels.Length; i++)
            {
                SerializedProperty entry = levels.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("displayName").stringValue = Levels[i].displayName;
                entry.FindPropertyRelative("levelFile").objectReferenceValue = levelFiles[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Hàng nút kéo ngang panel, cao 140, cách đáy panel <paramref name="bottom"/>; nút ẩn không chiếm chỗ.</summary>
        static RectTransform CreateButtonRow(string name, Transform parent, float bottom, float spacing)
        {
            RectTransform row = CreateRect(name, parent, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(40f, bottom), new Vector2(-40f, bottom + 140f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return row;
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

        /// <summary>Mẫu nhãn chữ có viền tối (tắt sẵn), dùng để nhân bản nhãn cho từng X01 khi chạy.</summary>
        static Text CreateLabelTemplate(string name, Transform parent, Vector2 size, Vector2 pivot, int fontSize, Color color, string text)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rect.sizeDelta = size;
            rect.pivot = pivot;
            Text label = ConfigureText(rect.gameObject.AddComponent<Text>(), fontSize, color, FontStyle.Bold, text);
            rect.gameObject.AddComponent<Outline>().effectColor = Background;
            rect.gameObject.SetActive(false);
            return label;
        }

        /// <summary>Text neo theo cạnh trên của parent; offset tính từ cạnh trên.</summary>
        static Text CreateText(string name, Transform parent, int fontSize, Color color, FontStyle style, Vector2 offsetMin, Vector2 offsetMax,
            string initialText)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(1f, 1f), offsetMin, offsetMax);
            return ConfigureText(rect.gameObject.AddComponent<Text>(), fontSize, color, style, initialText);
        }

        static Text ConfigureText(Text text, int fontSize, Color color, FontStyle style, string initialText)
        {
            // Font tích hợp của Unity (dynamic). Project chưa có font riêng.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = initialText;
            return text;
        }

        /// <summary>Nút trong hàng HorizontalLayoutGroup, rộng cố định <paramref name="width"/>.</summary>
        static Button CreateButton(string name, Transform row, Color color, float width, string label, int fontSize)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(row, false);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;

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
            ConfigureText(labelRect.gameObject.AddComponent<Text>(), fontSize, Color.white, FontStyle.Bold, label);
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
