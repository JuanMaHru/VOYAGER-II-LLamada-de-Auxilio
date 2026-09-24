using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Panel de DEBUG en el Inspector del CampaignManager (solo en Play Mode).
    /// Muestra el estado en vivo y tiene botones para probar el sistema sin UI.
    /// Está en una carpeta Editor: no existe en la build.
    /// </summary>
    [CustomEditor(typeof(CampaignManager))]
    public class CampaignManagerEditor : Editor
    {
        RegionData debugRegion;
        StatData debugStat;
        int debugAmount = 10;
        DailyConditionData debugCondition;
        EndingData debugEnding;
        bool showState = true;

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var cm = (CampaignManager)target;
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("DEBUG", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Los controles de debug aparecen en Play Mode.", MessageType.Info);
                if (cm.Config != null && GUILayout.Button("Validar contenido")) ContentValidator.Validate(cm.Config);
                return;
            }

            if (cm.State == null)
            {
                if (GUILayout.Button("Empezar partida", GUILayout.Height(26))) cm.StartNewCampaign();
                return;
            }

            DrawState(cm);
            EditorGUILayout.Space(6);

            // ---- Flujo ----
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reiniciar partida")) cm.StartNewCampaign();
                GUI.enabled = cm.Phase == CampaignPhase.DayEnded;
                if (GUILayout.Button("Siguiente día (NextDay)")) cm.NextDay();
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = cm.Phase == CampaignPhase.Deciding;
                if (GUILayout.Button("Aprobar seleccionada")) cm.Proposals.ApproveProposal();
                if (GUILayout.Button("Rechazar seleccionada")) cm.Proposals.RejectProposal();
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = cm.Phase == CampaignPhase.Deciding;
                if (GUILayout.Button("Decidir resto al azar")) cm.Debug_DecideRandomly();
                if (GUILayout.Button("Regenerar propuestas")) cm.Debug_RegenerateProposals();
                GUI.enabled = cm.Phase == CampaignPhase.Deciding || cm.Phase == CampaignPhase.DayEnded;
                if (GUILayout.Button("Saltar día")) cm.Debug_SkipDay();
                GUI.enabled = true;
            }
            if (GUILayout.Button("Simular partida completa (al azar)")) cm.Debug_SimulateCampaign();

            // ---- Forzar ----
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Modificar valores", EditorStyles.miniBoldLabel);
            debugAmount = EditorGUILayout.IntField("Cantidad", debugAmount);
            using (new EditorGUILayout.HorizontalScope())
            {
                debugRegion = (RegionData)EditorGUILayout.ObjectField(debugRegion, typeof(RegionData), false);
                GUI.enabled = debugRegion != null;
                if (GUILayout.Button("Aplicar", GUILayout.Width(70))) cm.Debug_ModifyRegion(debugRegion, debugAmount);
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                debugStat = (StatData)EditorGUILayout.ObjectField(debugStat, typeof(StatData), false);
                GUI.enabled = debugStat != null;
                if (GUILayout.Button("Aplicar", GUILayout.Width(70))) cm.Debug_ModifyStat(debugStat, debugAmount);
                GUI.enabled = true;
            }

            EditorGUILayout.LabelField("Forzar", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                debugCondition = (DailyConditionData)EditorGUILayout.ObjectField(debugCondition, typeof(DailyConditionData), false);
                GUI.enabled = cm.Phase == CampaignPhase.Deciding;
                if (GUILayout.Button("Condición", GUILayout.Width(70))) cm.Debug_ForceCondition(debugCondition);
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                debugEnding = (EndingData)EditorGUILayout.ObjectField(debugEnding, typeof(EndingData), false);
                GUI.enabled = debugEnding != null && cm.Phase != CampaignPhase.Finished;
                if (GUILayout.Button("Final", GUILayout.Width(70))) cm.Debug_ForceEnding(debugEnding);
                GUI.enabled = true;
            }
            if (GUILayout.Button("¿Qué final saldría ahora? (log)")) cm.Debug_PreviewEnding();
        }

        void DrawState(CampaignManager cm)
        {
            showState = EditorGUILayout.Foldout(showState, $"Estado — {cm.Phase}", true);
            if (!showState) return;

            var s = cm.State;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (cm.Phase == CampaignPhase.Finished)
                    EditorGUILayout.LabelField("FINAL", cm.FinalEnding != null ? cm.FinalEnding.DisplayName : "(ninguno)", EditorStyles.boldLabel);

                if (cm.CurrentDay != null)
                {
                    EditorGUILayout.LabelField("Día", $"{s.CurrentDay} / {s.TotalDays}");
                    EditorGUILayout.LabelField("Categoría", cm.CurrentDay.CategoryText);
                    EditorGUILayout.LabelField("Condición", cm.CurrentDay.ConditionText, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Hoy", $"Aprobadas {s.CountToday(true)} · Rechazadas {s.CountToday(false)} · Pendientes {cm.Proposals.PendingCount}");
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Propuestas del día", EditorStyles.miniBoldLabel);
                var pm = cm.Proposals;
                for (int i = 0; i < pm.Proposals.Count; i++)
                {
                    var p = pm.Proposals[i];
                    var d = pm.GetDecision(i);
                    string mark = d == true ? "✔ APROBADA" : d == false ? "✘ RECHAZADA" : "…";
                    string sel = pm.SelectedIndex == i ? "► " : "   ";
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button($"{sel}{p.RegionName} - {p.DisplayName}", EditorStyles.label)) pm.Select(i);
                        GUILayout.Label(mark, GUILayout.Width(90));
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Relaciones", EditorStyles.miniBoldLabel);
                foreach (var r in s.Regions)
                {
                    int v = s.GetRelation(r);
                    var rect = EditorGUILayout.GetControlRect();
                    float t = Mathf.InverseLerp(r.minRelation, r.maxRelation, v);
                    EditorGUI.ProgressBar(rect, t, $"{r.DisplayName}: {v}" + (r.useCriticalThreshold ? $"  (crítico {r.criticalThreshold})" : ""));
                }

                EditorGUILayout.LabelField("Scores", EditorStyles.miniBoldLabel);
                foreach (var st in s.Stats) EditorGUILayout.LabelField(st.DisplayName, s.GetStat(st).ToString());

                EditorGUILayout.LabelField("Flags", s.Flags.Count == 0 ? "(ninguno)" : string.Join(", ", s.Flags));
                EditorGUILayout.LabelField("Usadas / fallidas", $"{s.UsedProposalIds.Count} propuestas · {s.ConditionsFailed} condiciones fallidas");
            }
        }
    }
}
