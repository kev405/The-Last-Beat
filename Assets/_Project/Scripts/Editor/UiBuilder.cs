using System.Collections.Generic;
using System.IO;
using TheLastBeat.Abilities;
using TheLastBeat.Combat;
using TheLastBeat.Heroes;
using TheLastBeat.Level;
using TheLastBeat.Player;
using TheLastBeat.UI;
using TheLastBeat.Waves;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TheLastBeat.EditorTools
{
    /// <summary>
    /// Construye la interfaz con medidas exactas desde el menú "The Last Beat" (tareas 34 y 35):
    ///  - Crear HUD en la escena abierta: barras de vida, canción, habilidades, hordas, mensajes
    ///    y pantallas de victoria y derrota, ya conectados a los objetos de la escena.
    ///  - Crear escena de menú principal: MainMenu.unity con Jugar y Salir, y ambas escenas en la build.
    /// Todo se puede ajustar después a mano. Es código de editor: no va en la build del juego.
    /// </summary>
    public static class UiBuilder
    {
        private const string ScenesFolder = "Assets/_Project/Scenes/";

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.09f, 0.72f);
        private static readonly Color BarBack = new Color(0.1f, 0.09f, 0.12f, 0.95f);
        private static readonly Color Trail = new Color(1f, 0.86f, 0.62f, 0.9f);
        private static readonly Color KnightBlue = new Color(0.29f, 0.48f, 0.85f);
        private static readonly Color BardPurple = new Color(0.56f, 0.36f, 0.84f);
        private static readonly Color Gold = new Color(0.89f, 0.69f, 0.29f);
        private static readonly Color Lavender = new Color(0.78f, 0.7f, 0.94f);
        private static readonly Color Danger = new Color(0.94f, 0.33f, 0.27f);
        private static readonly Color ButtonColor = new Color(0.42f, 0.25f, 0.63f);

        private static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static Sprite Knob => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // ------------------------------------------------------------------ HUD

        [MenuItem("The Last Beat/Crear HUD en la escena abierta", priority = 0)]
        public static void CreateHud()
        {
            LevelFlow flow = Object.FindFirstObjectByType<LevelFlow>();
            if (flow == null)
            {
                EditorUtility.DisplayDialog("Falta el objeto Nivel",
                    "Primero crea el objeto Nivel con el componente Level Flow (fase 20 del plano).", "Entendido");
                return;
            }
            if (GameObject.Find("HUD") != null &&
                !EditorUtility.DisplayDialog("Ya existe un HUD", "Hay un objeto llamado HUD en la escena. ¿Crear otro?", "Crear otro", "Cancelar"))
                return;

            PlayerController knight = Object.FindFirstObjectByType<PlayerController>();
            BardCompanion bard = Object.FindFirstObjectByType<BardCompanion>();
            WaveSpawner spawner = Object.FindFirstObjectByType<WaveSpawner>();

            RectTransform canvas = CreateCanvas("HUD", 10);
            EnsureEventSystem();

            // --- Héroes (arriba a la izquierda)
            RectTransform heroes = Rect("Heroes", canvas, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -32), new Vector2(440, 160));
            AddImage(heroes, PanelColor, UiSprite, Image.Type.Sliced);
            AddText(Rect("KnightLabel", heroes, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(240, 28)),
                    "CABALLERO", 22, Color.white, TextAlignmentOptions.Left, true);
            RectTransform buffTag = Rect("BuffTag", heroes, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(200, 28));
            AddText(buffTag, "POTENCIADO", 20, Gold, TextAlignmentOptions.Right, true);
            HealthBar knightBar = Bar(heroes, "KnightBar", new Vector2(16, -44), new Vector2(408, 28), KnightBlue);
            AddText(Rect("BardLabel", heroes, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -84), new Vector2(240, 26)),
                    "BARDO", 20, Color.white, TextAlignmentOptions.Left, true);
            HealthBar bardBar = Bar(heroes, "BardBar", new Vector2(16, -114), new Vector2(408, 24), BardPurple);

            if (knight != null) Set(knightBar, "target", knight.GetComponent<Health>());
            if (bard != null) Set(bardBar, "target", bard.GetComponent<Health>());

            BuffStatusUI buffUi = heroes.gameObject.AddComponent<BuffStatusUI>();
            if (knight != null) Set(buffUi, "receiver", knight.GetComponent<BuffReceiver>());
            Set(buffUi, "buffTag", buffTag.gameObject);

            // --- Canción (debajo de los héroes)
            RectTransform songRt = Rect("SongStatus", canvas, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -204), new Vector2(360, 60));
            Image icon = AddImage(Rect("Icon", songRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6, 0), new Vector2(44, 44)), Lavender, Knob);
            Image ring = AddImage(Rect("RecoveryRing", songRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(56, 56)),
                                  new Color(1f, 1f, 1f, 0.85f), Knob, Image.Type.Filled);
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 0f;
            TMP_Text songLabel = AddText(Rect("Label", songRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(68, 0), new Vector2(290, 48)),
                                         "CANCIÓN: SUENA", 24, Lavender, TextAlignmentOptions.Left, true);
            SongStatusUI songUi = songRt.gameObject.AddComponent<SongStatusUI>();
            if (bard != null) Set(songUi, "song", bard.GetComponent<SongBuff>());
            Set(songUi, "icon", icon);
            Set(songUi, "recoveryRing", ring);
            Set(songUi, "label", songLabel);

            // --- Hordas (arriba al centro)
            RectTransform waveRt = Rect("WaveStatus", canvas, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(560, 92));
            CanvasGroup waveGroup = waveRt.gameObject.AddComponent<CanvasGroup>();
            waveGroup.alpha = 0f;
            TMP_Text waveText = AddText(Rect("WaveText", waveRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(560, 52)),
                                        "HORDA 1 / 3", 42, Color.white, TextAlignmentOptions.Center, true);
            TMP_Text enemiesText = AddText(Rect("EnemiesText", waveRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -54), new Vector2(560, 34)),
                                           "Enemigos: 0", 24, Lavender, TextAlignmentOptions.Center, false);
            WaveStatusUI waveUi = waveRt.gameObject.AddComponent<WaveStatusUI>();
            Set(waveUi, "spawner", spawner);
            Set(waveUi, "group", waveGroup);
            Set(waveUi, "waveText", waveText);
            Set(waveUi, "enemiesText", enemiesText);

            // --- Mensajes (centro)
            RectTransform banner = Rect("Banner", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(1400, 110));
            CanvasGroup bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            AddImage(Stretch("Band", banner, 0), new Color(0f, 0f, 0f, 0.45f));
            TMP_Text bannerText = AddText(Stretch("Text", banner, 0), "El pueblo está en silencio...", 56, Color.white, TextAlignmentOptions.Center, true);
            BannerUI bannerUi = banner.gameObject.AddComponent<BannerUI>();
            Set(bannerUi, "flow", flow);
            Set(bannerUi, "group", bannerGroup);
            Set(bannerUi, "text", bannerText);

            // --- Habilidades (abajo a la derecha): Giro (Q) a la izquierda, Embestida (E) a la derecha
            RectTransform abilities = Rect("Abilities", canvas, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 32), new Vector2(260, 120));
            AbilityCaster caster = knight != null ? knight.GetComponent<AbilityCaster>() : null;
            Slot(abilities, "Slot_Giro", new Vector2(-140, 0), "Q", 0, caster);
            Slot(abilities, "Slot_Embestida", new Vector2(0, 0), "E", 1, caster);

            // --- Pantallas de fin
            EndScreenUI end = canvas.gameObject.AddComponent<EndScreenUI>();
            Set(end, "flow", flow);
            EndPanel(canvas, end, "VictoryPanel", "¡EL PUEBLO RECUPERÓ SU RITMO!", "La música volvió a las calles.", Gold,
                     "victoryPanel", "victoryRetry", "victoryMenu");
            EndPanel(canvas, end, "DefeatPanel", "LA MÚSICA SE APAGÓ", "Protege a la bardo: sin su canción no hay victoria.", Danger,
                     "defeatPanel", "defeatRetry", "defeatMenu");

            Selection.activeGameObject = canvas.gameObject;
            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

            string missing = "";
            if (knight == null) missing += "\n- Knight (PlayerController)";
            if (bard == null) missing += "\n- Bard (BardCompanion)";
            if (spawner == null) missing += "\n- Enemigos (WaveSpawner)";
            EditorUtility.DisplayDialog("HUD creado",
                missing.Length == 0
                    ? "El HUD quedó creado y conectado. Guarda la escena con Ctrl+S."
                    : "El HUD quedó creado, pero no encontró:" + missing + "\nConecta esos campos a mano.",
                "Listo");
        }

        // ------------------------------------------------------------------ Menú

        [MenuItem("The Last Beat/Crear escena de menú principal", priority = 1)]
        public static void CreateMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string path = ScenesFolder + "MainMenu.unity";
            if (File.Exists(path) &&
                !EditorUtility.DisplayDialog("Ya existe", "MainMenu.unity ya existe. ¿Reemplazarla?", "Reemplazar", "Cancelar"))
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.086f, 0.075f, 0.106f);
            }

            RectTransform canvas = CreateCanvas("Menu", 0);
            EnsureEventSystem();

            AddText(Rect("Title", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(1600, 150)),
                    "EL ÚLTIMO COMPÁS", 120, Gold, TextAlignmentOptions.Center, true);
            AddText(Rect("Subtitle", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(1200, 50)),
                    "The Last Beat", 38, Lavender, TextAlignmentOptions.Center, false);
            Button play = MakeButton(canvas, "PlayButton", "JUGAR", new Vector2(0, -60), new Vector2(340, 80));
            Button quit = MakeButton(canvas, "QuitButton", "SALIR", new Vector2(0, -160), new Vector2(340, 80));
            AddText(Rect("Footer", canvas, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 32), new Vector2(1200, 34)),
                    "Desarrollo de Videojuegos · Univalle", 22, new Color(0.6f, 0.58f, 0.66f), TextAlignmentOptions.Center, false);

            MainMenuUI menu = canvas.gameObject.AddComponent<MainMenuUI>();
            Set(menu, "playButton", play);
            Set(menu, "quitButton", quit);

            EditorSceneManager.SaveScene(scene, path);
            AddScenesToBuild(path, ScenesFolder + "Pueblo.unity");
            EditorUtility.DisplayDialog("Menú creado",
                "Se creó Assets/_Project/Scenes/MainMenu.unity y quedó primera en Build Profiles, seguida de Pueblo.\nAbre Pueblo para seguir trabajando.",
                "Listo");
        }

        // ------------------------------------------------------------------ Piezas

        private static void Slot(RectTransform parent, string name, Vector2 pos, string key, int index, AbilityCaster caster)
        {
            RectTransform slot = Rect(name, parent, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(120, 120));
            AddImage(slot, PanelColor, UiSprite, Image.Type.Sliced);
            TMP_Text nameText = AddText(Rect("Name", slot, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(0, 28)),
                                        "HABILIDAD", 18, Color.white, TextAlignmentOptions.Center, true);
            AddText(Rect("Key", slot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -6), new Vector2(30, 30)),
                    key, 22, Gold, TextAlignmentOptions.Left, true);
            Image overlay = AddImage(Stretch("Cooldown", slot, 0), new Color(0f, 0f, 0f, 0.65f), UiSprite, Image.Type.Filled);
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = true;
            overlay.fillAmount = 0f;
            TMP_Text cdText = AddText(Stretch("CooldownText", slot, 0), "", 46, Color.white, TextAlignmentOptions.Center, true);

            AbilitySlotUI ui = slot.gameObject.AddComponent<AbilitySlotUI>();
            Set(ui, "caster", caster);
            SetInt(ui, "slot", index);
            Set(ui, "nameText", nameText);
            Set(ui, "cooldownOverlay", overlay);
            Set(ui, "cooldownText", cdText);
        }

        private static HealthBar Bar(RectTransform parent, string name, Vector2 pos, Vector2 size, Color fillColor)
        {
            RectTransform root = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), pos, size);
            AddImage(root, BarBack, UiSprite, Image.Type.Sliced);
            RectTransform trail = Stretch("Trail", root, 3);
            AddImage(trail, Trail);
            RectTransform fill = Stretch("Fill", root, 3);
            AddImage(fill, fillColor);
            TMP_Text value = AddText(Stretch("Value", root, 0), "200 / 200", size.y * 0.62f, Color.white, TextAlignmentOptions.Center, true);

            HealthBar bar = root.gameObject.AddComponent<HealthBar>();
            Set(bar, "fill", fill);
            Set(bar, "trail", trail);
            Set(bar, "valueText", value);
            return bar;
        }

        private static void EndPanel(RectTransform canvas, EndScreenUI end, string name, string title, string subtitle, Color titleColor,
                                     string panelField, string retryField, string menuField)
        {
            RectTransform panel = Stretch(name, canvas, 0);
            Image back = AddImage(panel, new Color(0f, 0f, 0f, 0.78f));
            back.raycastTarget = true; // bloquea clics al juego detrás
            AddText(Rect("Title", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 100), new Vector2(1600, 100)),
                    title, 68, titleColor, TextAlignmentOptions.Center, true);
            AddText(Rect("Subtitle", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 24), new Vector2(1400, 50)),
                    subtitle, 30, Color.white, TextAlignmentOptions.Center, false);
            Button retry = MakeButton(panel, "RetryButton", "REINTENTAR", new Vector2(-180, -90), new Vector2(320, 76));
            Button menu = MakeButton(panel, "MenuButton", "MENÚ", new Vector2(180, -90), new Vector2(320, 76));

            Set(end, panelField, panel.gameObject);
            Set(end, retryField, retry);
            Set(end, menuField, menu);
            panel.gameObject.SetActive(false);
        }

        private static Button MakeButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size)
        {
            RectTransform rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image img = AddImage(rt, ButtonColor, UiSprite, Image.Type.Sliced);
            img.raycastTarget = true;
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.6f);
            colors.selectedColor = new Color(1f, 0.88f, 0.6f);
            colors.pressedColor = new Color(0.75f, 0.68f, 0.85f);
            b.colors = colors;
            AddText(Stretch("Label", rt, 0), label, 30, Color.white, TextAlignmentOptions.Center, true);
            return b;
        }

        // ------------------------------------------------------------------ Utilidades

        private static RectTransform CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = LayerMask.NameToLayer("UI");
            Undo.RegisterCreatedObjectUndo(go, "Crear " + name);

            Canvas c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;

            CanvasScaler s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Stretch(string name, Transform parent, float padding)
        {
            RectTransform rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
            return rt;
        }

        private static Image AddImage(RectTransform rt, Color color, Sprite sprite = null, Image.Type type = Image.Type.Simple)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = type;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI AddText(RectTransform rt, string text, float size, Color color, TextAlignmentOptions align, bool bold)
        {
            TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        private static void Set(Object target, string field, Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[UiBuilder] No existe el campo '{field}' en {target.GetType().Name}.");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddScenesToBuild(params string[] paths)
        {
            var list = new List<EditorBuildSettingsScene>();
            var added = new HashSet<string>();
            foreach (string p in paths)
            {
                if (!File.Exists(p) || !added.Add(p)) continue;
                list.Add(new EditorBuildSettingsScene(p, true));
            }
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (added.Add(s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
