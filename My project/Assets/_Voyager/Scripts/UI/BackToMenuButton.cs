using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>Botón que vuelve al menú principal. Ponelo en cualquier botón del juego.</summary>
    [RequireComponent(typeof(Button))]
    public class BackToMenuButton : MonoBehaviour
    {
        [Tooltip("Nombre de la escena del menú (sin .unity).")]
        public string menuSceneName = "MainMenu";

        void Awake() => GetComponent<Button>().onClick.AddListener(GoToMenu);

        public void GoToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
