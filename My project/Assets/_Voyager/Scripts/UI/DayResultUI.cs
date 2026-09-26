using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Panel de resultado al cerrar el día (condición cumplida o no + narrativa).
    /// El botón "Continuar" llama CampaignManager.NextDay().
    /// Si CampaignManager.autoAdvanceDay está activo, este panel no es necesario.
    /// </summary>
    public class DayResultUI : MonoBehaviour
    {
        public CampaignManager campaign;
        [Tooltip("El panel que se muestra/oculta. Si queda vacío se usa este mismo GameObject " +
                 "(en ese caso este script debe estar en un objeto que NO se desactive; conviene usar un hijo).")]
        public GameObject panel;

        public TMP_Text titleText;       // "DÍA 3 — CONDICIÓN CUMPLIDA"
        public TMP_Text summaryText;     // "Aprobadas 2 · Rechazadas 3"
        public TMP_Text narrativeText;
        public Button continueButton;

        public string successTitle = "DÍA {0} — CONDICIÓN CUMPLIDA";
        public string failTitle = "DÍA {0} — CONDICIÓN INCUMPLIDA";
        public string noConditionTitle = "DÍA {0} — JORNADA TERMINADA";

        void Awake()
        {
            if (campaign == null) campaign = FindAnyObjectByType<CampaignManager>();
            if (panel == null) panel = gameObject;
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            campaign.OnDayEnded += Show;
            campaign.OnDayStarted += Hide;
            campaign.OnCampaignEnded += Hide;
            if (panel != gameObject) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (campaign == null) return;
            campaign.OnDayEnded -= Show;
            campaign.OnDayStarted -= Hide;
            campaign.OnCampaignEnded -= Hide;
        }

        void Show(DayResult r)
        {
            if (campaign.autoAdvanceDay) return;
            panel.SetActive(true);

            string title = r.condition == null ? noConditionTitle : r.conditionMet ? successTitle : failTitle;
            if (titleText != null) titleText.text = string.Format(title, r.day);
            if (summaryText != null)
                summaryText.text = $"Aprobadas {r.approved} · Rechazadas {r.rejected}" +
                                   (r.condition != null ? $"\n{r.condition.PlayerText}" : "");
            if (narrativeText != null) narrativeText.text = string.Join("\n\n", r.narratives);
        }

        void Hide(DayContext _) => Hide();
        void Hide(EndingData _) => Hide();
        void Hide() { if (panel != gameObject) panel.SetActive(false); }

        void OnContinue() => campaign.NextDay();
    }
}
