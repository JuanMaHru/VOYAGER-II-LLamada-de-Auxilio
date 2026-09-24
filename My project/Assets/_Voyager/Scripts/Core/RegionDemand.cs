using System;

namespace Voyager
{
    public enum DemandType
    {
        ApproveAll, // aprobar TODAS las propuestas de la región objetivo de ese día
        RejectAll   // rechazar TODAS las propuestas de la región objetivo de ese día
    }

    /// <summary>
    /// Exigencia de una región dentro de la condición del día.
    /// Ej: "EEUU exige que apruebes su propuesta" (demander = target = EEUU, ApproveAll)
    ///     "Asia exige que rechaces las propuestas de América Latina" (demander = Asia, target = América Latina, RejectAll)
    /// Si se cumple, la región que exige mejora su relación; si se ignora, empeora.
    /// </summary>
    [Serializable]
    public class RegionDemand
    {
        public RegionData demander;
        public RegionData target;
        public DemandType type = DemandType.ApproveAll;
        public int relationIfFulfilled = 5;
        public int relationIfIgnored = -10;

        [UnityEngine.Tooltip("Consecuencias extra si se cumple (ej: Tolerancia del Comité +7).")]
        public System.Collections.Generic.List<Consequence> onFulfilled = new System.Collections.Generic.List<Consequence>();
        [UnityEngine.Tooltip("Consecuencias extra si se ignora (ej: Tolerancia del Comité -20).")]
        public System.Collections.Generic.List<Consequence> onIgnored = new System.Collections.Generic.List<Consequence>();

        /// <summary>Se evalúa al cerrar el día, cuando todas las propuestas ya fueron selladas.</summary>
        public bool IsFulfilled(GameState state)
        {
            return type == DemandType.ApproveAll
                ? state.CountToday(false, region: target) == 0
                : state.CountToday(true, region: target) == 0;
        }

        /// <param name="targetCount">Cuántas propuestas de la región objetivo hay hoy (para singular/plural). 0 = desconocido.</param>
        public string Describe(int targetCount = 0)
        {
            string d = demander != null ? demander.DisplayName : "(¡sin región!)";
            string t = target != null ? target.DisplayName : "(¡sin región!)";
            bool one = targetCount == 1;
            string verb = type == DemandType.ApproveAll ? "apruebes" : "rechaces";

            if (demander == target)
                return $"{d} exige que {verb} {(one ? "su propuesta" : "sus propuestas")}.";
            return $"{d} exige que {verb} {(one ? "la propuesta" : "las propuestas")} de {t}.";
        }
    }
}
