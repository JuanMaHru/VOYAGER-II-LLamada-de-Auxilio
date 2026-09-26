using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>Opciones → Sonido: tres barras (maestro, efectos, música) que guardan su valor.</summary>
    public class SoundOptionsUI : MonoBehaviour
    {
        public Slider masterSlider, effectsSlider, musicSlider;
        public TMP_Text masterText, effectsText, musicText;

        void OnEnable()
        {
            Bind(masterSlider, masterText, "VOLUMEN MAESTRO", GameAudio.Master, v => GameAudio.Master = v);
            Bind(effectsSlider, effectsText, "VOLUMEN DE EFECTOS", GameAudio.Effects, v => GameAudio.Effects = v);
            Bind(musicSlider, musicText, "VOLUMEN DE MÚSICA", GameAudio.Music, v => GameAudio.Music = v);
        }

        static void Bind(Slider slider, TMP_Text text, string label, float value, System.Action<float> save)
        {
            if (slider == null) return;
            slider.onValueChanged.RemoveAllListeners();
            slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(Mathf.Round(value * 100f));
            SetText(text, label, slider.value);
            slider.onValueChanged.AddListener(v => { save(v / 100f); SetText(text, label, v); });
        }

        static void SetText(TMP_Text text, string label, float v)
        {
            if (text != null) text.text = $"{label}: {v:0}%";
        }
    }
}
