using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Voyager
{
    /// <summary>Ajustes de las condiciones generadas automáticamente (ConditionMode.Generated).</summary>
    [Serializable]
    public class ConditionGeneratorSettings
    {
        [Tooltip("Probabilidad de que el día incluya exigencias de regiones.")]
        [Range(0f, 1f)] public float demandChance = 0.6f;

        [Tooltip("Máximo de exigencias por día (cada una sobre una región distinta).")]
        [Range(0, 3)] public int maxDemands = 1;

        [Tooltip("Probabilidad de que la exigencia sea 'aprobá MI propuesta' en vez de 'rechazá la de OTRA región'.")]
        [Range(0f, 1f)] public float approveOwnChance = 0.5f;

        [Tooltip("Relación que gana la región que exige si cumplís su exigencia.")]
        public int relationIfFulfilled = 5;

        [Tooltip("Relación que pierde la región que exige si ignorás su exigencia.")]
        public int relationIfIgnored = -10;

        [Tooltip("Evitar 'aprobá todas' y 'rechazá todas' cuando haya otra opción.")]
        public bool avoidAllOrNothing = true;

        [Header("Condición del día (aprobar X / rechazar Y + exigencias)")]
        public List<Consequence> onSuccess = new List<Consequence>();
        public List<Consequence> onFail = new List<Consequence>();

        [Header("Cada exigencia de región")]
        public List<Consequence> onDemandFulfilled = new List<Consequence>();
        public List<Consequence> onDemandIgnored = new List<Consequence>();
    }

    /// <summary>
    /// Arma la condición del día A PARTIR de las propuestas que salieron, así siempre es cumplible:
    ///   1) opcionalmente elige exigencias de regiones que tengan propuestas hoy;
    ///   2) calcula cuántas aprobar/rechazar respetando esas exigencias:
    ///        mínimo a aprobar = propuestas de regiones con "aprobá todas"
    ///        máximo a aprobar = total - propuestas de regiones con "rechazá todas"
    ///   3) sortea la cantidad de aprobadas dentro de ese rango.
    /// </summary>
    public static class ConditionGenerator
    {
        public static DailyConditionData Generate(IReadOnlyList<ProposalData> proposals, IReadOnlyList<RegionData> regions,
                                                  ConditionGeneratorSettings s, Random rng, int day)
        {
            int total = proposals.Count;
            if (total == 0) return null;

            // Propuestas de hoy por región
            var countByRegion = new Dictionary<RegionData, int>();
            foreach (var p in proposals)
            {
                if (p.region == null) continue;
                countByRegion.TryGetValue(p.region, out int n);
                countByRegion[p.region] = n + 1;
            }

            // 1) Exigencias
            var demands = new List<RegionDemand>();
            if (s.maxDemands > 0 && countByRegion.Count > 0 && rng.NextDouble() < s.demandChance)
            {
                var targets = new List<RegionData>(countByRegion.Keys);
                Shuffle(targets, rng);
                int howMany = Math.Min(1 + rng.Next(s.maxDemands), targets.Count);

                for (int i = 0; i < howMany; i++)
                {
                    var target = targets[i];
                    var others = new List<RegionData>();
                    foreach (var r in regions) if (r != null && r != target) others.Add(r);

                    bool approveOwn = others.Count == 0 || rng.NextDouble() < s.approveOwnChance;
                    demands.Add(new RegionDemand
                    {
                        target = target,
                        demander = approveOwn ? target : others[rng.Next(others.Count)],
                        type = approveOwn ? DemandType.ApproveAll : DemandType.RejectAll,
                        relationIfFulfilled = s.relationIfFulfilled,
                        relationIfIgnored = s.relationIfIgnored,
                        onFulfilled = s.onDemandFulfilled,
                        onIgnored = s.onDemandIgnored
                    });
                }
            }

            // 2) Rango posible de aprobadas
            int minApprove = 0, maxApprove = total;
            foreach (var d in demands)
            {
                int n = countByRegion[d.target];
                if (d.type == DemandType.ApproveAll) minApprove += n;
                else maxApprove -= n;
            }

            int lo = minApprove, hi = maxApprove;
            if (s.avoidAllOrNothing && total >= 2)
            {
                int lo2 = Math.Max(minApprove, 1), hi2 = Math.Min(maxApprove, total - 1);
                if (lo2 <= hi2) { lo = lo2; hi = hi2; }
            }

            // 3) Cantidades
            int approve = rng.Next(lo, hi + 1);
            int reject = total - approve;

            var c = ScriptableObject.CreateInstance<DailyConditionData>();
            c.name = $"Condición generada (día {day})";
            c.requirements = new RequirementGroup
            {
                mode = LogicMode.All,
                requirements = new List<Requirement>
                {
                    new Requirement { type = RequirementType.ApprovedToday, comparison = Comparison.Equal, value = approve },
                    new Requirement { type = RequirementType.RejectedToday, comparison = Comparison.Equal, value = reject }
                }
            };
            c.regionDemands = demands;
            c.onSuccess = s.onSuccess;
            c.onFail = s.onFail;

            var text = new StringBuilder();
            if (reject == 0) text.Append($"Aprobá todas las propuestas ({total}).");
            else if (approve == 0) text.Append($"Rechazá todas las propuestas ({total}).");
            else text.Append($"Aprobá {approve} y rechazá {reject}.");
            foreach (var d in demands) text.Append('\n').Append(d.Describe(countByRegion[d.target]));
            c.playerText = text.ToString();
            return c;
        }

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
