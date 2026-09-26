using System.Collections.Generic;
using UnityEngine;

namespace Voyager.UI
{
    /// <summary>
    /// La mesa con los sobres. Arrastrá tus sobres existentes (con ProposalCardUI) a "cards".
    /// Si hay más sobres que propuestas, los sobrantes se ocultan.
    /// Opcional: "cardPrefab" + "container" para crear sobres extra si un día trae más propuestas.
    /// </summary>
    public class ProposalBoardUI : MonoBehaviour
    {
        public ProposalManager proposalManager;
        public List<ProposalCardUI> cards = new List<ProposalCardUI>();

        [Header("Opcional: sobres extra")]
        public ProposalCardUI cardPrefab;
        public Transform container;

        void Awake()
        {
            if (proposalManager == null) proposalManager = FindAnyObjectByType<ProposalManager>();
        }

        void OnEnable()
        {
            if (proposalManager == null) return;
            proposalManager.OnProposalsChanged += HandleProposals;
            proposalManager.OnSelectionChanged += HandleSelection;
            proposalManager.OnProposalDecided += HandleDecided;
            HandleProposals(proposalManager.Proposals);
        }

        void OnDisable()
        {
            if (proposalManager == null) return;
            proposalManager.OnProposalsChanged -= HandleProposals;
            proposalManager.OnSelectionChanged -= HandleSelection;
            proposalManager.OnProposalDecided -= HandleDecided;
        }

        void HandleProposals(IReadOnlyList<ProposalData> proposals)
        {
            while (cards.Count < proposals.Count && cardPrefab != null)
                cards.Add(Instantiate(cardPrefab, container != null ? container : transform));

            if (cards.Count < proposals.Count)
                GameLog.Warn($"ProposalBoardUI tiene {cards.Count} sobres y el día trae {proposals.Count} propuestas.");

            for (int i = 0; i < cards.Count; i++)
            {
                if (i < proposals.Count) { cards[i].gameObject.SetActive(true); cards[i].Bind(proposalManager, i); }
                else cards[i].gameObject.SetActive(false);
            }
        }

        void HandleSelection(int index, ProposalData p) => RefreshAll();
        void HandleDecided(int index, ProposalData p, bool approved) => RefreshAll();

        void RefreshAll()
        {
            foreach (var c in cards) if (c != null && c.gameObject.activeSelf) c.RefreshState();
        }
    }
}
