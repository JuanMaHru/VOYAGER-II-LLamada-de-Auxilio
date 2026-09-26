using System;
using System.Collections.Generic;

namespace Voyager
{
    /// <summary>
    /// Qué hace una Consequence.
    /// Para agregar un tipo nuevo: sumar un valor acá y un "case" en Consequence.Apply
    /// (y en ConsequenceDrawer si usa un campo nuevo). Nada más.
    /// </summary>
    public enum ConsequenceType
    {
        ModifyRegion,     // relación de region += amount
        ModifyAllRegions, // todas las regiones += amount
        ModifyStat,       // stat += amount
        SetFlag,          // activa un flag (sirve para desbloquear propuestas/finales)
        ClearFlag,        // desactiva un flag
        ShowNarrative,    // muestra un texto narrativo
        EndCampaign       // termina la partida al cerrar el día (con un final concreto o evaluando finales)
    }

    /// <summary>
    /// Un cambio sobre el estado del juego. Se usa en efectos de propuestas,
    /// consecuencias de condiciones diarias y consecuencias de umbral crítico de regiones.
    /// </summary>
    [Serializable]
    public class Consequence
    {
        public ConsequenceType type = ConsequenceType.ModifyStat;
        public int amount;

        public RegionData region;
        public StatData stat;
        public string flag;
        [UnityEngine.TextArea] public string text;

        [UnityEngine.Tooltip("Solo EndCampaign: final a mostrar. Vacío = evaluar los finales normalmente.")]
        public EndingData ending;

        [UnityEngine.Tooltip("Solo EndCampaign: terminar YA, sin esperar al cierre del día.")]
        public bool immediate;

        public void Apply(GameState state, string source)
        {
            switch (type)
            {
                case ConsequenceType.ModifyRegion:
                    state.ModifyRelation(region, amount, source);
                    break;

                case ConsequenceType.ModifyAllRegions:
                    foreach (var r in state.Regions)
                        state.ModifyRelation(r, amount, source);
                    break;

                case ConsequenceType.ModifyStat:
                    state.ModifyStat(stat, amount, source);
                    break;

                case ConsequenceType.SetFlag:
                    state.SetFlag(flag, true);
                    break;

                case ConsequenceType.ClearFlag:
                    state.SetFlag(flag, false);
                    break;

                case ConsequenceType.ShowNarrative:
                    state.QueueNarrative(text);
                    break;

                case ConsequenceType.EndCampaign:
                    state.RequestCampaignEnd(ending, source, immediate);
                    break;
            }
        }

        public string Describe()
        {
            string sign = amount >= 0 ? "+" : "";
            switch (type)
            {
                case ConsequenceType.ModifyRegion:     return $"{Name(region)} {sign}{amount}";
                case ConsequenceType.ModifyAllRegions: return $"Todas las regiones {sign}{amount}";
                case ConsequenceType.ModifyStat:       return $"{Name(stat)} {sign}{amount}";
                case ConsequenceType.SetFlag:          return $"Activar flag '{flag}'";
                case ConsequenceType.ClearFlag:        return $"Desactivar flag '{flag}'";
                case ConsequenceType.ShowNarrative:    return $"Narrativa: \"{Short(text)}\"";
                case ConsequenceType.EndCampaign:      return (immediate ? "FIN INMEDIATO" : "FIN DE PARTIDA") +
                                                              (ending != null ? $" → {ending.DisplayName}" : " (evaluar finales)");
                default:                               return type.ToString();
            }
        }

        public static void ApplyAll(List<Consequence> list, GameState state, string source)
        {
            if (list == null) return;
            foreach (var c in list) c?.Apply(state, source);
        }

        static string Name(ContentAsset a) => a != null ? a.DisplayName : "(¡sin asignar!)";
        static string Short(string s) => string.IsNullOrEmpty(s) ? "" : (s.Length > 40 ? s.Substring(0, 40) + "…" : s);
    }
}
