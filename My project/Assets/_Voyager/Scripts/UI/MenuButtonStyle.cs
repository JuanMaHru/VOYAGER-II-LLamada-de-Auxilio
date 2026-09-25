using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Estética de los botones de menú (estilo "máquina de escribir"):
    ///   · Normal: solo el texto.
    ///   · Mouse encima / elegido con teclado: aparece una barra detrás, el texto cambia de color
    ///     y se ve una flechita ▶ a la izquierda.
    ///   · Al presionar: la barra se oscurece un poco y el botón se achica apenas (más sutil).
    /// Usa tiempo "sin escala", así funciona también con el juego en pausa.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButtonStyle : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public Image highlight;
        public TMP_Text label;
        public Graphic arrow;

        [Header("Colores")]
        public Color normalText = new Color32(216, 204, 170, 255);
        public Color highlightText = new Color32(27, 23, 18, 255);
        public Color highlightColor = new Color32(216, 204, 170, 255);
        public Color pressedColor = new Color32(184, 170, 133, 255);

        [Header("Feedback")]
        [Tooltip("Al pasar el mouse, el botón queda 'elegido' (como con el teclado): solo uno se resalta a la vez.")]
        public bool selectOnHover = true;
        [Range(0.9f, 1f)] public float pressedScale = 0.97f;
        [Tooltip("Qué tan rápido aparece la barra (más alto = más rápido).")]
        public float fadeSpeed = 18f;

        Button button;
        bool hovered, pressed, selected;
        float on; // 0 = normal, 1 = resaltado

        void Awake()
        {
            button = GetComponent<Button>();
            button.transition = Selectable.Transition.None;
        }

        void OnEnable()
        {
            hovered = pressed = false;
            selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            on = IsOn ? 1f : 0f;
            Apply();
        }

        bool IsOn => (hovered || selected) && button != null && button.IsInteractable();

        void Update()
        {
            float target = IsOn ? 1f : 0f;
            if (!Mathf.Approximately(on, target) || pressed)
            {
                on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * fadeSpeed);
                Apply();
            }
        }

        void Apply()
        {
            if (highlight != null)
            {
                var bar = pressed ? pressedColor : highlightColor;
                bar.a *= on;
                highlight.color = bar;
            }
            if (label != null) label.color = Color.Lerp(normalText, highlightText, on);
            if (arrow != null)
            {
                var c = arrow.color; c.a = on; arrow.color = c;
            }
            transform.localScale = Vector3.one * (pressed ? pressedScale : 1f);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (selectOnHover && button.IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; Apply(); }
        public void OnPointerDown(PointerEventData e) { pressed = true; Apply(); }
        public void OnPointerUp(PointerEventData e) { pressed = false; Apply(); }
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;
    }
}
