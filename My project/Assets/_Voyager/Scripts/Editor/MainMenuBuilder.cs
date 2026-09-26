using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Voyager.UI;
using static Voyager.EditorTools.GameScreenBuilder;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Crea la escena del menú principal (título, Jugar, Créditos, Salir), agrega los botones
    /// "Menú" a la escena del juego y pone las dos escenas en la lista de la build (menú primero).
    /// Menú: Voyager → Crear escena de menú principal.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string MenuSceneName = "MainMenu";
        const string MenuScenePath = "Assets/Scenes/" + MenuSceneName + ".unity";
        const string DefaultGameScenePath = "Assets/Scenes/SampleScene.unity";

        static readonly string[] Credits = { "Juan Manuel Hruby", "Alexis Mendoza", "Pedro Noriega" };

        static bool skipRebuildQuestion;

        /// <summary>
        /// Rehace el menú con el estilo nuevo (opciones, sonido, controles, créditos) y pasa
        /// los botones de la escena del juego (pausa, final, carteles) al mismo estilo.
        /// </summary>
        [MenuItem("Voyager/Aplicar estilo nuevo y menú de opciones", priority = 6)]
        public static void ApplyNewStyle()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Estilo nuevo",
                    "Esto REHACE la escena MainMenu (se pierden los cambios hechos a mano en ella) y pasa los botones " +
                    "de la escena del juego al estilo nuevo, sin rehacerla. ¿Seguir?", "Seguir", "Cancelar"))
                return;
            skipRebuildQuestion = true;
            try { Build(); }
            finally { skipRebuildQuestion = false; }
        }

        [MenuItem("Voyager/Crear escena de menú principal", priority = 3)]
        public static void Build()
        {
            if (!EnsureTmpEssentials(Build)) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string gameScenePath = FindGameScenePath();
            if (gameScenePath == null)
            {
                EditorUtility.DisplayDialog("Menú principal", "No encontré la escena del juego (la que tiene GameSystem). " +
                    "Abrila y volvé a apretar el botón.", "OK");
                return;
            }
            string gameSceneName = System.IO.Path.GetFileNameWithoutExtension(gameScenePath);

            if (System.IO.File.Exists(MenuScenePath) && !Application.isBatchMode && !skipRebuildQuestion &&
                !EditorUtility.DisplayDialog("Menú principal", $"Ya existe {MenuScenePath}. ¿Rehacerla?", "Rehacer", "Cancelar"))
                return;

            // ---------- 1) Botones "Menú" en la escena del juego (sin rehacer su pantalla) ----------
            var gameScene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);
            var canvas = GameObject.Find("Canvas_Oficina");
            if (canvas == null) GameScreenBuilder.Build();          // si no tiene pantalla, la crea (ya con los botones)
            else AddMenuButtons(canvas.transform, MenuSceneName);
            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);

            // ---------- 2) Escena del menú ----------
            var menuScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EnsureEventSystem();
            BuildMenuUI(gameSceneName);
            EditorSceneManager.SaveScene(menuScene, MenuScenePath);

            // ---------- 3) Lista de escenas de la build: menú primero ----------
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(gameScenePath, true)
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MenuScenePath && s.path != gameScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[Voyager] Menú principal creado en {MenuScenePath}. Escenas de la build: {MenuSceneName} (primera) y {gameSceneName}. " +
                      "Apretá Play desde esta escena para empezar por el menú.");
        }

        static string FindGameScenePath()
        {
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && !string.IsNullOrEmpty(active.path) && active.path != MenuScenePath &&
                Object.FindAnyObjectByType<CampaignManager>() != null)
                return active.path;

            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MenuScenePath && System.IO.File.Exists(s.path)) return s.path;

            return System.IO.File.Exists(DefaultGameScenePath) ? DefaultGameScenePath : null;
        }

        static void BuildMenuUI(string gameSceneName)
        {
            var canvasGo = new GameObject("Canvas_Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            Stretch(Img("Img_Fondo", root, Navy).rectTransform);
            var dark = MenuStyleKit.Palette.OnDark;
            var cream = MenuStyleKit.Cream;

            // ---------- Menú principal ----------
            var main = UIObj("Panel_Principal", root);
            Stretch(Rt(main));

            // Textos de respaldo: si existe el arte (Texto Titulo), ArtSetup los apaga y pone el logo.
            var title = Txt("Txt_Titulo", main.transform, "VOYAGER II", 160, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            TopBand(title.rectTransform, 100, 100, 110, 200);
            title.characterSpacing = 12;
            var subtitle = Txt("Txt_Subtitulo", main.transform, "Llamada de Auxilio", 64, TextAlignmentOptions.Center, Color.white);
            TopBand(subtitle.rectTransform, 100, 100, 310, 90);
            var tagline = Txt("Txt_Pregunta", main.transform,
                "Si tuvieras que explicarle a alguien que nunca conoció la Tierra qué es la humanidad, ¿qué elegirías mostrarle?",
                26, TextAlignmentOptions.Center, new Color(cream.r, cream.g, cream.b, 0.7f), FontStyles.Italic);
            TopBand(tagline.rectTransform, 360, 360, 458, 80);

            // Créditos está dentro de Opciones (no en el menú principal).
            var list = MenuList("Botones", main.transform, 3, new Vector2(0.5f, 0), new Vector2(0, 130));
            var play = MenuStyleKit.CreateMenuButton("Btn_Jugar", list, "JUGAR", dark);
            var options = MenuStyleKit.CreateMenuButton("Btn_Opciones", list, "OPCIONES", dark);
            var quit = MenuStyleKit.CreateMenuButton("Btn_Salir", list, "SALIR", dark);

            // ---------- Opciones ----------
            var optionsPanel = SubScreen("Panel_Opciones", root, "OPCIONES");
            var oList = MenuList("Botones", optionsPanel, 4, new Vector2(0.5f, 0.5f), new Vector2(0, -60));
            var sound = MenuStyleKit.CreateMenuButton("Btn_Sonido", oList, "SONIDO", dark);
            var controls = MenuStyleKit.CreateMenuButton("Btn_Controles", oList, "CONTROLES", dark);
            var optCredits = MenuStyleKit.CreateMenuButton("Btn_Creditos", oList, "CRÉDITOS", dark);
            var optBack = MenuStyleKit.CreateMenuButton("Btn_Volver", oList, "VOLVER", dark);

            // ---------- Sonido ----------
            var soundPanel = SubScreen("Panel_Sonido", root, "SONIDO");
            var rows = UIObj("Filas", soundPanel);
            Place(Rt(rows), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(820, 450));
            var soundUI = soundPanel.gameObject.AddComponent<SoundOptionsUI>();
            (soundUI.masterSlider, soundUI.masterText) = VolumeRow(rows.transform, "Maestro", 0);
            (soundUI.effectsSlider, soundUI.effectsText) = VolumeRow(rows.transform, "Efectos", 1);
            (soundUI.musicSlider, soundUI.musicText) = VolumeRow(rows.transform, "Musica", 2);
            var soundBack = BackButton(soundPanel);

            // ---------- Controles ----------
            var controlsPanel = SubScreen("Panel_Controles", root, "CONTROLES");
            var controlsText = Txt("Txt_Controles", controlsPanel,
                "<b>APROBAR:</b> Click izquierdo.\n" +
                "<b>RECHAZAR:</b> Click izquierdo.\n" +
                "<b>ARRASTRAR HOJAS:</b> Mantener click izquierdo.\n" +
                "<b>PAUSA:</b> Esc.",
                36, TextAlignmentOptions.Left, cream);
            controlsText.lineSpacing = 40;
            controlsText.characterSpacing = 3;
            Place(controlsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(980, 380));
            var controlsBack = BackButton(controlsPanel);

            // ---------- Créditos ----------
            var creditsPanel = SubScreen("Panel_Creditos", root, "CRÉDITOS");
            var names = Txt("Txt_Nombres", creditsPanel, string.Join("\n", Credits), 48, TextAlignmentOptions.Center, cream);
            names.lineSpacing = 40;
            names.characterSpacing = 4;
            Place(names.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1000, 380));
            var creditsBack = BackButton(creditsPanel);

            // ---------- Script del menú ----------
            var menu = canvasGo.AddComponent<MainMenuUI>();
            menu.gameSceneName = gameSceneName;
            menu.mainPanel = main;
            menu.optionsPanel = optionsPanel.gameObject;
            menu.soundPanel = soundPanel.gameObject;
            menu.controlsPanel = controlsPanel.gameObject;
            menu.creditsPanel = creditsPanel.gameObject;
            menu.playButton = play;
            menu.optionsButton = options;
            menu.quitButton = quit;
            menu.soundButton = sound;
            menu.controlsButton = controls;
            menu.optionsCreditsButton = optCredits;
            menu.backButtons = new List<Button> { optBack, soundBack, controlsBack, creditsBack };
            foreach (var p in new[] { optionsPanel, soundPanel, controlsPanel, creditsPanel }) p.gameObject.SetActive(false);

            AddTitleScreen(root, menu);
            ArtSetup.ApplyToMenu(root); // si existen Fondo y Texto Titulo en Assets/Art/UI
        }

        // ------------------------------------------------------------------ piezas del menú
        /// <summary>Lista vertical de botones de menú (alto fijo por renglón, como en la referencia).</summary>
        static Transform MenuList(string name, Transform parent, int count, Vector2 anchor, Vector2 pos)
        {
            const float rowHeight = 70, spacing = 12;
            var list = UIObj(name, parent);
            Place(Rt(list), anchor, new Vector2(0.5f, anchor.y), pos, new Vector2(560, count * rowHeight + (count - 1) * spacing));
            var col = list.AddComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = col.childForceExpandHeight = true;
            return list.transform;
        }

        /// <summary>Pantalla secundaria: oscurece el fondo de estrellas y pone el título grande arriba.</summary>
        static Transform SubScreen(string name, Transform root, string title)
        {
            var panel = Img(name, root, new Color(0, 0, 0, 0.55f), raycast: true);
            Stretch(panel.rectTransform);
            var t = Txt("Txt_Titulo", panel.transform, title, 96, TextAlignmentOptions.Center, MenuStyleKit.Cream, FontStyles.Bold);
            t.characterSpacing = 10;
            TopBand(t.rectTransform, 100, 100, 130, 130);
            return panel.transform;
        }

        static Button BackButton(Transform panel)
        {
            var b = MenuStyleKit.CreateMenuButton("Btn_Volver", panel, "VOLVER", MenuStyleKit.Palette.OnDark);
            Place(Rt(b.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(360, 70));
            return b;
        }

        static (Slider, TMPro.TMP_Text) VolumeRow(Transform parent, string key, int index)
        {
            var label = Txt("Txt_Volumen" + key, parent, "VOLUMEN", 34, TextAlignmentOptions.Center, MenuStyleKit.Cream);
            label.characterSpacing = 4;
            TopBand(label.rectTransform, 0, 0, index * 150, 50);
            var slider = MenuStyleKit.CreateSlider("Slider_" + key, parent);
            TopBand(Rt(slider.gameObject), 60, 60, index * 150 + 62, 40);
            return (slider, label);
        }

        /// <summary>Créditos queda solo dentro de Opciones: quita el botón del menú principal y achica la lista.</summary>
        static void RemoveMainMenuCredits(MainMenuUI menu)
        {
            var list = menu.transform.Find("Panel_Principal/Botones");
            var credits = list != null ? list.Find("Btn_Creditos") : null;
            if (credits == null) return;

            Undo.RecordObject(menu, "Quitar Créditos");
            menu.creditsButton = null;
            Undo.DestroyObjectImmediate(credits.gameObject);

            var rt = (RectTransform)list;
            Undo.RecordObject(rt, "Lista del menú");
            int rows = list.childCount;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, rows * 70 + (rows - 1) * 12);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 130);
        }

        /// <summary>Pantalla antes del menú: título + "Presiona cualquier tecla para comenzar".</summary>
        static void AddTitleScreen(Transform root, MainMenuUI menu)
        {
            if (root.Find("Panel_Inicio") != null) return;

            var panel = Img("Panel_Inicio", root, Navy, raycast: true);
            Stretch(panel.rectTransform);
            panel.transform.SetAsLastSibling();

            var title = Txt("Txt_Titulo", panel.transform, "VOYAGER II", 190, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            TopBand(title.rectTransform, 100, 100, 260, 230);
            title.characterSpacing = 14;
            var subtitle = Txt("Txt_Subtitulo", panel.transform, "Llamada de Auxilio", 72, TextAlignmentOptions.Center, Color.white);
            TopBand(subtitle.rectTransform, 100, 100, 490, 100);

            var press = Txt("Txt_PresionaTecla", panel.transform, "Presiona cualquier tecla para comenzar", 40,
                            TextAlignmentOptions.Center, MenuStyleKit.Cream);
            press.characterSpacing = 4;
            Place(press.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(1200, 70));

            menu.titlePanel = panel.gameObject;
            menu.pressAnyKeyText = press;
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Pantalla de inicio");
        }

        // =====================================================================
        // Actualizar escenas ya creadas (sin rehacerlas)
        // =====================================================================
        [MenuItem("Voyager/Actualizar escenas (pausa y pantalla de inicio)", priority = 4)]
        public static void UpdateScenes()
        {
            if (!EnsureTmpEssentials(UpdateScenes)) return;
            if (!System.IO.File.Exists(MenuScenePath)) { Build(); return; } // no hay menú: se crea completo
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string gameScenePath = FindGameScenePath() ??
                EditorBuildSettings.scenes.Select(s => s.path).FirstOrDefault(p => p != MenuScenePath);

            // Juego: sacar "Menú" de la cabecera + agregar la pausa con ESC
            if (gameScenePath != null)
            {
                var gameScene = EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Single);
                var canvas = GameObject.Find("Canvas_Oficina");
                if (canvas != null)
                {
                    AddMenuButtons(canvas.transform, MenuSceneName);
                    EditorSceneManager.MarkSceneDirty(gameScene);
                    EditorSceneManager.SaveScene(gameScene);
                }
                else Debug.LogWarning($"[Voyager] {gameScenePath} no tiene Canvas_Oficina: usá Voyager → Crear pantalla del juego.");
            }

            // Menú: agregar la pantalla de inicio
            var menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            var menu = Object.FindAnyObjectByType<MainMenuUI>();
            if (menu != null)
            {
                AddTitleScreen(menu.transform, menu);
                RemoveMainMenuCredits(menu);
                EditorUtility.SetDirty(menu);
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
            }
            Debug.Log("[Voyager] Escenas actualizadas: pausa con ESC en el juego y pantalla de inicio en el menú.");
        }
    }
}
