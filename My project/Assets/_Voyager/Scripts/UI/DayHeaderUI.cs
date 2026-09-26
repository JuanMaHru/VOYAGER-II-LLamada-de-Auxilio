using TMPro;
using UnityEngine;

namespace Voyager.UI
{
    /// <summary>Cabecera del día: número de día, categoría, condición y contadores.</summary>
    public class DayHeaderUI : MonoBehaviour
    {
        public CampaignManager campaign;

        public TMP_Text dayText;        // "DÍA 3 / 7"
        public TMP_Text categoryText;   // "MÚSICA"
        public TMP_Text conditionText;  // "Aprobá 2 y rechazá 3"
        public TMP_Text countersText;   // "Aprobadas 1 · Rechazadas 2 · Pendientes 2"

        [Tooltip("Formato del día. {0} = día actual, {1} = días totales.")]
        public string dayFormat = "DÍA {0} / {1}";

        void Awake()
        {
            if (campaign == null) campaign = FindAnyObjectByType<CampaignManager>();
        }

        void OnEnable()
        {
            if (campaign == null) return;
            campaign.OnDayStarted += HandleDay;
            campaign.OnDecisionApplied += HandleDecision;
            if (campaign.CurrentDay != null) HandleDay(campaign.CurrentDay);
        }

        void OnDisable()
        {
            if (campaign == null) return;
            campaign.OnDayStarted -= HandleDay;
            campaign.OnDecisionApplied -= HandleDecision;
        }

        void HandleDay(DayContext day)
        {
            if (dayText != null) dayText.text = string.Format(dayFormat, day.day, day.totalDays);
            if (categoryText != null) categoryText.text = day.CategoryText.ToUpperInvariant();
            if (conditionText != null) conditionText.text = day.ConditionText;
            RefreshCounters();
        }

        void HandleDecision(ProposalData p, bool approved) => RefreshCounters();

        void RefreshCounters()
        {
            if (countersText == null || campaign.State == null) return;
            countersText.text = $"Aprobadas {campaign.State.CountToday(true)} · " +
                                $"Rechazadas {campaign.State.CountToday(false)} · " +
                                $"Pendientes {campaign.Proposals.PendingCount}";
        }
    }
}
