using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Pruebas automáticas rápidas sobre el contenido de ejemplo (no hace falta dar Play).
    /// Menú: Voyager → Pruebas rápidas (contenido de ejemplo).
    /// </summary>
    public static class VoyagerSmokeTests
    {
        static readonly List<string> failures = new List<string>();
        static int checks;

        [MenuItem("Voyager/Pruebas rápidas (contenido de ejemplo)", priority = 40)]
        public static void Run()
        {
            failures.Clear();
            checks = 0;
            var config = AssetDatabase.LoadAssetAtPath<CampaignConfig>("Assets/_Voyager/Example/CampaignConfig_Example.asset");
            if (config == null)
            {
                Debug.LogError("[Voyager] Primero ejecutá Voyager → Crear contenido de ejemplo.");
                Finish();
                return;
            }

            bool logs = GameLog.Enabled;
            GameLog.Enabled = false; // pruebas silenciosas
            try
            {
                TestConditionMetAndGoodEnding(config);
                TestConditionFailedAndDefaultEnding(config);
                TestCriticalThreshold(config);
                TestNoRepeatsAndRandomSimulation(config);
                TestRequirementsAndClamp(config);
                TestGeneratedConditionsAlwaysFeasible(config);
                TestImmediateEndAndProposalRequirements(config);
            }
            finally { GameLog.Enabled = logs; }
            Finish();
        }

        // -------------------------------------------------------------------------------
        static void TestConditionMetAndGoodEnding(CampaignConfig config)
        {
            var (cm, go) = Build(config);
            cm.StartNewCampaign();
            Check(cm.Proposals.Proposals.Count == 5, "El día genera 5 propuestas");
            Decide(cm, ("P_Novena_Sinfonia", true), ("P_Raga", true), ("P_Rock_Argentino", false), ("P_Kpop", false), ("P_Michael_Jackson", false));
            Check(cm.Phase == CampaignPhase.DayEnded, "El día se cierra solo al decidir la última propuesta");
            Check(cm.LastDayResult.conditionMet, "Aprobar 2 / Rechazar 3 → condición cumplida");
            Check(Stat(cm, "Culture") == 9, $"Culture = 5 + 4 = 9 (es {Stat(cm, "Culture")})");
            Check(Stat(cm, "Diplomacy") == 5, $"Diplomacy = 2 + 3 (bonus condición) = 5 (es {Stat(cm, "Diplomacy")})");
            Check(Rel(cm, "Europa") == 10, $"Europa = +10 (es {Rel(cm, "Europa")})");
            Check(Rel(cm, "EEUU") == -10, $"EEUU = -10 por rechazar a Michael Jackson (es {Rel(cm, "EEUU")})");
            cm.NextDay();
            Check(cm.Phase == CampaignPhase.Finished, "Tras el último día, NextDay muestra el final");
            Check(cm.FinalEnding != null && cm.FinalEnding.name == "E_Mensaje_Humanidad", "Final = 'El mensaje de la humanidad'");
            Object.DestroyImmediate(go);
        }

        static void TestConditionFailedAndDefaultEnding(CampaignConfig config)
        {
            var (cm, go) = Build(config);
            cm.StartNewCampaign();
            Decide(cm, ("P_Novena_Sinfonia", true), ("P_Raga", true), ("P_Rock_Argentino", true), ("P_Kpop", true), ("P_Michael_Jackson", true));
            Check(!cm.LastDayResult.conditionMet, "Aprobar 5 → condición incumplida");
            Check(Rel(cm, "Asia") == 13, $"Asia = 10 + 8 - 5 (castigo) = 13 (es {Rel(cm, "Asia")})");
            Check(cm.LastDayResult.narratives.Count == 1, "La falla genera 1 texto narrativo de cierre");
            Check(cm.State.ConditionsFailed == 1, "Se cuenta 1 condición fallida");
            cm.NextDay();
            Check(cm.FinalEnding != null && cm.FinalEnding.name == "E_Silencio",
                  "Culture 18 pero Diplomacy 2 → no cumple AND → final por defecto");
            Object.DestroyImmediate(go);
        }

        static void TestCriticalThreshold(CampaignConfig config)
        {
            var (cm, go) = Build(config);
            int narratives = 0;
            cm.OnNarrative += _ => narratives++;
            cm.StartNewCampaign();
            narratives = 0; // ignorar el texto de intro
            Decide(cm, ("P_Novena_Sinfonia", false), ("P_Michael_Jackson", false), ("P_Raga", true), ("P_Kpop", true), ("P_Rock_Argentino", false));
            Check(Rel(cm, "Europa") == -10, $"Europa = -10, justo en su límite crítico (es {Rel(cm, "Europa")})");
            Check(cm.State.HasFlag("europa_ofendida"), "Cruzar el límite crítico activa el flag");
            Check(Stat(cm, "Diplomacy") == -5 + 3, $"Diplomacy = -5 (crítico) + 3 (condición) = -2 (es {Stat(cm, "Diplomacy")})");
            Check(narratives == 2, $"2 narrativas durante el día: rechazo a Michael Jackson + crítico de Europa (fueron {narratives})");
            cm.Debug_ModifyRegion(Region(cm, "Europa"), -10);
            Check(Stat(cm, "Diplomacy") == -2, "Bajar más allá del límite NO vuelve a disparar la consecuencia");
            Object.DestroyImmediate(go);
        }

        static void TestNoRepeatsAndRandomSimulation(CampaignConfig config)
        {
            for (int run = 0; run < 25; run++)
            {
                var (cm, go) = Build(config);
                cm.Debug_SimulateCampaign();
                Check(cm.Phase == CampaignPhase.Finished && cm.FinalEnding != null, $"Simulación {run}: termina con un final");
                var ids = cm.State.History.Select(h => h.proposalId).ToList();
                Check(ids.Count == ids.Distinct().Count(), $"Simulación {run}: ninguna propuesta se repite");
                cm.StartNewCampaign();
                Check(cm.State.History.Count == 0 && cm.State.UsedProposalIds.Count == 5,
                      $"Simulación {run}: reiniciar limpia el historial (solo quedan las 5 del nuevo día)");
                Object.DestroyImmediate(go);
            }
        }

        static void TestRequirementsAndClamp(CampaignConfig config)
        {
            var (cm, go) = Build(config);
            cm.StartNewCampaign();
            var europa = Region(cm, "Europa");
            cm.Debug_ModifyRegion(europa, 500);
            Check(Rel(cm, "Europa") == europa.maxRelation, "La relación se limita al máximo de la región");

            var anyLow = new Requirement { type = RequirementType.AnyRegionRelation, comparison = Comparison.LessOrEqual, value = -100 };
            Check(!anyLow.Evaluate(cm.State), "AnyRegionRelation <= -100 falso al inicio");
            cm.Debug_ModifyRegion(Region(cm, "Asia"), -1000);
            Check(anyLow.Evaluate(cm.State), "AnyRegionRelation <= -100 verdadero cuando Asia llega a -100");

            var group = new RequirementGroup
            {
                mode = LogicMode.Any,
                requirements = new List<Requirement>
                {
                    new Requirement { type = RequirementType.FlagSet, flag = "no_existe" },
                    new Requirement { type = RequirementType.CurrentDay, comparison = Comparison.Equal, value = 1 }
                }
            };
            Check(group.Evaluate(cm.State), "Grupo OR: alcanza con que se cumpla uno");
            group.mode = LogicMode.All;
            Check(!group.Evaluate(cm.State), "Grupo AND: falla si uno no se cumple");
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Genera miles de días al azar (1 a 6 propuestas, regiones repetidas o ausentes) y prueba
        /// TODAS las combinaciones de aprobar/rechazar: al menos una tiene que cumplir la condición.
        /// </summary>
        static void TestGeneratedConditionsAlwaysFeasible(CampaignConfig config)
        {
            var rng = new System.Random(7);
            var settings = new ConditionGeneratorSettings { demandChance = 1f, maxDemands = 3 };
            var regions = config.regions;
            int impossible = 0, withDemands = 0, badTotals = 0;

            for (int trial = 0; trial < 2000; trial++)
            {
                int n = 1 + rng.Next(6);
                var proposals = new List<ProposalData>();
                for (int i = 0; i < n; i++)
                {
                    var p = ScriptableObject.CreateInstance<ProposalData>();
                    p.name = $"T{i}";
                    p.region = regions[rng.Next(regions.Count)];
                    proposals.Add(p);
                }
                settings.demandChance = (float)rng.NextDouble();
                var cond = ConditionGenerator.Generate(proposals, regions, settings, rng, 1);
                if (cond.regionDemands.Count > 0) withDemands++;

                int a = cond.requirements.requirements[0].value, r = cond.requirements.requirements[1].value;
                if (a + r != n) badTotals++;

                bool feasible = false;
                for (int mask = 0; mask < (1 << n) && !feasible; mask++)
                {
                    var state = new GameState(config);
                    state.BeginDay(1);
                    for (int i = 0; i < n; i++) state.RecordDecision(proposals[i], (mask & (1 << i)) != 0);
                    feasible = cond.IsMet(state);
                }
                if (!feasible) impossible++;

                Object.DestroyImmediate(cond);
                foreach (var p in proposals) Object.DestroyImmediate(p);
            }
            Check(impossible == 0, $"Condiciones generadas: todas cumplibles (imposibles: {impossible} de 2000)");
            Check(badTotals == 0, "Condiciones generadas: aprobar + rechazar = total de propuestas");
            Check(withDemands > 0, "Condiciones generadas: algunas incluyen exigencias de regiones");
        }

        /// <summary>
        /// Una variable con límite crítico + EndCampaign inmediato termina la partida EN EL MOMENTO
        /// (sin esperar al cierre del día). Y los requisitos "aprobaste X" funcionan.
        /// </summary>
        static void TestImmediateEndAndProposalRequirements(CampaignConfig config)
        {
            var culture = config.stats.First(s => s.Id == "Culture");
            bool oldUse = culture.useCriticalThreshold;
            int oldThreshold = culture.criticalThreshold;
            var oldOnCritical = culture.onCritical;
            try
            {
                culture.useCriticalThreshold = true;
                culture.criticalThreshold = -1;
                culture.onCritical = new List<Consequence>
                {
                    new Consequence { type = ConsequenceType.SetFlag, flag = "despedido" },
                    new Consequence { type = ConsequenceType.EndCampaign, immediate = true }
                };

                // 1) Por debug
                var (cm, go) = Build(config);
                cm.StartNewCampaign();
                cm.Debug_ModifyStat(culture, -5);
                Check(cm.Phase == CampaignPhase.Finished, "Llegar al límite crítico con 'Ya' termina la partida en el momento");
                Check(cm.State.HasFlag("despedido"), "El límite crítico activa el flag de despido");
                Object.DestroyImmediate(go);

                // 2) En medio del día, por una propuesta
                culture.criticalThreshold = 4;
                (cm, go) = Build(config);
                cm.StartNewCampaign();
                cm.Debug_ModifyStat(culture, 10);          // 10
                var raga = cm.Proposals.Proposals.First(p => p.Id == "P_Raga");
                cm.Proposals.RejectProposal(raga);         // Raga rechazada: Diplomacia -2, Cultura igual
                cm.Debug_ModifyStat(culture, -6);          // 4 → cruza
                Check(cm.Phase == CampaignPhase.Finished && cm.State.DaysCompleted == 0,
                      "El despido ocurre en medio del día, sin cerrar el día");
                Object.DestroyImmediate(go);
            }
            finally
            {
                culture.useCriticalThreshold = oldUse;
                culture.criticalThreshold = oldThreshold;
                culture.onCritical = oldOnCritical;
            }

            // 3) Requisitos sobre propuestas concretas
            var (cm2, go2) = Build(config);
            cm2.StartNewCampaign();
            var kpop = cm2.Proposals.Proposals.First(p => p.Id == "P_Kpop");
            var approvedKpop = new Requirement { type = RequirementType.ProposalApproved, proposal = kpop };
            var rejectedKpop = new Requirement { type = RequirementType.ProposalRejected, proposal = kpop };
            Check(!approvedKpop.Evaluate(cm2.State), "'Aprobaste K-pop' es falso antes de decidir");
            cm2.Proposals.ApproveProposal(kpop);
            Check(approvedKpop.Evaluate(cm2.State) && !rejectedKpop.Evaluate(cm2.State), "'Aprobaste K-pop' es verdadero después de aprobarla");
            var asiaTotal = new Requirement { type = RequirementType.TotalApprovedFromRegion, region = kpop.region, comparison = Comparison.GreaterOrEqual, value = 1 };
            Check(asiaTotal.Evaluate(cm2.State), "Aprobadas de Asia en la partida >= 1");
            Object.DestroyImmediate(go2);
        }

        // ------------------------------------------------------------------ helpers
        static (CampaignManager, GameObject) Build(CampaignConfig config)
        {
            var go = new GameObject("VoyagerTest") { hideFlags = HideFlags.HideAndDontSave };
            var cm = go.AddComponent<CampaignManager>();
            var pm = go.AddComponent<ProposalManager>();
            cm.startOnPlay = false;
            cm.enableDebugLogs = false;
            GameLog.Enabled = false; // AddComponent dispara OnValidate, que lo había reactivado
            var so = new SerializedObject(cm);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("proposalManager").objectReferenceValue = pm;
            so.ApplyModifiedPropertiesWithoutUndo();
            var spm = new SerializedObject(pm);
            spm.FindProperty("campaign").objectReferenceValue = cm;
            spm.ApplyModifiedPropertiesWithoutUndo();
            return (cm, go);
        }

        static void Decide(CampaignManager cm, params (string id, bool approve)[] decisions)
        {
            foreach (var (id, approve) in decisions)
            {
                var p = cm.Proposals.Proposals.FirstOrDefault(x => x.Id == id);
                Check(p != null, $"La propuesta {id} está en el día");
                if (p == null) continue;
                bool ok = approve ? cm.Proposals.ApproveProposal(p) : cm.Proposals.RejectProposal(p);
                Check(ok, $"Se pudo decidir {id}");
            }
            var any = cm.Proposals.Proposals.FirstOrDefault();
            if (any != null) Check(!cm.Proposals.ApproveProposal(any), "No se puede volver a sellar una propuesta ya decidida");
        }

        static RegionData Region(CampaignManager cm, string id) => cm.State.Regions.First(r => r.Id == id);
        static int Rel(CampaignManager cm, string id) => cm.State.GetRelation(Region(cm, id));
        static int Stat(CampaignManager cm, string id) => cm.State.GetStat(cm.State.Stats.First(s => s.Id == id));

        static void Check(bool condition, string description)
        {
            checks++;
            if (!condition) failures.Add(description);
        }

        static void Finish()
        {
            if (failures.Count == 0) Debug.Log($"<color=green>[Voyager] Pruebas rápidas: {checks} verificaciones OK.</color>");
            else Debug.LogError($"[Voyager] Pruebas rápidas: {failures.Count} de {checks} fallaron:\n✘ " + string.Join("\n✘ ", failures));

            if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
        }
    }
}
