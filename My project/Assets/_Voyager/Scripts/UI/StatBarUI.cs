using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Barra que muestra una variable global (ej: Tolerancia del Comité).
    /// Sirve con un Slider, con una Image tipo "Filled", o con las dos. Todo es opcional.
    /// </summary>
    public class StatBarUI : MonoBehaviour
    {
        public CampaignManager campaign;
        [Tooltip("La variable a mostrar. Ej: Stat_Tolerancia.")]
        public StatData stat;

        [Header("Visual (usá uno o los dos)")]
        public Slider slider;
        [Tooltip("Image con Image Type = Filled.")]
        public Image fillImage;
        public TMP_Text valueText;
        [Tooltip("{0} = valor, {1} = máximo, {2} = nombre.")]
        public string format = "{2}: {0}/{1}";

        [Header("Color según el valor (opcional)")]
        public bool useColors = true;
        public Color highColor = new Color(0.30f, 0.75f, 0.35f);
        public Color midColor = new Color(0.95f, 0.75f, 0.20f);
        public Color lowColor = new Color(0.85f, 0.25f, 0.20f);

        GameState subscribed;

        void Awake()
        {
            if (campaign == null) campaign = FindAnyObjectByType<CampaignManager>();
        }

        void OnEnable()
        {
            if (campaign == null) return;
            campaign.OnCampaignStarted += Attach;
            Attach();
        }

        void OnDisable()
        {
            if (campaign != null) campaign.OnCampaignStarted -= Attach;
            Detach();
        }

        void Attach()
        {
            Detach();
            if (campaign.State == null) return;
            subscribed = campaign.State;
            subscribed.OnStatChanged += HandleChanged;
            Refresh();
        }

        void Detach()
        {
            if (subscribed != null) subscribed.OnStatChanged -= HandleChanged;
            subscribed = null;
        }

        void HandleChanged(StatData changed, int before, int after)
        {
            if (changed == stat) Refresh();
        }

        void Refresh()
        {
            if (stat == null || subscribed == null) return;
            int value = subscribed.GetStat(stat);
            float t = Mathf.InverseLerp(stat.minValue, stat.maxValue, value);

            if (slider != null)
            {
                slider.minValue = stat.minValue;
                slider.maxValue = stat.maxValue;
                slider.SetValueWithoutNotify(value);
            }
            if (fillImage != null)
            {
                fillImage.fillAmount = t;
                if (useColors) fillImage.color = t > 0.5f ? Color.Lerp(midColor, highColor, (t - 0.5f) * 2f)
                                                          : Color.Lerp(lowColor, midColor, t * 2f);
            }
            if (valueText != null) valueText.text = string.Format(format, value, stat.maxValue, stat.DisplayName);
        }
    }
}
