using System;
using System.Collections.Generic;
using System.Text;

namespace Voyager
{
    /// <summary>Registro de una decisión tomada. Guarda IDs (no assets) para poder serializarse.</summary>
    [Serializable]
    public class DecisionRecord
    {
        public int day;
        public string proposalId;
        public string regionId;
        public string categoryId;
        public bool approved;
    }

    /// <summary>
    /// TODO el estado mutable de una partida. Es una clase C# común (no MonoBehaviour),
    /// así que es fácil de testear y, más adelante, de guardar/cargar: todo se indexa por ID.
    ///
    /// Reemplaza a RelationshipManager y ScoreManager: las relaciones y los stats
    /// se consultan y modifican acá.
    /// </summary>
    public class GameState
    {
        // ---------- Definiciones (solo lectura, vienen del CampaignConfig) ----------
        readonly List<RegionData> regions;
        readonly List<StatData> stats;

        public IReadOnlyList<RegionData> Regions => regions;
        public IReadOnlyList<StatData> Stats => stats;

        // ---------- Estado serializable ----------
        public int CurrentDay { get; private set; }
        public int TotalDays { get; }
        public int DaysCompleted { get; private set; }
        public int ConditionsFailed { get; private set; }

        readonly Dictionary<string, int> relations = new Dictionary<string, int>();
        readonly Dictionary<string, int> statValues = new Dictionary<string, int>();
        readonly Dictionary<string, int> consecutiveRejections = new Dictionary<string, int>();
        readonly HashSet<string> flags = new HashSet<string>();
        readonly HashSet<string> usedProposalIds = new HashSet<string>();   // "UsedProposals"
        readonly HashSet<string> usedConditionIds = new HashSet<string>();
        readonly HashSet<string> usedCategoryIds = new HashSet<string>();
        readonly List<DecisionRecord> history = new List<DecisionRecord>();

        public IReadOnlyCollection<string> Flags => flags;
        public IReadOnlyCollection<string> UsedProposalIds => usedProposalIds;
        public IReadOnlyList<DecisionRecord> History => history;

        // ---------- Pendientes que procesa el CampaignManager ----------
        readonly List<string> pendingNarratives = new List<string>();
        public bool CampaignEndRequested { get; private set; }
        public EndingData ForcedEnding { get; private set; }

        // ---------- Eventos (la UI puede escucharlos para refrescar barras, etc.) ----------
        public event Action<RegionData, int, int> OnRelationChanged; // región, antes, después
        public event Action<StatData, int, int> OnStatChanged;       // stat, antes, después
        public event Action<RegionData> OnRegionCritical;

        public GameState(CampaignConfig config)
        {
            regions = new List<RegionData>();
            foreach (var r in config.regions) if (r != null) regions.Add(r);
            stats = new List<StatData>();
            foreach (var s in config.stats) if (s != null) stats.Add(s);

            TotalDays = config.TotalDays;

            foreach (var r in regions) relations[r.Id] = Clamp(r.initialRelation, r.minRelation, r.maxRelation);
            foreach (var s in stats) statValues[s.Id] = Clamp(s.initialValue, s.minValue, s.maxValue);
        }

        // =====================================================================
        // Días
        // =====================================================================
        public void BeginDay(int day) => CurrentDay = day;
        public void CompleteDay(bool conditionMet)
        {
            DaysCompleted++;
            if (!conditionMet) ConditionsFailed++;
        }

        // =====================================================================
        // Relaciones (ex RelationshipManager)
        // =====================================================================
        public int GetRelation(RegionData region)
        {
            if (region == null) return 0;
            return relations.TryGetValue(region.Id, out int v) ? v : 0;
        }

        public bool HasRegion(RegionData region) => region != null && relations.ContainsKey(region.Id);

