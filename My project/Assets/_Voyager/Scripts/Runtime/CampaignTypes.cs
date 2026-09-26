using System.Collections.Generic;

namespace Voyager
{
    public enum CampaignPhase
    {
        NotStarted,
        Deciding,   // el jugador está sellando propuestas
        DayEnded,   // día cerrado, esperando NextDay() (pantalla de resultado)
        Finished    // final elegido
    }

    /// <summary>Lo que la UI necesita saber al empezar un día.</summary>
    public class DayContext
    {
        public int day;
        public int totalDays;
        public DayConfig config;
        public CategoryData category;          // null = categorías mezcladas
        public DailyConditionData condition;   // null = sin condición
        public IReadOnlyList<ProposalData> proposals;

        public string ConditionText => condition != null ? condition.PlayerText : "Sin condición especial.";
        public string CategoryText => category != null ? category.DisplayName : "Varias";
    }

    /// <summary>Resumen del día al cerrarlo (para la pantalla de resultado).</summary>
    public class DayResult
    {
        public int day;
        public DailyConditionData condition;
        public bool conditionMet;
        public int approved;
        public int rejected;
        public int undecided;
        public List<string> narratives = new List<string>(); // consecuencias narrativas del cierre del día
        public bool campaignWillEnd;                          // el próximo NextDay() mostrará el final
    }
}
