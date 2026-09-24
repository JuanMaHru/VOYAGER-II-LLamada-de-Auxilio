using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Voyager.EditorTools
{
    /// <summary>
    /// Revisa el contenido en busca de errores comunes ANTES de dar Play:
    /// IDs duplicados, propuestas sin región/categoría, efectos sin objetivo,
    /// días sin propuestas suficientes, finales sin configurar, etc.
    /// </summary>
    public static class ContentValidator
    {
        [MenuItem("Voyager/Validar contenido", priority = 20)]
        public static void ValidateFromMenu()
        {
            var guids = AssetDatabase.FindAssets("t:CampaignConfig");
            if (guids.Length == 0) { Debug.LogWarning("[Voyager] No hay ningún CampaignConfig en el proyecto."); return; }
            foreach (var g in guids)
                Validate(AssetDatabase.LoadAssetAtPath<CampaignConfig>(AssetDatabase.GUIDToAssetPath(g)));
        }

        public static bool Validate(CampaignConfig config)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (config.database == null) errors.Add("No hay ProposalDatabase asignada.");
            if (config.days.Count == 0) errors.Add("No hay días configurados.");
            if (config.regions.Count == 0) errors.Add("No hay regiones.");
            if (config.defaultEnding == null) warnings.Add("No hay defaultEnding: si ningún final se cumple no se mostrará nada.");

            CheckDuplicates("Regiones", config.regions, errors);
            CheckDuplicates("Stats", config.stats, errors);
            CheckDuplicates("Categorías", config.categories, errors);
            CheckDuplicates("Condiciones", config.conditionPool, errors);
            CheckDuplicates("Finales", config.endings, errors);

            var proposals = config.database != null ? config.database.proposals.Where(p => p != null).ToList() : new List<ProposalData>();
            if (config.database != null)
            {
                CheckDuplicates("Propuestas", proposals, errors);
                if (config.database.proposals.Any(p => p == null)) warnings.Add("La ProposalDatabase tiene elementos vacíos.");

                foreach (var p in proposals)
                {
                    if (p.region == null) warnings.Add($"Propuesta '{p.name}' sin región.");
                    else if (!config.regions.Contains(p.region)) warnings.Add($"Propuesta '{p.name}': su región '{p.region.name}' no está en la campaña (nunca saldrá).");
                    if (p.category == null) warnings.Add($"Propuesta '{p.name}' sin categoría.");
                    if (p.onApprove.Count == 0 && p.onReject.Count == 0) warnings.Add($"Propuesta '{p.name}' no tiene efectos.");
                    CheckConsequences($"Propuesta '{p.name}' (aprobar)", p.onApprove, config, errors);
                    CheckConsequences($"Propuesta '{p.name}' (rechazar)", p.onReject, config, errors);
                    CheckRequirements($"Propuesta '{p.name}' (disponibilidad)", p.availability, config, errors);
                }

                int needed = config.days.Sum(d => d.proposalCount);
                if (proposals.Count < needed)
                    errors.Add($"La campaña necesita {needed} propuestas en total y la base tiene {proposals.Count} (no se repiten).");

                // Categorías fijas: ¿alcanzan?
                var byCategory = proposals.Where(p => p.category != null).GroupBy(p => p.category).ToDictionary(g => g.Key, g => g.Count());
                var fixedNeeds = new Dictionary<CategoryData, int>();
                foreach (var d in config.days)
                    if (d.categoryMode == CategoryMode.Fixed && d.fixedCategory != null)
                        fixedNeeds[d.fixedCategory] = (fixedNeeds.TryGetValue(d.fixedCategory, out int n) ? n : 0) + d.proposalCount;
                foreach (var kv in fixedNeeds)
                {
                    int have = byCategory.TryGetValue(kv.Key, out int h) ? h : 0;
                    if (have < kv.Value)
                        warnings.Add($"Los días con categoría fija '{kv.Key.name}' piden {kv.Value} propuestas y hay {have}. El mazo completará con otras categorías.");
                }
            }

            for (int i = 0; i < config.days.Count; i++)
            {
                var d = config.days[i];
                string tag = $"Día {i + 1}";
                if (d.categoryMode == CategoryMode.Fixed && d.fixedCategory == null) errors.Add($"{tag}: categoría Fixed sin asignar.");
                if (d.conditionMode == ConditionMode.Fixed && d.fixedCondition == null) errors.Add($"{tag}: condición Fixed sin asignar.");
                if (d.conditionMode == ConditionMode.Fixed && d.fixedCondition != null && d.fixedCondition.MinProposalsNeeded() > d.proposalCount)
                    errors.Add($"{tag}: la condición '{d.fixedCondition.name}' necesita {d.fixedCondition.MinProposalsNeeded()} propuestas y el día tiene {d.proposalCount}.");
                if (d.conditionMode == ConditionMode.RandomFromPool && config.conditionPool.Count == 0)
                    warnings.Add($"{tag}: condición aleatoria pero el pool está vacío.");
                if (d.categoryMode == CategoryMode.RandomFromPool && config.categories.Count == 0)
                    warnings.Add($"{tag}: categoría aleatoria pero la lista de categorías está vacía.");
            }

            foreach (var c in config.conditionPool.Where(c => c != null))
            {
                CheckRequirements($"Condición '{c.name}'", c.requirements, config, errors);
                CheckConsequences($"Condición '{c.name}' (éxito)", c.onSuccess, config, errors);
                CheckConsequences($"Condición '{c.name}' (falla)", c.onFail, config, errors);
            }
            foreach (var e in config.endings.Where(e => e != null))
                CheckRequirements($"Final '{e.name}'", e.requirements, config, errors);
            foreach (var r in config.regions.Where(r => r != null))
            {
                CheckConsequences($"Región '{r.name}' (crítico)", r.onCritical, config, errors);
                if (r.minRelation > r.maxRelation) errors.Add($"Región '{r.name}': mínimo mayor que máximo.");
                if (r.useCriticalThreshold && r.criticalThreshold < r.minRelation)
                    warnings.Add($"Región '{r.name}': el límite crítico ({r.criticalThreshold}) es menor que el mínimo ({r.minRelation}); nunca se alcanzará.");
            }

            var sb = new StringBuilder($"<b>[Voyager] Validación de '{config.name}'</b>: {errors.Count} errores, {warnings.Count} avisos\n");
            foreach (var e in errors) sb.AppendLine("✘ " + e);
            foreach (var w in warnings) sb.AppendLine("⚠ " + w);
            if (errors.Count == 0 && warnings.Count == 0) sb.AppendLine("✔ Todo en orden.");

            if (errors.Count > 0) Debug.LogError(sb.ToString(), config);
            else if (warnings.Count > 0) Debug.LogWarning(sb.ToString(), config);
            else Debug.Log(sb.ToString(), config);
            return errors.Count == 0;
        }

        static void CheckDuplicates<T>(string label, IEnumerable<T> items, List<string> errors) where T : ContentAsset
        {
            foreach (var g in items.Where(i => i != null).GroupBy(i => i.Id).Where(g => g.Count() > 1))
                errors.Add($"{label}: ID duplicado '{g.Key}' ({string.Join(", ", g.Select(x => x.name))}).");
        }

        static void CheckConsequences(string where, List<Consequence> list, CampaignConfig config, List<string> errors)
        {
            foreach (var c in list)
            {
                if (c.type == ConsequenceType.ModifyRegion && c.region == null) errors.Add($"{where}: ModifyRegion sin región.");
                if (c.type == ConsequenceType.ModifyRegion && c.region != null && !config.regions.Contains(c.region))
                    errors.Add($"{where}: la región '{c.region.name}' no está en CampaignConfig.regions.");
                if (c.type == ConsequenceType.ModifyStat && c.stat == null) errors.Add($"{where}: ModifyStat sin stat.");
                if (c.type == ConsequenceType.ModifyStat && c.stat != null && !config.stats.Contains(c.stat))
                    errors.Add($"{where}: el stat '{c.stat.name}' no está en CampaignConfig.stats.");
                if ((c.type == ConsequenceType.SetFlag || c.type == ConsequenceType.ClearFlag) && string.IsNullOrEmpty(c.flag))
                    errors.Add($"{where}: flag sin nombre.");
            }
        }

        static void CheckRequirements(string where, RequirementGroup group, CampaignConfig config, List<string> errors)
        {
            foreach (var r in group.requirements)
            {
                switch (r.type)
                {
                    case RequirementType.StatValue:
                        if (r.stat == null) errors.Add($"{where}: requisito de stat sin stat.");
                        else if (!config.stats.Contains(r.stat)) errors.Add($"{where}: el stat '{r.stat.name}' no está en la campaña.");
                        break;
                    case RequirementType.RegionRelation:
                    case RequirementType.ApprovedTodayFromRegion:
                    case RequirementType.RejectedTodayFromRegion:
                    case RequirementType.ConsecutiveRejectionsOfRegion:
                        if (r.region == null) errors.Add($"{where}: requisito '{r.type}' sin región.");
                        break;
                    case RequirementType.ApprovedTodayInCategory:
                        if (r.category == null) errors.Add($"{where}: requisito de categoría sin categoría.");
                        break;
                    case RequirementType.FlagSet:
                    case RequirementType.FlagNotSet:
                        if (string.IsNullOrEmpty(r.flag)) errors.Add($"{where}: requisito de flag sin nombre.");
                        break;
                }
            }
        }
    }
}
