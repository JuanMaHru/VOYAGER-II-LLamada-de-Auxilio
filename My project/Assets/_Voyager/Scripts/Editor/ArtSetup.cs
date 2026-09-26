using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Voyager.EditorTools.GameScreenBuilder;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Pone el arte de Assets/Art/UI en las pantallas ya armadas (sin rehacerlas):
    ///   · Pantalla de inicio (escena del menú): Fondo + Texto Titulo (reemplaza los textos del nombre).
    ///   · Pantalla final (escena del juego):   Fondo + Texto Titulo más chico arriba, el final debajo.
    /// Menú: Voyager → Aplicar arte (Fondo y Texto Titulo).
    /// </summary>
    public static class ArtSetup
    {
        const string BackgroundPath = "Assets/Art/UI/Fondo.png";
        const string LogoPath = "Assets/Art/UI/Texto Titulo.png";
        const float Aspect = 16f / 9f;

        static Sprite Background => AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        static Sprite Logo => AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);

        [MenuItem("Voyager/Aplicar arte (Fondo y Texto Titulo)", priority = 5)]
        public static void ApplyFromMenu()
        {
            if (Background == null || Logo == null)
            {
                EditorUtility.DisplayDialog("Arte", $"No encontré {BackgroundPath} o {LogoPath}.\n" +
                    "Revisá que estén ahí y que su Texture Type sea 'Sprite (2D and UI)'.", "OK");
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string gameScene = null, menuScene = null;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path.EndsWith("/" + MainMenuBuilder.MenuSceneName + ".unity")) menuScene = s.path;
                else if (gameScene == null) gameScene = s.path;
            }

            if (gameScene != null)
            {
                var scene = EditorSceneManager.OpenScene(gameScene, OpenSceneMode.Single);
                var canvas = GameObject.Find("Canvas_Oficina");
                if (canvas != null && ApplyToGame(canvas.transform)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            if (menuScene != null)
            {
                var scene = EditorSceneManager.OpenScene(menuScene, OpenSceneMode.Single);
                var canvas = GameObject.Find("Canvas_Menu");
                if (canvas != null && ApplyToMenu(canvas.transform)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            Debug.Log("[Voyager] Arte aplicado: pantalla de inicio y pantalla final con Fondo y Texto Titulo.");
        }

        /// <summary>Pantalla de inicio. Devuelve true si cambió algo.</summary>
        internal static bool ApplyToMenu(Transform canvasMenu)
        {
            if (Background == null || Logo == null) return false;
            bool changed = ApplyToMainPanel(canvasMenu);

            var panel = canvasMenu.Find("Panel_Inicio");
            if (panel == null) return changed;

            AddBackground(panel);

            // El logo reemplaza los textos del nombre del juego (quedan apagados, por si quieren volver atrás).
            SetActive(panel.Find("Txt_Titulo"), false);
            SetActive(panel.Find("Txt_Subtitulo"), false);

            if (panel.Find("Img_TextoTitulo") == null)
            {
                var logo = Img("Img_TextoTitulo", panel, Color.white);
                logo.sprite = Logo;
                Stretch(logo.rectTransform);
                var fit = logo.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; // entero, sin deformar
                fit.aspectRatio = Aspect;
                logo.transform.SetSiblingIndex(1); // arriba del fondo, debajo de "Presiona cualquier tecla"
                Undo.RegisterCreatedObjectUndo(logo.gameObject, "Logo");
            }
            return true;
        }

        /// <summary>
        /// Menú principal (JUGAR / CRÉDITOS / SALIR): el fondo general pasa a ser Fondo.png y el logo
        /// reemplaza los textos del nombre. El logo va al 80% para que no se pise con los botones.
        /// </summary>
        static bool ApplyToMainPanel(Transform canvasMenu)
        {
            var main = canvasMenu.Find("Panel_Principal");
            var rootBg = canvasMenu.Find("Img_Fondo");
            if (main == null) return false;

            // Fondo general del menú (también queda detrás del panel de créditos)
            if (rootBg != null && rootBg.GetComponent<Image>() is Image bg)
            {
                Undo.RecordObject(bg, "Fondo del menú");
                bg.sprite = Background;
                bg.color = Color.white;
                var fit = rootBg.GetComponent<AspectRatioFitter>();
                if (fit == null) fit = Undo.AddComponent<AspectRatioFitter>(rootBg.gameObject);
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = Aspect;
            }

            SetActive(main.Find("Txt_Titulo"), false);
            SetActive(main.Find("Txt_Subtitulo"), false);

            if (main.Find("Img_TextoTitulo") == null)
            {
                var logo = Img("Img_TextoTitulo", main, Color.white);
                logo.sprite = Logo;
                logo.preserveAspect = true;
                Place(logo.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 25), new Vector2(1536, 864));
                logo.transform.SetAsFirstSibling();
                Undo.RegisterCreatedObjectUndo(logo.gameObject, "Logo del menú");
            }

            // La pregunta del juego queda entre el logo y los botones.
            if (main.Find("Txt_Pregunta") is RectTransform question)
            {
                TopBand(question, 360, 360, 458, 80);
                if (question.GetComponent<TMPro.TMP_Text>() is TMPro.TMP_Text qt) { Undo.RecordObject(qt, "Pregunta"); qt.fontSize = 26; }
            }
            return true;
        }

        /// <summary>Pantalla final del juego. Devuelve true si cambió algo.</summary>
        internal static bool ApplyToGame(Transform canvasOficina)
        {
            var panel = canvasOficina.Find("UI_Final/Panel_Final");
            if (panel == null || Background == null || Logo == null) return false;

            AddBackground(panel);

            if (panel.Find("Img_TextoTitulo") == null)
            {
                // Mitad de tamaño y pegado arriba: el logo ocupa la franja superior y deja lugar al final.
                var logo = Img("Img_TextoTitulo", panel, Color.white);
                logo.sprite = Logo;
                logo.preserveAspect = true;
                Place(logo.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(960, 540));
                logo.transform.SetSiblingIndex(1);
                Undo.RegisterCreatedObjectUndo(logo.gameObject, "Logo");
            }

            // Bajar el título del final y el contenido para que no se pisen con el logo.
            if (panel.Find("Txt_TituloFinal") is RectTransform title) TopBand(title, 100, 100, 325, 90);
            if (panel.Find("Contenido_Final") is RectTransform content)
            {
                content.offsetMax = new Vector2(content.offsetMax.x, -435);
                content.offsetMin = new Vector2(content.offsetMin.x, 170);
            }
            return true;
        }

        /// <summary>Fondo a pantalla completa que cubre todo sin deformarse (recorta los bordes si hace falta).</summary>
        static void AddBackground(Transform panel)
        {
            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null) panelImage.color = Color.black; // por si algún borde queda sin cubrir

            if (panel.Find("Img_Fondo") != null) return;
            var bg = Img("Img_Fondo", panel, Color.white);
            bg.sprite = Background;
            Stretch(bg.rectTransform);
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = Aspect;
            bg.transform.SetAsFirstSibling();
            Undo.RegisterCreatedObjectUndo(bg.gameObject, "Fondo");
        }

        static void SetActive(Transform t, bool active)
        {
            if (t == null) return;
            Undo.RecordObject(t.gameObject, "Ocultar texto");
            t.gameObject.SetActive(active);
        }
    }
}
