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
            Decide(cm, ("P_Novena_Sinfonia", true), ("P_Raga", true), ("P_Rock_Argentino", false), ("P_Kpop", false), ("P_Flamenco", false));
            Check(cm.Phase == CampaignPhase.DayEnded, "El día se cierra solo al decidir la última propuesta");
            Check(cm.LastDayResult.conditionMet, "Aprobar 2 / Rechazar 3 → condición cumplida");
            Check(Stat(cm, "Culture") == 9, $"Culture = 5 + 4 = 9 (es {Stat(cm, "Culture")})");
            Check(Stat(cm, "Diplomacy") == 5, $"Diplomacy = 2 + 3 (bonus condición) = 5 (es {Stat(cm, "Diplomacy")})");
            Check(Rel(cm, "Europa") == 4, $"Europa = +10 - 6 = 4 (es {Rel(cm, "Europa")})");
            cm.NextDay();
            Check(cm.Phase == CampaignPhase.Finished, "Tras el último día, NextDay muestra el final");
            Check(cm.FinalEnding != null && cm.FinalEnding.name == "E_Mensaje_Humanidad", "Final = 'El mensaje de la humanidad'");
            Object.DestroyImmediate(go);
        }

        static void TestConditionFailedAndDefaultEnding(CampaignConfig config)
        {
            var (cm, go) = Build(config);
            cm.StartNewCampaign();
            Decide(cm, ("P_Novena_Sinfonia", true), ("P_Raga", true), ("P_Rock_Argentino", true), ("P_Kpop", true), ("P_Flamenco", true));
            Check(!cm.LastDayResult.conditionMet, "Aprobar 5 → condición incumplida");
            Check(Rel(cm, "Asia") == 13, $"Asia = 10 + 8 - 5 (castigo) = 13 (es {Rel(cm, "Asia")})");
            Check(cm.LastDayResult.narratives.Count == 1, "La falla genera 1 texto narrativo de cierre");
            Check(cm.State.ConditionsFailed == 1, "Se cuenta 1 condición fallida");
            cm.NextDay();
            Check(cm.FinalEnding != null && cm.FinalEnding.name == "E_Silencio",
                  "Culture 17 pero Diplomacy 2 → no cumple AND → final por defecto");
            Object.DestroyImmediate(go);
        }

        static void TestCriticalThreshold(CampaignConfig config)
        {
            var (cm, go) = Build(config);
            int narratives = 0;
            cm.OnNarrative += _ => narratives++;
            cm.StartNewCampaign();
            narratives = 0; // ignorar el texto de intro
            Decide(cm, ("P_Novena_Sinfonia", false), ("P_Flamenco", false), ("P_Raga", true), ("P_Kpop", true), ("P_Rock_Argentino", false));
            Check(Rel(cm, "Europa") == -16, $"Europa = -10 - 6 = -16 (es {Rel(cm, "Europa")})");
            Check(cm.State.HasFlag("europa_ofendida"), "Cruzar el límite crítico activa el flag");
            Check(Stat(cm, "Diplomacy") == -5 + 3, $"Diplomacy = -5 (crítico) + 3 (condición) = -2 (es {Stat(cm, "Diplomacy")})");
            Check(narratives == 2, $"2 narrativas durante el día: Flamenco + crítico de Europa (fueron {narratives})");
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
