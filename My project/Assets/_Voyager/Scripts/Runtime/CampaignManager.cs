using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Random = System.Random;

namespace Voyager
{
    /// <summary>
    /// Orquesta la partida: días, condición, categoría, propuestas, cierre del día y final.
    /// Es el único punto de entrada de la escena. La UI escucha sus eventos y llama
    /// NextDay() / StartNewCampaign(); las decisiones entran por ProposalManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class CampaignManager : MonoBehaviour
    {
        [SerializeField] CampaignConfig config;
        [SerializeField] ProposalManager proposalManager;

        [Tooltip("Empezar una partida nueva automáticamente al dar Play.")]
        public bool startOnPlay = true;

        [Tooltip("Si está activo, al cerrar un día arranca el siguiente sin esperar a la UI. " +
                 "Si no, la UI debe llamar NextDay() (ej: botón 'Continuar' en la pantalla de resultado).")]
        public bool autoAdvanceDay = false;

        [Tooltip("Logs detallados de gameplay en la consola.")]
        public bool enableDebugLogs = true;

        // ---------------- Estado público (solo lectura) ----------------
        public CampaignConfig Config => config;
        public ProposalManager Proposals => proposalManager;
        public GameState State { get; private set; }
        public CampaignPhase Phase { get; private set; } = CampaignPhase.NotStarted;
        public DayContext CurrentDay { get; private set; }
        public DayResult LastDayResult { get; private set; }
        public EndingData FinalEnding { get; private set; }

        // ---------------- Eventos para la UI ----------------
        public event Action OnCampaignStarted;
        public event Action<DayContext> OnDayStarted;
        public event Action<ProposalData, bool> OnDecisionApplied; // propuesta, aprobada (ya aplicados los efectos)
        public event Action<string> OnNarrative;                   // textos durante el día (intro, efectos de propuestas)
        public event Action<DayResult> OnDayEnded;
        public event Action<EndingData> OnCampaignEnded;

        Random rng;
        ProposalDeck deck;

        // =====================================================================
        // Ciclo de vida
        // =====================================================================
        void Reset() => proposalManager = GetComponent<ProposalManager>();

        void Awake()
        {
            if (proposalManager == null) proposalManager = GetComponent<ProposalManager>();
            GameLog.Enabled = enableDebugLogs;
        }

        void OnValidate() => GameLog.Enabled = enableDebugLogs;

        void Start()
        {
            if (startOnPlay) StartNewCampaign();
        }

        // =====================================================================
        // API principal
        // =====================================================================
        public void StartNewCampaign()
        {
            if (!ValidateSetup()) return;

            int seed = config.useFixedSeed ? config.seed : Environment.TickCount;
            rng = new Random(seed);
            State = new GameState(config);
            deck = new ProposalDeck(config.database);
            FinalEnding = null;
            LastDayResult = null;

            GameLog.Info($"<b>══════ NUEVA PARTIDA ══════</b>  (semilla {seed}, {config.TotalDays} días, " +
                         $"{config.database.proposals.Count} propuestas en la base)\n{State.DescribeFullState()}");

            OnCampaignStarted?.Invoke();
            StartDay(1);
        }

        /// <summary>Pasa al día siguiente o, si corresponde, muestra el final. Llamar desde la UI tras el resultado del día.</summary>
        public bool NextDay()
        {
            if (Phase == CampaignPhase.Finished) return false;
            if (Phase == CampaignPhase.Deciding)
            {
                GameLog.Warn("NextDay() llamado con el día todavía en curso. Terminá las propuestas primero (o usá Debug → Terminar día).");
                return false;
            }

            if (State.CampaignEndRequested || State.CurrentDay >= config.TotalDays) EndCampaign(State.ForcedEnding);
            else StartDay(State.CurrentDay + 1);
            return true;
        }

        /// <summary>
        /// Lo llama ProposalManager cuando el jugador sella una propuesta.
        /// Aplica efectos, registra la decisión y cierra el día si ya no quedan propuestas.
        /// </summary>
        public void RegisterDecision(ProposalData proposal, bool approved)
        {
            if (Phase != CampaignPhase.Deciding) { GameLog.Warn("Decisión ignorada: no hay un día en curso."); return; }

            var relBefore = State.SnapshotRelations();
            var statsBefore = State.SnapshotStats();

            State.RecordDecision(proposal, approved);
            Consequence.ApplyAll(proposal.GetEffects(approved), State, $"Propuesta '{proposal.DisplayName}'");

            GameLog.Info($"<b>[PROPUESTA]</b> {proposal.RegionName} - {proposal.CategoryName}: \"{proposal.DisplayName}\"\n" +
                         $"Resultado: {(approved ? "<color=green>APROBADA</color>" : "<color=red>RECHAZADA</color>")}\n" +
                         State.DescribeChanges(relBefore, statsBefore) +
                         $"Hoy → Aprobadas: {State.CountToday(true)} · Rechazadas: {State.CountToday(false)} · " +
                         $"Pendientes: {proposalManager.PendingCount}");

            foreach (var text in State.TakeNarratives()) OnNarrative?.Invoke(text);
            OnDecisionApplied?.Invoke(proposal, approved);

            if (State.EndImmediately) { EndCampaign(State.ForcedEnding); return; } // ej: la tolerancia llegó a 0
            if (proposalManager.AllDecided) EndDay();
        }

        // =====================================================================
        // Flujo interno del día
        // =====================================================================
        void StartDay(int dayNumber)
        {
            State.BeginDay(dayNumber);
            var dayCfg = config.days[dayNumber - 1];

            // Generated: primero salen las propuestas y la condición se arma a partir de ellas.
            bool generated = dayCfg.conditionMode == ConditionMode.Generated;
            var condition = generated ? null : ChooseCondition(dayCfg);
            var category = ChooseCategory(dayCfg, condition);
            var proposals = deck.Draw(BuildDrawRequest(dayCfg, category, condition), State, rng);
            foreach (var p in proposals) State.MarkProposalUsed(p);
            if (generated) condition = GenerateCondition(proposals, dayNumber);

            CurrentDay = new DayContext
            {
                day = dayNumber,
                totalDays = config.TotalDays,
                config = dayCfg,
                category = category,
                condition = condition,
                proposals = proposals
            };
            Phase = CampaignPhase.Deciding;

            LogDayStart();

            proposalManager.SetProposals(proposals);
            if (!string.IsNullOrEmpty(dayCfg.introText)) OnNarrative?.Invoke(dayCfg.introText);
            OnDayStarted?.Invoke(CurrentDay);

            if (proposals.Count == 0)
            {
                GameLog.Error($"Día {dayNumber} sin propuestas: el mazo está vacío. Se cierra el día automáticamente.");
                EndDay();
            }
        }

        void EndDay()
        {
            if (Phase != CampaignPhase.Deciding) return;

            var condition = CurrentDay.condition;
            bool met = condition == null || condition.IsMet(State);

            var relBefore = State.SnapshotRelations();
            var statsBefore = State.SnapshotStats();

            var log = new StringBuilder($"<b>[FIN DEL DÍA {State.CurrentDay}]</b>\n");
            log.AppendLine($"Aprobadas: {State.CountToday(true)} · Rechazadas: {State.CountToday(false)}");
            if (condition != null)
            {
                log.AppendLine($"Condición: {condition.PlayerText}");
                log.AppendLine(condition.DescribeWithResults(State));
                log.AppendLine(met ? "<color=green>Condición CUMPLIDA.</color>" : "<color=red>Condición INCUMPLIDA.</color>");

                if (condition.regionDemands.Count > 0)
                    log.Append("[EXIGENCIAS]\n").Append(condition.ApplyDemandEffects(State));

                var consequences = met ? condition.onSuccess : condition.onFail;
                Consequence.ApplyAll(consequences, State, $"Condición '{condition.DisplayName}' {(met ? "cumplida" : "incumplida")}");
                if (consequences.Count > 0)
                {
                    log.AppendLine("[CONSECUENCIAS]");
                    foreach (var c in consequences) log.AppendLine("  " + c.Describe());
                }
                if (condition.regionDemands.Count > 0 || consequences.Count > 0)
                    log.Append(State.DescribeChanges(relBefore, statsBefore));
            }
            else log.AppendLine("Sin condición.");

            State.CompleteDay(met);
            Phase = CampaignPhase.DayEnded;

            LastDayResult = new DayResult
            {
                day = State.CurrentDay,
                condition = condition,
                conditionMet = met,
                approved = State.CountToday(true),
                rejected = State.CountToday(false),
                undecided = proposalManager.PendingCount,
                narratives = State.TakeNarratives(),
                campaignWillEnd = State.CampaignEndRequested || State.CurrentDay >= config.TotalDays
            };
            if (LastDayResult.campaignWillEnd) log.AppendLine("→ La campaña termina.");
            GameLog.Info(log.ToString());

            OnDayEnded?.Invoke(LastDayResult);

            if (State.EndImmediately) { EndCampaign(State.ForcedEnding); return; }
            if (autoAdvanceDay) NextDay();
        }

        void EndCampaign(EndingData forced)
        {
            if (Phase == CampaignPhase.Finished) return;
            FinalEnding = forced != null ? forced : EndingResolver.Resolve(config.endings, config.defaultEnding, State);
            if (forced != null) GameLog.Info($"<b>[FINAL FORZADO]</b> {forced.DisplayName}");
            Phase = CampaignPhase.Finished;
            PickAlienReactions();

            var reactionsLog = new StringBuilder();
            foreach (var p in FinalReactions) reactionsLog.AppendLine($"  • {p.DisplayName}: {p.alienReaction}");
            GameLog.Info($"<b>══════ FIN DE LA PARTIDA ══════</b>\nFinal: {(FinalEnding != null ? FinalEnding.DisplayName : "(ninguno)")}\n" +
                         (FinalReactions.Count > 0 ? "[REACCIONES ALIENÍGENAS]\n" + reactionsLog : "") +
                         State.DescribeFullState());
            OnCampaignEnded?.Invoke(FinalEnding);
        }

        readonly List<ProposalData> finalReactions = new List<ProposalData>();

        /// <summary>Propuestas aprobadas cuya reacción alienígena se muestra en el final (ProposalData.alienReaction).</summary>
        public IReadOnlyList<ProposalData> FinalReactions => finalReactions;

        void PickAlienReactions()
        {
            finalReactions.Clear();
            if (FinalEnding == null || !FinalEnding.showAlienReactions || FinalEnding.maxReactions <= 0) return;

            // Candidatas: aprobadas con reacción que NO sean ya protagonistas del final elegido.
            var featured = new HashSet<ProposalData>();
            foreach (var r in FinalEnding.requirements.requirements)
                if (r.type == RequirementType.ProposalApproved && r.proposal != null) featured.Add(r.proposal);

            var candidates = new List<ProposalData>();
            foreach (var record in State.History)
            {
                if (!record.approved) continue;
                var p = config.database.FindById(record.proposalId);
                if (p != null && !featured.Contains(p) && !string.IsNullOrEmpty(p.alienReaction)) candidates.Add(p);
            }
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            for (int i = 0; i < candidates.Count && i < FinalEnding.maxReactions; i++) finalReactions.Add(candidates[i]);
        }

        // =====================================================================
        // Selección de condición / categoría (D3) y armado del pedido al mazo (D4)
        // =====================================================================
        DailyConditionData ChooseCondition(DayConfig dayCfg)
        {
            switch (dayCfg.conditionMode)
            {
                case ConditionMode.None:
                    return null;

                case ConditionMode.Fixed:
                    if (dayCfg.fixedCondition == null) GameLog.Warn($"Día {State.CurrentDay}: modo Fixed pero sin condición asignada.");
                    State.MarkConditionUsed(dayCfg.fixedCondition);
                    return dayCfg.fixedCondition;
            }

            var candidates = GetConditionCandidates(dayCfg, includeUsed: false);
            if (candidates.Count == 0)
            {
                candidates = GetConditionCandidates(dayCfg, includeUsed: true);
                if (candidates.Count > 0) GameLog.Info("Pool de condiciones agotado: se permiten repetidas.");
            }
            if (candidates.Count == 0)
            {
                GameLog.Warn($"Día {State.CurrentDay}: el pool de condiciones está vacío o ninguna es cumplible con " +
                             $"{dayCfg.proposalCount} propuestas. El día no tendrá condición.");
                return null;
            }

            var chosen = candidates[rng.Next(candidates.Count)];
            State.MarkConditionUsed(chosen);
            return chosen;
        }

        DailyConditionData lastGenerated; // instancia creada en runtime: se destruye al generar la siguiente

        DailyConditionData GenerateCondition(List<ProposalData> proposals, int dayNumber)
        {
            if (lastGenerated != null)
            {
                if (Application.isPlaying) Destroy(lastGenerated);
                else DestroyImmediate(lastGenerated);
            }
            lastGenerated = ConditionGenerator.Generate(proposals, State.Regions, config.conditionGenerator, rng, dayNumber);
            return lastGenerated;
        }

        List<DailyConditionData> GetConditionCandidates(DayConfig dayCfg, bool includeUsed)
        {
            var list = new List<DailyConditionData>();
            foreach (var c in config.conditionPool)
            {
                if (c == null) continue;
                if (!includeUsed && State.IsConditionUsed(c)) continue;
                if (c.MinProposalsNeeded() > dayCfg.proposalCount) continue; // imposible con tan pocas propuestas
                list.Add(c);
            }
            return list;
        }

        CategoryData ChooseCategory(DayConfig dayCfg, DailyConditionData condition)
        {
            CategoryData chosen;
            switch (dayCfg.categoryMode)
            {
                case CategoryMode.AnyCategory:
                    return null;
                case CategoryMode.Fixed:
                    chosen = dayCfg.fixedCategory;
                    if (chosen == null) GameLog.Warn($"Día {State.CurrentDay}: modo Fixed pero sin categoría asignada.");
                    break;
                default:
                    if (config.categories.Count == 0)
                    {
                        GameLog.Warn("CampaignConfig.categories está vacío: el día usará categorías mezcladas.");
                        return null;
                    }
                    chosen = deck.ChooseCategory(config.categories, State, dayCfg.proposalCount,
                                                 dayCfg.allowedRegions, config.avoidRepeatingCategories, rng);
                    break;
            }
            State.MarkCategoryUsed(chosen);
            return chosen;
        }

        DrawRequest BuildDrawRequest(DayConfig dayCfg, CategoryData category, DailyConditionData condition)
        {
            var req = new DrawRequest
            {
                count = dayCfg.proposalCount,
                category = category,
                distinctRegions = dayCfg.distinctRegions,
                allowedRegions = dayCfg.allowedRegions
            };
            if (condition != null)
            {
                req.mustIncludeRegions = condition.GetRequiredRegions();
                foreach (var c in condition.GetRequiredCategories())
                    if (c != category) req.mustIncludeCategories.Add(c); // si es la del día, ya está cubierta
            }
            return req;
        }

        void LogDayStart()
        {
            if (!GameLog.Enabled) return;
            var sb = new StringBuilder();
            sb.AppendLine($"<b>════ [DÍA {CurrentDay.day}/{CurrentDay.totalDays}] ════</b>");
            sb.AppendLine($"Categoría: {CurrentDay.CategoryText}");
            sb.AppendLine($"Condición: {CurrentDay.ConditionText}");
            sb.AppendLine("Propuestas:");
            for (int i = 0; i < CurrentDay.proposals.Count; i++)
            {
                var p = CurrentDay.proposals[i];
                sb.AppendLine($"  {i + 1}. {p.RegionName} - {p.DisplayName} ({p.CategoryName})");
            }
            GameLog.Info(sb.ToString());
        }

        bool ValidateSetup()
        {
            string error = null;
            if (config == null) error = "CampaignManager no tiene CampaignConfig asignado.";
            else if (config.database == null) error = "CampaignConfig no tiene ProposalDatabase asignada.";
            else if (config.TotalDays == 0) error = "CampaignConfig no tiene días configurados.";
            else if (proposalManager == null) error = "Falta el componente ProposalManager (ponelo en el mismo GameObject).";

            if (error != null) { GameLog.Error(error); return false; }
            return true;
        }

        // =====================================================================
        // DEBUG (no se compila en builds finales)
        // =====================================================================
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Cierra el día actual (las propuestas sin decidir quedan sin decidir) y pasa al siguiente.</summary>
        public void Debug_SkipDay()
        {
            if (Phase == CampaignPhase.Deciding) EndDay();
            if (Phase == CampaignPhase.DayEnded && !autoAdvanceDay) NextDay();
        }

        /// <summary>Vuelve a sortear las propuestas del día (solo si todavía no se decidió ninguna).</summary>
        public void Debug_RegenerateProposals()
        {
            if (Phase != CampaignPhase.Deciding) { GameLog.Warn("Solo durante un día en curso."); return; }
            if (proposalManager.PendingCount != proposalManager.Proposals.Count)
            {
                GameLog.Warn("Ya se decidieron propuestas hoy: no se pueden regenerar.");
                return;
            }
            foreach (var p in CurrentDay.proposals) State.UnmarkProposalUsed(p);
            bool generated = CurrentDay.config.conditionMode == ConditionMode.Generated;
            var proposals = deck.Draw(BuildDrawRequest(CurrentDay.config, CurrentDay.category, generated ? null : CurrentDay.condition), State, rng);
            foreach (var p in proposals) State.MarkProposalUsed(p);
            CurrentDay.proposals = proposals;
            if (generated) CurrentDay.condition = GenerateCondition(proposals, CurrentDay.day);
            GameLog.Info("[DEBUG] Propuestas regeneradas.");
            LogDayStart();
            proposalManager.SetProposals(proposals);
            OnDayStarted?.Invoke(CurrentDay);
        }

        public void Debug_ForceCondition(DailyConditionData condition)
        {
            if (Phase != CampaignPhase.Deciding) { GameLog.Warn("Solo durante un día en curso."); return; }
            CurrentDay.condition = condition;
            GameLog.Info($"[DEBUG] Condición forzada: {CurrentDay.ConditionText}");
            OnDayStarted?.Invoke(CurrentDay);
        }

        public void Debug_ModifyRegion(RegionData region, int delta)
        {
            if (State == null) return;
            var rel = State.SnapshotRelations(); var st = State.SnapshotStats();
            State.ModifyRelation(region, delta, "DEBUG");
            GameLog.Info("[DEBUG] Relación modificada\n" + State.DescribeChanges(rel, st));
            if (State.EndImmediately) EndCampaign(State.ForcedEnding);
        }

        public void Debug_ModifyStat(StatData stat, int delta)
        {
            if (State == null) return;
            var rel = State.SnapshotRelations(); var st = State.SnapshotStats();
            State.ModifyStat(stat, delta, "DEBUG");
            GameLog.Info("[DEBUG] Stat modificado\n" + State.DescribeChanges(rel, st));
            if (State.EndImmediately) EndCampaign(State.ForcedEnding);
        }

        public void Debug_ForceEnding(EndingData ending)
        {
            if (State == null) return;
            EndCampaign(ending);
        }

        /// <summary>Evalúa los finales con el estado actual SIN terminar la partida.</summary>
        public EndingData Debug_PreviewEnding()
            => State == null ? null : EndingResolver.Resolve(config.endings, config.defaultEnding, State);

        /// <summary>Decide al azar todas las propuestas pendientes del día.</summary>
        public void Debug_DecideRandomly()
        {
            if (Phase != CampaignPhase.Deciding) return;
            int day = State.CurrentDay;
            for (int i = 0; i < proposalManager.Proposals.Count && Phase == CampaignPhase.Deciding && State.CurrentDay == day; i++)
                if (!proposalManager.IsDecided(i)) proposalManager.Decide(i, rng.Next(2) == 0);
        }

        /// <summary>Juega la partida entera con decisiones al azar (para probar finales y balance).</summary>
        public void Debug_SimulateCampaign()
        {
            if (Phase == CampaignPhase.NotStarted || Phase == CampaignPhase.Finished) StartNewCampaign();
            int guard = 1000;
            while (Phase != CampaignPhase.Finished && guard-- > 0)
            {
                if (Phase == CampaignPhase.Deciding) Debug_DecideRandomly();
                else if (Phase == CampaignPhase.DayEnded) NextDay();
                else break;
            }
        }
#endif
    }
}
