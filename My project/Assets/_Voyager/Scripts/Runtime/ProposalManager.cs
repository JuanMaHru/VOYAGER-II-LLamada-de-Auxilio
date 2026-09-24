using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// Propuestas del día y selección actual. Es la API que usa la UI:
    ///   Select(i)            → el jugador toca un sobre
    ///   ApproveProposal()    → sello APROBAR (sobre la seleccionada)
    ///   RejectProposal()     → sello DESAPROBAR
    /// No calcula puntajes: le pasa la decisión al CampaignManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProposalManager : MonoBehaviour
    {
        [SerializeField] CampaignManager campaign;

        [Tooltip("Tras sellar una propuesta, seleccionar automáticamente la siguiente pendiente (D1).")]
        public bool autoSelectNext = true;

        readonly List<ProposalData> proposals = new List<ProposalData>();
        readonly List<bool?> decisions = new List<bool?>(); // null = pendiente
        int selectedIndex = -1;

        // ---------------- Eventos para la UI ----------------
        public event Action<IReadOnlyList<ProposalData>> OnProposalsChanged;
        public event Action<int, ProposalData> OnSelectionChanged;         // índice (-1 = nada), propuesta
        public event Action<int, ProposalData, bool> OnProposalDecided;    // índice, propuesta, aprobada

        // ---------------- Consultas ----------------
        public IReadOnlyList<ProposalData> Proposals => proposals;
        public int SelectedIndex => selectedIndex;
        public ProposalData SelectedProposal => IsValid(selectedIndex) ? proposals[selectedIndex] : null;
        public bool IsDecided(int index) => IsValid(index) && decisions[index].HasValue;
        public bool? GetDecision(int index) => IsValid(index) ? decisions[index] : null;
        public bool CanDecideSelected => CanDecide(selectedIndex);

        public int PendingCount
        {
            get { int n = 0; foreach (var d in decisions) if (!d.HasValue) n++; return n; }
        }
        public bool AllDecided => PendingCount == 0;

        void Reset() => campaign = GetComponent<CampaignManager>();

        void Awake()
        {
            if (campaign == null) campaign = GetComponent<CampaignManager>();
        }

        // =====================================================================
        // Lo llama el CampaignManager al empezar el día
        // =====================================================================
        public void SetProposals(List<ProposalData> list)
        {
            proposals.Clear();
            decisions.Clear();
            proposals.AddRange(list);
            foreach (var _ in list) decisions.Add(null);
            selectedIndex = -1;

            OnProposalsChanged?.Invoke(proposals);
            Select(autoSelectNext && proposals.Count > 0 ? 0 : -1);
        }

        // =====================================================================
        // API para la UI
        // =====================================================================
        public void Select(int index)
        {
            if (index != -1 && !IsValid(index)) return;
            selectedIndex = index;
            OnSelectionChanged?.Invoke(selectedIndex, SelectedProposal);
        }

        public void Select(ProposalData proposal) => Select(proposals.IndexOf(proposal));

        /// <summary>Sello APROBAR sobre la propuesta seleccionada.</summary>
        public bool ApproveProposal() => Decide(selectedIndex, true);

        /// <summary>Sello DESAPROBAR sobre la propuesta seleccionada.</summary>
        public bool RejectProposal() => Decide(selectedIndex, false);

        public bool ApproveProposal(ProposalData p) => Decide(proposals.IndexOf(p), true);
        public bool RejectProposal(ProposalData p) => Decide(proposals.IndexOf(p), false);

        public bool Decide(int index, bool approved)
        {
            if (!CanDecide(index)) return false;

            var proposal = proposals[index];
            decisions[index] = approved; // D1: el sello es definitivo
            OnProposalDecided?.Invoke(index, proposal, approved);

            if (autoSelectNext) SelectNextPending(index);

            // Último paso: puede cerrar el día (y, con autoAdvanceDay, arrancar el siguiente).
            campaign.RegisterDecision(proposal, approved);
            return true;
        }

        bool CanDecide(int index)
        {
            if (campaign == null || campaign.Phase != CampaignPhase.Deciding) return false;
            if (!IsValid(index)) return false;
            return !decisions[index].HasValue;
        }

        void SelectNextPending(int from)
        {
            for (int k = 1; k <= proposals.Count; k++)
            {
                int i = (from + k) % proposals.Count;
                if (!decisions[i].HasValue) { Select(i); return; }
            }
            Select(-1);
        }

        bool IsValid(int index) => index >= 0 && index < proposals.Count;
    }
}
