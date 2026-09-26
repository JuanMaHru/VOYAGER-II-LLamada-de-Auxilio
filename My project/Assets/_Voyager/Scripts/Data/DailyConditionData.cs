using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Condición que el jugador debe cumplir durante un día.
    /// Ej: "Aprobá 2 y rechazá 3" = [Aprobadas hoy == 2] AND [Rechazadas hoy == 3].
    /// Puede incluir exigencias de regiones ("EEUU exige que apruebes su propuesta").
    /// Se evalúa al terminar el día. Puede ser un asset hecho a mano o generarse
    /// automáticamente cada día (ConditionMode.Generated, ver ConditionGenerator).
    /// </summary>
    [CreateAssetMenu(menuName = "Voyager/Daily Condition", fileName = "Condition_")]
    public class DailyConditionData : ContentAsset
    {
        [Tooltip("Texto que ve el jugador. Ej: \"Debés aprobar 2 propuestas y rechazar 3.\"")]
        [TextArea] public string playerText;

        public RequirementGroup requirements = new RequirementGroup();

        [Tooltip("Exigencias de regiones. TODAS deben cumplirse para que la condición se cumpla, " +
                 "y además cada una premia o castiga a la región que exige.")]
        public List<RegionDemand> regionDemands = new List<RegionDemand>();

        [Header("Consecuencias")]
        public List<Consequence> onSuccess = new List<Consequence>();
        public List<Consequence> onFail = new List<Consequence>();

        public string PlayerText
        {
            get
            {
                if (!string.IsNullOrEmpty(playerText)) return playerText;
                var sb = new StringBuilder(requirements.Describe());
                foreach (var d in regionDemands) sb.Append(' ').Append(d.Describe());
                return sb.ToString();
            }
        }

        /// <summary>¿Se cumplió todo? (requisitos + exigencias de regiones)</summary>
        public bool IsMet(GameState state)
        {
            if (!requirements.Evaluate(state)) return false;
            foreach (var d in regionDemands)
                if (!d.IsFulfilled(state)) return false;
            return true;
        }

        /// <summary>Premia o castiga a cada región que exigió algo. Devuelve el detalle para el log.</summary>
        public string ApplyDemandEffects(GameState state)
        {
            var sb = new StringBuilder();
            foreach (var d in regionDemands)
            {
                bool ok = d.IsFulfilled(state);
                int delta = ok ? d.relationIfFulfilled : d.relationIfIgnored;
                string source = $"Exigencia de {d.demander?.DisplayName} {(ok ? "cumplida" : "ignorada")}";
                state.ModifyRelation(d.demander, delta, source);
                var extra = ok ? d.onFulfilled : d.onIgnored;
                Consequence.ApplyAll(extra, state, source);

                sb.Append($"  {(ok ? "✔" : "✘")} {d.Describe()} → {d.demander?.DisplayName} {(delta >= 0 ? "+" : "")}{delta}");
                foreach (var c in extra) sb.Append(" · ").Append(c.Describe());
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public string DescribeWithResults(GameState state)
        {
            var sb = new StringBuilder(requirements.DescribeWithResults(state));
            foreach (var d in regionDemands)
                sb.Append("\n  ").Append(d.IsFulfilled(state) ? "✔ " : "✘ ").Append(d.Describe());
            return sb.ToString();
        }

        // ---- Análisis para que el mazo no genere días imposibles (D4) ----
        // Solo aplica en modo All (AND): en modo Any alcanza con que se pueda cumplir otro requisito.

        /// <summary>Regiones que deben aparecer (repetidas si hacen falta varias).</summary>
        public List<RegionData> GetRequiredRegions()
        {
            var list = new List<RegionData>();
            foreach (var d in regionDemands)
                if (d.target != null) list.Add(d.target); // una exigencia sin propuestas de esa región no tendría sentido
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
