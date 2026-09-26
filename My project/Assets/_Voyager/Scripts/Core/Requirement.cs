using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Qué valor del juego mira un Requirement.
    /// Para agregar un tipo nuevo: sumar un valor acá y un "case" en Requirement.GetValue
    /// (y en RequirementDrawer si usa un campo nuevo). Nada más.
    /// </summary>
    public enum RequirementType
    {
        StatValue,                 // stat OP valor
        RegionRelation,            // relación de region OP valor
        AllRegionsRelation,        // TODAS las regiones OP valor ("mantener todas por encima de -20")
        AnyRegionRelation,         // ALGUNA región OP valor ("una región llegó a -100")

        ApprovedToday,             // aprobadas hoy OP valor
        RejectedToday,             // rechazadas hoy OP valor
        ApprovedTodayFromRegion,   // aprobadas hoy de region OP valor
        RejectedTodayFromRegion,   // rechazadas hoy de region OP valor
        ApprovedTodayInCategory,   // aprobadas hoy de category OP valor

        TotalApproved,             // aprobadas en toda la partida OP valor
        TotalRejected,             // rechazadas en toda la partida OP valor
        DaysCompleted,             // días terminados OP valor
        CurrentDay,                // día actual OP valor (útil para disponibilidad de propuestas)
        ConditionsFailed,          // condiciones diarias incumplidas OP valor
        ConsecutiveRejectionsOfRegion, // rechazos seguidos de propuestas de region OP valor (D7: "ignorar" un bloque)

        FlagSet,                   // el flag está activo
        FlagNotSet,                // el flag NO está activo

        // (agregados al final para no cambiar el número de los anteriores en assets ya guardados)
        ProposalApproved,          // la propuesta X fue aprobada en algún momento de la partida
        ProposalRejected,          // la propuesta X fue rechazada en algún momento de la partida
        TotalApprovedFromRegion,   // aprobadas de region en toda la partida OP valor
        TotalApprovedInCategory    // aprobadas de category en toda la partida OP valor
    }

    /// <summary>
    /// Una pregunta sobre el estado del juego: [Tipo] [Objetivo] [Operador] [Valor].
    /// Se usa en condiciones diarias, finales y disponibilidad de propuestas.
    /// </summary>
    [Serializable]
    public class Requirement
    {
        public RequirementType type = RequirementType.StatValue;
        public Comparison comparison = Comparison.GreaterOrEqual;
        public int value;

        public StatData stat;
        public RegionData region;
        public CategoryData category;
        public string flag;
        public ProposalData proposal;

        public bool Evaluate(GameState state)
        {
            switch (type)
            {
                case RequirementType.FlagSet:    return state.HasFlag(flag);
                case RequirementType.FlagNotSet: return !state.HasFlag(flag);
                case RequirementType.ProposalApproved: return state.WasDecided(proposal, true);
                case RequirementType.ProposalRejected: return state.WasDecided(proposal, false);

                case RequirementType.AllRegionsRelation:
                    foreach (var r in state.Regions)
                        if (!comparison.Compare(state.GetRelation(r), value)) return false;
                    return true;

                case RequirementType.AnyRegionRelation:
                    foreach (var r in state.Regions)
                        if (comparison.Compare(state.GetRelation(r), value)) return true;
                    return false;

                default:
                    return comparison.Compare(GetValue(state), value);
            }
        }

        /// <summary>El valor actual que se compara (para requisitos numéricos simples).</summary>
        public int GetValue(GameState state)
        {
            switch (type)
            {
                case RequirementType.StatValue:               return state.GetStat(stat);
                case RequirementType.RegionRelation:          return state.GetRelation(region);
                case RequirementType.ApprovedToday:           return state.CountToday(true);
                case RequirementType.RejectedToday:           return state.CountToday(false);
                case RequirementType.ApprovedTodayFromRegion: return state.CountToday(true, region: region);
                case RequirementType.RejectedTodayFromRegion: return state.CountToday(false, region: region);
                case RequirementType.ApprovedTodayInCategory: return state.CountToday(true, category: category);
                case RequirementType.TotalApproved:           return state.CountTotal(true);
                case RequirementType.TotalRejected:           return state.CountTotal(false);
                case RequirementType.DaysCompleted:           return state.DaysCompleted;
                case RequirementType.CurrentDay:              return state.CurrentDay;
                case RequirementType.ConditionsFailed:        return state.ConditionsFailed;
                case RequirementType.ConsecutiveRejectionsOfRegion: return state.GetConsecutiveRejections(region);
                case RequirementType.TotalApprovedFromRegion: return state.CountTotal(true, region: region);
                case RequirementType.TotalApprovedInCategory: return state.CountTotal(true, category: category);
                default:
                    Debug.LogWarning($"[Voyager] RequirementType {type} no tiene valor numérico.");
                    return 0;
            }
        }

        public string Describe()
        {
            string op = comparison.Symbol();
            switch (type)
            {
                case RequirementType.StatValue:               return $"{Name(stat)} {op} {value}";
                case RequirementType.RegionRelation:          return $"Relación {Name(region)} {op} {value}";
                case RequirementType.AllRegionsRelation:      return $"Todas las regiones {op} {value}";
                case RequirementType.AnyRegionRelation:       return $"Alguna región {op} {value}";
                case RequirementType.ApprovedToday:           return $"Aprobadas hoy {op} {value}";
                case RequirementType.RejectedToday:           return $"Rechazadas hoy {op} {value}";
                case RequirementType.ApprovedTodayFromRegion: return $"Aprobadas hoy de {Name(region)} {op} {value}";
                case RequirementType.RejectedTodayFromRegion: return $"Rechazadas hoy de {Name(region)} {op} {value}";
                case RequirementType.ApprovedTodayInCategory: return $"Aprobadas hoy de {Name(category)} {op} {value}";
                case RequirementType.TotalApproved:           return $"Aprobadas totales {op} {value}";
                case RequirementType.TotalRejected:           return $"Rechazadas totales {op} {value}";
                case RequirementType.DaysCompleted:           return $"Días completados {op} {value}";
                case RequirementType.CurrentDay:              return $"Día actual {op} {value}";
                case RequirementType.ConditionsFailed:        return $"Condiciones incumplidas {op} {value}";
                case RequirementType.ConsecutiveRejectionsOfRegion: return $"Rechazos seguidos de {Name(region)} {op} {value}";
                case RequirementType.FlagSet:                 return $"Flag '{flag}' activo";
                case RequirementType.FlagNotSet:              return $"Flag '{flag}' inactivo";
                case RequirementType.ProposalApproved:        return $"Aprobaste '{Name(proposal)}'";
                case RequirementType.ProposalRejected:        return $"Rechazaste '{Name(proposal)}'";
                case RequirementType.TotalApprovedFromRegion: return $"Aprobadas de {Name(region)} en la partida {op} {value}";
                case RequirementType.TotalApprovedInCategory: return $"Aprobadas de {Name(category)} en la partida {op} {value}";
                default:                                      return type.ToString();
            }
        }

        static string Name(ContentAsset a) => a != null ? a.DisplayName : "(¡sin asignar!)";
    }

    /// <summary>Lista de requisitos combinados con AND (All) u OR (Any). Vacía = siempre se cumple.</summary>
    [Serializable]
    public class RequirementGroup
    {
        public LogicMode mode = LogicMode.All;
        public List<Requirement> requirements = new List<Requirement>();

        public bool IsEmpty => requirements == null || requirements.Count == 0;

        public bool Evaluate(GameState state)
        {
            if (IsEmpty) return true;

            if (mode == LogicMode.All)
            {
                foreach (var r in requirements)
                    if (!r.Evaluate(state)) return false;
                return true;
            }

            foreach (var r in requirements)
                if (r.Evaluate(state)) return true;
            return false;
        }

        public string Describe()
        {
            if (IsEmpty) return "(sin requisitos)";
            var sb = new StringBuilder();
            string joiner = mode == LogicMode.All ? " Y " : " O ";
            for (int i = 0; i < requirements.Count; i++)
            {
                if (i > 0) sb.Append(joiner);
                sb.Append(requirements[i].Describe());
            }
            return sb.ToString();
        }

        /// <summary>Detalle con ✔/✘ por requisito, para los logs.</summary>
        public string DescribeWithResults(GameState state)
        {
            if (IsEmpty) return "  (sin requisitos) ✔";
            var sb = new StringBuilder();
            foreach (var r in requirements)
                sb.Append("  ").Append(r.Evaluate(state) ? "✔ " : "✘ ").AppendLine(r.Describe());
            sb.Append("  Modo: ").Append(mode == LogicMode.All ? "TODAS (AND)" : "ALGUNA (OR)");
            return sb.ToString();
        }
    }
}