        public void ModifyRelation(RegionData region, int delta, string source)
        {
            if (region == null) { GameLog.Warn($"Consecuencia ModifyRegion sin región asignada ({source})."); return; }
            if (!HasRegion(region))
            {
                GameLog.Warn($"La región '{region.DisplayName}' no está en CampaignConfig.regions. Se ignora ({source}).");
                return;
            }
            if (delta == 0) return;

            int before = relations[region.Id];
            int after = Clamp(before + delta, region.minRelation, region.maxRelation);
            relations[region.Id] = after;
            OnRelationChanged?.Invoke(region, before, after);

            // D6: se dispara al CRUZAR el umbral hacia abajo; si se recupera, puede volver a dispararse.
            if (region.useCriticalThreshold && before > region.criticalThreshold && after <= region.criticalThreshold)
            {
                GameLog.Info($"<color=red>[TOLERANCIA]</color> {region.DisplayName} llegó a su límite crítico " +
                             $"({after} <= {region.criticalThreshold}).");
                OnRegionCritical?.Invoke(region);
                Consequence.ApplyAll(region.onCritical, this, $"Límite crítico de {region.DisplayName}");
            }
        }

        /// <summary>Solo para debug/carga: fija un valor directo (respeta mín/máx y dispara el umbral).</summary>
        public void SetRelation(RegionData region, int value, string source)
            => ModifyRelation(region, value - GetRelation(region), source);

        // =====================================================================
        // Stats globales (ex ScoreManager)
        // =====================================================================
        public int GetStat(StatData stat)
        {
            if (stat == null) return 0;
            return statValues.TryGetValue(stat.Id, out int v) ? v : 0;
        }

        public bool HasStat(StatData stat) => stat != null && statValues.ContainsKey(stat.Id);

        public void ModifyStat(StatData stat, int delta, string source)
        {
            if (stat == null) { GameLog.Warn($"Consecuencia ModifyStat sin stat asignado ({source})."); return; }
            if (!HasStat(stat))
            {
                GameLog.Warn($"El stat '{stat.DisplayName}' no está en CampaignConfig.stats. Se ignora ({source}).");
                return;
            }
            if (delta == 0) return;

            int before = statValues[stat.Id];
            int after = Clamp(before + delta, stat.minValue, stat.maxValue);
            statValues[stat.Id] = after;
            OnStatChanged?.Invoke(stat, before, after);

            // Igual que las regiones: se dispara al CRUZAR el límite hacia abajo.
            if (stat.useCriticalThreshold && before > stat.criticalThreshold && after <= stat.criticalThreshold)
            {
                GameLog.Info($"<color=red>[LÍMITE]</color> {stat.DisplayName} llegó a su límite crítico ({after} <= {stat.criticalThreshold}).");
                Consequence.ApplyAll(stat.onCritical, this, $"Límite crítico de {stat.DisplayName}");
            }
        }

        public void SetStat(StatData stat, int value, string source)
            => ModifyStat(stat, value - GetStat(stat), source);

        // =====================================================================
        // Flags
        // =====================================================================
        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void SetFlag(string flag, bool active)
        {
            if (string.IsNullOrEmpty(flag)) { GameLog.Warn("Consecuencia de flag sin nombre."); return; }
            bool changed = active ? flags.Add(flag) : flags.Remove(flag);
            if (changed) GameLog.Info($"[FLAG] '{flag}' → {(active ? "ACTIVO" : "inactivo")}");
        }

        // =====================================================================
        // Propuestas / historial
        // =====================================================================
        public bool IsProposalUsed(ProposalData p) => p != null && usedProposalIds.Contains(p.Id);
        public void MarkProposalUsed(ProposalData p) { if (p != null) usedProposalIds.Add(p.Id); }
        public void UnmarkProposalUsed(ProposalData p) { if (p != null) usedProposalIds.Remove(p.Id); }

        public bool IsConditionUsed(DailyConditionData c) => c != null && usedConditionIds.Contains(c.Id);
        public void MarkConditionUsed(DailyConditionData c) { if (c != null) usedConditionIds.Add(c.Id); }
        public void ClearUsedConditions() => usedConditionIds.Clear();

        public bool IsCategoryUsed(CategoryData c) => c != null && usedCategoryIds.Contains(c.Id);
        public void MarkCategoryUsed(CategoryData c) { if (c != null) usedCategoryIds.Add(c.Id); }

        public void RecordDecision(ProposalData p, bool approved)
        {
            history.Add(new DecisionRecord
            {
                day = CurrentDay,
                proposalId = p.Id,
                regionId = p.region != null ? p.region.Id : null,
                categoryId = p.category != null ? p.category.Id : null,
                approved = approved
            });

            if (p.region != null)
            {
                consecutiveRejections.TryGetValue(p.region.Id, out int streak);
                consecutiveRejections[p.region.Id] = approved ? 0 : streak + 1;
            }
        }

