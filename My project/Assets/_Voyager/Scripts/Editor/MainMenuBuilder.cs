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

            if (System.IO.File.Exists(MenuScenePath) && !Application.isBatchMode &&
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

            // ---------- Panel principal ----------
            var main = UIObj("Panel_Principal", root);
            Stretch(Rt(main));

            var title = Txt("Txt_Titulo", main.transform, "VOYAGER II", 160, TextAlignmentOptions.Center, Gold, FontStyles.Bold);
            TopBand(title.rectTransform, 100, 100, 110, 200);
            title.characterSpacing = 12;
            var subtitle = Txt("Txt_Subtitulo", main.transform, "Llamada de Auxilio", 64, TextAlignmentOptions.Center, Color.white);
            TopBand(subtitle.rectTransform, 100, 100, 310, 90);
            var tagline = Txt("Txt_Pregunta", main.transform,
                "Si tuvieras que explicarle a alguien que nunca conoció la Tierra qué es la humanidad, ¿qué elegirías mostrarle?",
                28, TextAlignmentOptions.Center, new Color(1, 1, 1, 0.6f), FontStyles.Italic);
            TopBand(tagline.rectTransform, 360, 360, 410, 90);

            var buttons = UIObj("Botones", main.transform);
            Place(Rt(buttons), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(520, 420));
            var col = buttons.AddComponent<VerticalLayoutGroup>();
            col.spacing = 30; col.childControlWidth = col.childControlHeight = true; col.childForceExpandWidth = col.childForceExpandHeight = true;
            var play = Btn("Btn_Jugar", buttons.transform, "JUGAR", Green, 48);
            var credits = Btn("Btn_Creditos", buttons.transform, "CRÉDITOS", Hex("33415C"), 40);
            var quit = Btn("Btn_Salir", buttons.transform, "SALIR", Red, 40);

            // ---------- Panel de créditos ----------
            var creditsPanel = Img("Panel_Creditos", root, Dim, raycast: true);
            Stretch(creditsPanel.rectTransform);
            var box = Box(creditsPanel.transform, new Vector2(900, 620));
            var cTitle = Txt("Txt_TituloCreditos", box, "CRÉDITOS", 56, TextAlignmentOptions.Center, Ink, FontStyles.Bold);
            TopBand(cTitle.rectTransform, 40, 40, 50, 80);
            var names = Txt("Txt_Nombres", box, string.Join("\n", Credits), 44, TextAlignmentOptions.Center, Ink);
            names.lineSpacing = 30;
            Stretch(names.rectTransform, 60, 60, 160, 170);
            var back = Btn("Btn_Volver", box, "Volver al menú", Navy, 32);
            Place(Rt(back.gameObject), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(420, 90));
            creditsPanel.gameObject.SetActive(false);

            // ---------- Script del menú ----------
            var menu = canvasGo.AddComponent<MainMenuUI>();
            menu.gameSceneName = gameSceneName;
            menu.mainPanel = main;
            menu.creditsPanel = creditsPanel.gameObject;
            menu.playButton = play;
            menu.creditsButton = credits;
            menu.quitButton = quit;
            menu.creditsBackButton = back;

            AddTitleScreen(root, menu);
            ArtSetup.ApplyToMenu(root); // si existen Fondo y Texto Titulo en Assets/Art/UI
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
                            TextAlignmentOptions.Center, Color.white);
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
                EditorUtility.SetDirty(menu);
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
            }
            Debug.Log("[Voyager] Escenas actualizadas: pausa con ESC en el juego y pantalla de inicio en el menú.");
        }
    }
}
