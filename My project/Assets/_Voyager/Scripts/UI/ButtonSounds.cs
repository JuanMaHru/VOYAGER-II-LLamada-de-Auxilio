using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Sonido de rollover (mouse encima) y de click para un botón.
    /// GameSounds lo agrega solo a todos los botones al cargar cada escena.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonSounds : MonoBehaviour, IPointerEnterHandler
    {
        [Tooltip("Desactivado en los sellos, que tienen su propio sonido.")]
        public bool playClick = true;

        Button button;

        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(() => { if (playClick) GameSounds.Play(GameSounds.Sound.Click); });
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (button != null && button.IsInteractable()) GameSounds.Play(GameSounds.Sound.Rollover);
        }
    }
}