        public int CountToday(bool approved, RegionData region = null, CategoryData category = null)
        {
            int count = 0;
            foreach (var r in history)
            {
                if (r.day != CurrentDay || r.approved != approved) continue;
                if (region != null && r.regionId != region.Id) continue;
                if (category != null && r.categoryId != category.Id) continue;
                count++;
            }
            return count;
        }

        public int CountTotal(bool approved, RegionData region = null, CategoryData category = null)
        {
            int count = 0;
            foreach (var r in history)
            {
                if (r.approved != approved) continue;
                if (region != null && r.regionId != region.Id) continue;
                if (category != null && r.categoryId != category.Id) continue;
                count++;
            }
            return count;
        }

        /// <summary>¿Esta propuesta fue aprobada (true) / rechazada (false) en algún momento de la partida?</summary>
        public bool WasDecided(ProposalData p, bool approved)
        {
            if (p == null) return false;
            foreach (var r in history)
                if (r.proposalId == p.Id && r.approved == approved) return true;
            return false;
        }

        public int GetConsecutiveRejections(RegionData region)
            => region != null && consecutiveRejections.TryGetValue(region.Id, out int v) ? v : 0;

        // =====================================================================
        // Narrativa y fin de partida (los procesa el CampaignManager)
        // =====================================================================
        public void QueueNarrative(string text)
        {
            if (!string.IsNullOrEmpty(text)) pendingNarratives.Add(text);
        }

        public List<string> TakeNarratives()
        {
            var copy = new List<string>(pendingNarratives);
            pendingNarratives.Clear();
            return copy;
        }

        /// <summary>true = terminar YA (sin esperar al cierre del día).</summary>
        public bool EndImmediately { get; private set; }

        public void RequestCampaignEnd(EndingData ending, string source, bool immediate = false)
        {
            // Si ya había un final forzado, se mantiene el primero.
            if (!CampaignEndRequested || ForcedEnding == null) ForcedEnding = ending;
            CampaignEndRequested = true;
            EndImmediately |= immediate;
            GameLog.Info($"<color=red>[FIN SOLICITADO]</color> por: {source}" +
                         (ending != null ? $" → {ending.DisplayName}" : " (se evaluarán los finales)") +
                         (immediate ? ". Termina YA." : ". Se aplicará al cerrar el día."));
        }

        // =====================================================================
        // Snapshots para logs "antes → después"
        // =====================================================================
        public Dictionary<string, int> SnapshotRelations() => new Dictionary<string, int>(relations);
        public Dictionary<string, int> SnapshotStats() => new Dictionary<string, int>(statValues);

        public string DescribeChanges(Dictionary<string, int> relationsBefore, Dictionary<string, int> statsBefore)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[RELACIONES]");
            foreach (var r in regions)
                sb.AppendLine("  " + FormatDelta(r.DisplayName, relationsBefore[r.Id], relations[r.Id]));
            sb.AppendLine("[SCORES]");
            foreach (var s in stats)
                sb.AppendLine("  " + FormatDelta(s.DisplayName, statsBefore[s.Id], statValues[s.Id]));
            return sb.ToString();
        }

        public string DescribeFullState()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Día {CurrentDay}/{TotalDays} · Completados: {DaysCompleted} · Condiciones fallidas: {ConditionsFailed}");
            sb.AppendLine($"Aprobadas totales: {CountTotal(true)} · Rechazadas totales: {CountTotal(false)}");
            sb.AppendLine("[RELACIONES]");
            foreach (var r in regions) sb.AppendLine($"  {r.DisplayName}: {GetRelation(r)}");
            sb.AppendLine("[SCORES]");
            foreach (var s in stats) sb.AppendLine($"  {s.DisplayName}: {GetStat(s)}");
            sb.Append("[FLAGS] ").AppendLine(flags.Count == 0 ? "(ninguno)" : string.Join(", ", flags));
            return sb.ToString();
        }

        static string FormatDelta(string name, int before, int after)
        {
            int d = after - before;
            string delta = d > 0 ? $"<color=green>+{d}</color>" : d < 0 ? $"<color=red>{d}</color>" : "0";
            return $"{name}: {delta}  ({before} → {after})";
        }

        static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
