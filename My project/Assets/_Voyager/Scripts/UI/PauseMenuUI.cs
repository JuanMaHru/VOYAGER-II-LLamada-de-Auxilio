using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Pantalla de pausa: ESC la abre y la cierra. Tiene "Reanudar" y "Menú principal".
    /// No se puede pausar cuando ya se está mostrando el final.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        public CampaignManager campaign;
        [Tooltip("Panel a mostrar. Conviene que sea un hijo de este objeto (así este script sigue activo).")]
        public GameObject panel;
        public Button resumeButton;
        public Button menuButton;
        [Tooltip("Nombre de la escena del menú (sin .unity).")]
        public string menuSceneName = "MainMenu";

        public bool IsPaused { get; private set; }

        void Awake()
        {
            if (campaign == null) campaign = FindAnyObjectByType<CampaignManager>();
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (!InputHelper.EscapePressed()) return;
            if (IsPaused) Resume();
            else if (CanPause()) Pause();
        }

        bool CanPause() => campaign == null || campaign.Phase != CampaignPhase.Finished;

        public void Pause()
        {
            IsPaused = true;
            if (panel != null) panel.SetActive(true);
            Time.timeScale = 0f; // congela animaciones o timers que agreguen más adelante
        }

        public void Resume()
        {
            IsPaused = false;
            if (panel != null) panel.SetActive(false);
            Time.timeScale = 1f;
        }

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        void OnDestroy() => Time.timeScale = 1f; // nunca dejar el juego congelado al cambiar de escena
    }
}
