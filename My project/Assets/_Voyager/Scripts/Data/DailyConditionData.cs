using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Condición que el jugador debe cumplir durante un día.
    /// Ej: "Aprobá 2 y rechazá 3" = [Aprobadas hoy == 2] AND [Rechazadas hoy == 3].
    /// Se evalúa al terminar el día.
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Daily Condition", fileName = "Condition_")]
    public class DailyConditionData : ContentAsset
    {
        [Tooltip("Texto que ve el jugador. Ej: \"Debés aprobar 2 propuestas y rechazar 3.\"")]
        [TextArea] public string playerText;

        public RequirementGroup requirements = new RequirementGroup();

        [Header("Consecuencias")]
        public List<Consequence> onSuccess = new List<Consequence>();
        public List<Consequence> onFail = new List<Consequence>();

        public string PlayerText => string.IsNullOrEmpty(playerText) ? requirements.Describe() : playerText;

        // ---- Análisis para que el mazo no genere días imposibles (D4) ----
        // Solo aplica en modo All (AND): en modo Any alcanza con que se pueda cumplir otro requisito.

        /// <summary>Regiones que deben aparecer (repetidas si hacen falta varias).</summary>
        public List<RegionData> GetRequiredRegions()
        {
            var list = new List<RegionData>();
            if (requirements.mode != LogicMode.All) return list;
            foreach (var r in requirements.requirements)
            {
                if (r.region == null) continue;
                if (r.type != RequirementType.ApprovedTodayFromRegion && r.type != RequirementType.RejectedTodayFromRegion) continue;
                int n = r.comparison.MinimumCountNeeded(r.value);
                for (int i = 0; i < n; i++) list.Add(r.region);
            }
            return list;
        }

        /// <summary>Categorías que deben aparecer (repetidas si hacen falta varias).</summary>
        public List<CategoryData> GetRequiredCategories()
        {
            var list = new List<CategoryData>();
            if (requirements.mode != LogicMode.All) return list;
            foreach (var r in requirements.requirements)
            {
                if (r.category == null || r.type != RequirementType.ApprovedTodayInCategory) continue;
                int n = r.comparison.MinimumCountNeeded(r.value);
                for (int i = 0; i < n; i++) list.Add(r.category);
            }
            return list;
        }

        /// <summary>Mínimo de propuestas en el día para que la condición sea cumplible (aprox.).</summary>
        public int MinProposalsNeeded()
        {
            if (requirements.mode != LogicMode.All) return 0;
            int approved = 0, rejected = 0;
            foreach (var r in requirements.requirements)
            {
                if (r.type == RequirementType.ApprovedToday) approved = System.Math.Max(approved, r.comparison.MinimumCountNeeded(r.value));
                if (r.type == RequirementType.RejectedToday) rejected = System.Math.Max(rejected, r.comparison.MinimumCountNeeded(r.value));
            }
            return approved + rejected;
        }
    }
}
