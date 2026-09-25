using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Escena del menú, con tres pantallas:
    ///   1) Pantalla de inicio: título + "Presioná cualquier tecla para comenzar".
    ///      Se muestra solo la primera vez que se abre el juego; al volver de una partida se salta.
    ///   2) Menú principal: Jugar, Créditos, Salir.
    ///   3) Créditos, con "Volver al menú".
    /// Las escenas tienen que estar en File → Build Profiles → Scene List
    /// (el menú Voyager → Crear escena de menú principal las agrega solo).
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Nombre de la escena del juego (sin .unity).")]
        public string gameSceneName = "SampleScene";

        [Header("Paneles")]
        [Tooltip("Pantalla de inicio ('Presioná cualquier tecla'). Opcional.")]
        public GameObject titlePanel;
        public GameObject mainPanel;
        public GameObject creditsPanel;

        [Header("Botones")]
        public Button playButton;
        public Button creditsButton;
        public Button quitButton;
        public Button creditsBackButton;

        [Header("Pantalla de inicio")]
        [Tooltip("Texto que parpadea suavemente. Opcional.")]
        public TMP_Text pressAnyKeyText;
        [Tooltip("Mostrar la pantalla de inicio también al volver de una partida.")]
        public bool showTitleEveryTime = false;

        static bool titleShownThisSession;

        // Se reinicia en cada Play aunque el editor tenga desactivado el "domain reload".
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => titleShownThisSession = false;

        void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (creditsButton != null) creditsButton.onClick.AddListener(ShowCredits);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
            if (creditsBackButton != null) creditsBackButton.onClick.AddListener(ShowMain);

            Time.timeScale = 1f;
            if (titlePanel != null && (showTitleEveryTime || !titleShownThisSession)) ShowTitle();
            else ShowMain();
        }

        void Update()
        {
            if (titlePanel == null || !titlePanel.activeSelf) return;

            if (pressAnyKeyText != null)
                pressAnyKeyText.alpha = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f));

            if (InputHelper.AnyKeyPressed())
            {
                titleShownThisSession = true;
                ShowMain();
            }
        }

        public void Play() => SceneManager.LoadScene(gameSceneName);

        public void ShowTitle()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            titlePanel.SetActive(true);
        }

        public void ShowCredits()
        {
            if (mainPanel != null) mainPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        public void ShowMain()
        {
            if (titlePanel != null) titlePanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // en el editor, "salir" = parar el Play
#else
            Application.Quit();
#endif
        }
    }
}
