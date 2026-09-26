using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Escena del menú:
    ///   Pantalla de inicio ("Presiona cualquier tecla") → Menú principal
    ///   Menú principal: Jugar · Opciones · Créditos · Salir
    ///   Opciones: Sonido · Controles · Créditos · Volver
    /// "Volver" (o ESC) regresa a la pantalla anterior, así Créditos vuelve a donde lo abriste.
    /// Las escenas tienen que estar en File → Build Profiles → Scene List
    /// (el menú Voyager → Crear escena de menú principal las agrega solo).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Nombre de la escena del juego (sin .unity).")]
        public string gameSceneName = "SampleScene";

        [Header("Pantallas")]
        [Tooltip("Pantalla de inicio ('Presiona cualquier tecla'). Opcional.")]
        public GameObject titlePanel;
        public GameObject mainPanel;
        public GameObject optionsPanel;
        public GameObject soundPanel;
        public GameObject controlsPanel;
        public GameObject creditsPanel;

        [Header("Menú principal")]
        public Button playButton;
        public Button optionsButton;
        public Button creditsButton;
        public Button quitButton;

        [Header("Opciones")]
        public Button soundButton;
        public Button controlsButton;
        public Button optionsCreditsButton;

        [Header("Botones 'Volver' (todos vuelven a la pantalla anterior)")]
        public List<Button> backButtons = new List<Button>();

        [Header("Pantalla de inicio")]
        [Tooltip("Texto que parpadea suavemente. Opcional.")]
        public TMP_Text pressAnyKeyText;
        [Tooltip("Mostrar la pantalla de inicio también al volver de una partida.")]
        public bool showTitleEveryTime = false;

        static bool titleShownThisSession;

        // Se reinicia en cada Play aunque el editor tenga desactivado el "domain reload".
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => titleShownThisSession = false;

        readonly Stack<GameObject> history = new Stack<GameObject>();
        GameObject current;

        void Awake()
        {
            Wire(playButton, Play);
            Wire(optionsButton, () => Open(optionsPanel));
            Wire(creditsButton, () => Open(creditsPanel));
            Wire(quitButton, Quit);
            Wire(soundButton, () => Open(soundPanel));
            Wire(controlsButton, () => Open(controlsPanel));
            Wire(optionsCreditsButton, () => Open(creditsPanel));
            foreach (var b in backButtons) Wire(b, Back);

            Time.timeScale = 1f;
            foreach (var p in new[] { titlePanel, mainPanel, optionsPanel, soundPanel, controlsPanel, creditsPanel })
                if (p != null) p.SetActive(false);

            if (titlePanel != null && (showTitleEveryTime || !titleShownThisSession)) Show(titlePanel);
            else Show(mainPanel);
        }

        void Update()
        {
            if (current == titlePanel && titlePanel != null)
            {
                if (pressAnyKeyText != null)
                    pressAnyKeyText.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));
                if (InputHelper.AnyKeyPressed())
                {
                    titleShownThisSession = true;
                    Show(mainPanel);
                }
                return;
            }

            if (InputHelper.EscapePressed() && history.Count > 0) Back();
        }

        // ------------------------------------------------------------------ navegación
        public void Open(GameObject panel)
        {
            if (panel == null) return;
            if (current != null) history.Push(current);
            Show(panel);
        }

        public void Back() => Show(history.Count > 0 ? history.Pop() : mainPanel);

        void Show(GameObject panel)
        {
            if (current != null) current.SetActive(false);
            current = panel;
            if (panel == null) return;
            panel.SetActive(true);
            if (panel == mainPanel) history.Clear();

            // Deja elegido el primer botón (así el teclado y el resaltado arrancan en él).
            var first = panel.GetComponentInChildren<Button>();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }

        // Compatibilidad con escenas viejas
        public void ShowMain() => Show(mainPanel);
        public void ShowCredits() => Open(creditsPanel);

        // ------------------------------------------------------------------ acciones
        public void Play() => SceneManager.LoadScene(gameSceneName);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // en el editor, "salir" = parar el Play
#else
            Application.Quit();
#endif
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (b != null) b.onClick.AddListener(action);
        }
    }
}
