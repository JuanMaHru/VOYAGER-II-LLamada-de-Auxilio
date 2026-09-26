using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Conecta los botones SELLO APROBAR / SELLO DESAPROBAR con el ProposalManager.
    /// Solo reenvía el click: toda la lógica está en el sistema de gameplay.
    /// </summary>
    public class StampButtonsUI : MonoBehaviour
    {
        public ProposalManager proposalManager;
        public Button approveButton;
        public Button rejectButton;

        void Awake()
        {
            if (proposalManager == null) proposalManager = FindAnyObjectByType<ProposalManager>();
            if (approveButton != null) approveButton.onClick.AddListener(() => proposalManager.ApproveProposal());
            if (rejectButton != null) rejectButton.onClick.AddListener(() => proposalManager.RejectProposal());
        }

        void OnEnable()
        {
            if (proposalManager == null) return;
            proposalManager.OnSelectionChanged += HandleSelection;
            proposalManager.OnProposalDecided += HandleDecided;
            Refresh();
        }

        void OnDisable()
        {
            if (proposalManager == null) return;
            proposalManager.OnSelectionChanged -= HandleSelection;
            proposalManager.OnProposalDecided -= HandleDecided;
        }

        void HandleSelection(int i, ProposalData p) => Refresh();
        void HandleDecided(int i, ProposalData p, bool approved) => Refresh();

        void Refresh()
        {
            bool can = proposalManager != null && proposalManager.CanDecideSelected;
            if (approveButton != null) approveButton.interactable = can;
            if (rejectButton != null) rejectButton.interactable = can;
        }
    }
}
