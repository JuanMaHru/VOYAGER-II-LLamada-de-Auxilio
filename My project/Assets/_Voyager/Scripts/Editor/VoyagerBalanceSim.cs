using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Juega muchas partidas automáticas con la campaña REAL para ver cómo quedó el balance:
    /// qué finales salen, cuántos puntos se juntan y si alguna condición resultó imposible.
    /// El "jugador" simulado intenta cumplir la condición del día el 80% de las veces.
    /// Menú: Voyager → Simular 1000 partidas (balance).
    /// </summary>
    public static class VoyagerBalanceSim
    {
        const string ConfigPath = "Assets/_Voyager/Data/CampaignConfig.asset";

        [MenuItem("Voyager/Simular 1000 partidas (balance)", priority = 41)]
        public static void Run()
        {
            var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>(ConfigPath);
            if (config == null) { Debug.LogError($"[Voyager] No existe {ConfigPath}."); return; }
            Simulate(config, 1000, 0.8); // jugador atento
            Simulate(config, 1000, 0.5); // jugador distraído
        }

        public static void Simulate(CampaignConfig config, int games, double complianceChance)
        {
            bool logs = GameLog.Enabled;
            var rnd = new System.Random(1);
            var endings = new Dictionary<string, int>();
            var scores = new List<int>();
            var stat = config.stats.FirstOrDefault(s => s != null && s.Id == "tolerancia") ?? config.stats.FirstOrDefault(s => s != null);
            int early = 0, compliantDays = 0, impossible = 0, totalDays = 0, metDays = 0, demandDays = 0, withReactions = 0;

            try
            {
                for (int g = 0; g < games; g++)
                {
                    var go = new GameObject("VoyagerSim") { hideFlags = HideFlags.HideAndDontSave };
                    var cm = go.AddComponent<CampaignManager>();
                    var pm = go.AddComponent<ProposalManager>();
                    cm.startOnPlay = false;
                    cm.enableDebugLogs = false;
                    var so = new SerializedObject(cm);
                    so.FindProperty("config").objectReferenceValue = config;
                    so.FindProperty("proposalManager").objectReferenceValue = pm;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    var spm = new SerializedObject(pm);
                    spm.FindProperty("campaign").objectReferenceValue = cm;
                    spm.ApplyModifiedPropertiesWithoutUndo();
                    GameLog.Enabled = false;

                    cm.StartNewCampaign();
                    int guard = 200;
                    while (cm.Phase != CampaignPhase.Finished && guard-- > 0)
                    {
                        if (cm.Phase == CampaignPhase.Deciding)
                        {
                            bool comply = rnd.NextDouble() < complianceChance;
                            if (cm.CurrentDay.condition != null && cm.CurrentDay.condition.regionDemands.Count > 0) demandDays++;
                            int completedBefore = cm.State.DaysCompleted;
                            PlayDay(cm, rnd, comply);
                            if (cm.State.DaysCompleted == completedBefore) continue; // despido en medio del día: el día no se cerró
                            totalDays++;
                            if (cm.LastDayResult.conditionMet) metDays++;
                            if (comply && cm.CurrentDay.condition != null)
                            {
                                compliantDays++;
                                if (!cm.LastDayResult.conditionMet) impossible++;
                            }
                        }
                        else if (cm.Phase == CampaignPhase.DayEnded) cm.NextDay();
                    }
                    if (cm.Phase == CampaignPhase.Finished && cm.FinalReactions.Count > 0) withReactions++;

                    string ending = cm.FinalEnding != null ? cm.FinalEnding.DisplayName : "(ninguno)";
                    endings[ending] = (endings.TryGetValue(ending, out int n) ? n : 0) + 1;
                    if (stat != null) scores.Add(cm.State.GetStat(stat));
                    if (cm.State.DaysCompleted < config.TotalDays) early++;
                    Object.DestroyImmediate(go);
                }
            }
            finally { GameLog.Enabled = logs; }

            scores.Sort();
            var sb = new StringBuilder($"<b>[Voyager] Simulación de {games} partidas</b> (jugador que intenta cumplir el {complianceChance:P0} de los días)\n");
            sb.AppendLine("Finales:");
            foreach (var kv in endings.OrderByDescending(k => k.Value))
                sb.AppendLine($"  {kv.Key}: {kv.Value} ({(float)kv.Value / games:P0})");
            if (scores.Count > 0)
                sb.AppendLine($"{stat.DisplayName} final → mín {scores[0]} · 25% {P(scores, 0.25)} · mediana {P(scores, 0.5)} · 75% {P(scores, 0.75)} · máx {scores[scores.Count - 1]}");
            sb.AppendLine($"Partidas terminadas antes de tiempo (despido): {early} ({(float)early / games:P0})");
            sb.AppendLine($"Partidas con reacciones alienígenas en el final: {(float)withReactions / games:P0}");
            sb.AppendLine($"Días con condición cumplida: {(float)metDays / Mathf.Max(1, totalDays):P0} · días con exigencias de regiones: {(float)demandDays / Mathf.Max(1, totalDays):P0}");
            sb.AppendLine(impossible == 0
                ? $"✔ Condiciones imposibles: 0 (de {compliantDays} días jugados intentando cumplir)"
                : $"✘ Condiciones imposibles: {impossible} de {compliantDays}");

            if (impossible == 0) Debug.Log(sb.ToString());
            else Debug.LogError(sb.ToString());
        }

        /// <summary>Decide todas las propuestas del día. Si comply = true, intenta cumplir la condición.</summary>
        static void PlayDay(CampaignManager cm, System.Random rnd, bool comply)
        {
            var pm = cm.Proposals;
            var cond = cm.CurrentDay.condition;
            int n = pm.Proposals.Count;
            var plan = new bool?[n];

            if (comply && cond != null)
            {
                foreach (var d in cond.regionDemands)
                    for (int i = 0; i < n; i++)
                        if (pm.Proposals[i].region == d.target) plan[i] = d.type == DemandType.ApproveAll;

                var approveReq = cond.requirements.requirements
                    .FirstOrDefault(r => r.type == RequirementType.ApprovedToday && r.comparison == Comparison.Equal);
                int target = approveReq != null ? approveReq.value : n / 2;
                int approved = plan.Count(x => x == true);

                var free = Enumerable.Range(0, n).Where(i => plan[i] == null).OrderBy(_ => rnd.Next()).ToList();
                foreach (int i in free) { plan[i] = approved < target; if (approved < target) approved++; }
            }

            int day = cm.State.CurrentDay;
            for (int i = 0; i < n && cm.Phase == CampaignPhase.Deciding && cm.State.CurrentDay == day; i++)
                pm.Decide(i, plan[i] ?? rnd.Next(2) == 0);
        }

        static int P(List<int> sorted, double q) => sorted[Mathf.Clamp((int)(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
    }
}
