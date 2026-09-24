using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Muestra UNA propuesta (un sobre/carta). Ponelo en cada sobre de tu prototipo y
    /// asigná solo los campos que tenga tu diseño (todos son opcionales).
    ///
    /// Dos usos:
    ///  - Sobre de la mesa: lo maneja ProposalBoardUI (Bind con un índice).
    ///  - Vista de detalle: marcá "followSelection" y muestra siempre la propuesta seleccionada.
    /// </summary>
    public class ProposalCardUI : MonoBehaviour
    {
        [Header("Textos (TextMeshPro)")]
        public TMP_Text titleText;
        public TMP_Text regionText;
        public TMP_Text categoryText;
        public TMP_Text descriptionText;
        public TMP_Text specialEventText;

        [Header("Imágenes")]
        public Image proposalImage;
        public Image regionIcon;
        [Tooltip("Opcional: se tiñe con el color de la región.")]
        public Graphic regionColorTarget;

        [Header("Estado")]
        [Tooltip("Botón del sobre para seleccionarlo (puede ser el propio sobre).")]
        public Button selectButton;
        public GameObject selectedHighlight;
        public GameObject approvedStamp;
        public GameObject rejectedStamp;

        [Header("Modo detalle")]
        [Tooltip("Si está activo, ignora Bind y muestra siempre la propuesta seleccionada.")]
        public bool followSelection = false;
        public ProposalManager proposalManager; // solo hace falta en modo detalle

        ProposalManager manager;
        int index = -1;

        void Awake()
        {
            if (selectButton != null) selectButton.onClick.AddListener(OnClicked);
        }

        void OnEnable()
        {
            if (!followSelection) return;
            if (proposalManager == null) proposalManager = FindAnyObjectByType<ProposalManager>();
            if (proposalManager == null) return;
            proposalManager.OnSelectionChanged += HandleSelectionForDetail;
            proposalManager.OnProposalDecided += HandleDecidedForDetail;
            HandleSelectionForDetail(proposalManager.SelectedIndex, proposalManager.SelectedProposal);
        }

        void OnDisable()
        {
            if (!followSelection || proposalManager == null) return;
            proposalManager.OnSelectionChanged -= HandleSelectionForDetail;
            proposalManager.OnProposalDecided -= HandleDecidedForDetail;
        }

        /// <summary>Lo llama ProposalBoardUI para asignar el sobre a una propuesta del día.</summary>
        public void Bind(ProposalManager owner, int proposalIndex)
        {
            manager = owner;
            index = proposalIndex;
            Show(owner.Proposals[proposalIndex]);
            RefreshState();
        }

        public void Show(ProposalData p)
        {
            gameObject.SetActive(p != null || followSelection);
            if (p == null) { Clear(); return; }

            SetText(titleText, p.DisplayName);
            SetText(regionText, p.RegionName);
            SetText(categoryText, p.CategoryName);
            SetText(descriptionText, p.description);
            SetText(specialEventText, p.specialEventText);

            SetImage(proposalImage, p.image);
            SetImage(regionIcon, p.region != null ? p.region.icon : null);
            if (regionColorTarget != null && p.region != null) regionColorTarget.color = p.region.color;
        }

        /// <summary>Actualiza resaltado y sellos según el ProposalManager.</summary>
        public void RefreshState()
        {
            var m = followSelection ? proposalManager : manager;
            int i = followSelection && m != null ? m.SelectedIndex : index;
            if (m == null) return;

            bool? decision = m.GetDecision(i);
            if (selectedHighlight != null) selectedHighlight.SetActive(!followSelection && m.SelectedIndex == i);
            if (approvedStamp != null) approvedStamp.SetActive(decision == true);
            if (rejectedStamp != null) rejectedStamp.SetActive(decision == false);
            if (selectButton != null) selectButton.interactable = true; // se puede releer una propuesta ya sellada
        }

        void OnClicked()
        {
            if (!followSelection && manager != null) manager.Select(index);
        }

        void HandleSelectionForDetail(int i, ProposalData p)
        {
            Show(p);
            RefreshState();
        }

        void HandleDecidedForDetail(int i, ProposalData p, bool approved) => RefreshState();

        void Clear()
        {
            SetText(titleText, ""); SetText(regionText, ""); SetText(categoryText, "");
            SetText(descriptionText, ""); SetText(specialEventText, "");
            SetImage(proposalImage, null); SetImage(regionIcon, null);
            if (approvedStamp != null) approvedStamp.SetActive(false);
            if (rejectedStamp != null) rejectedStamp.SetActive(false);
        }

        static void SetText(TMP_Text t, string value)
        {
            if (t != null) t.text = value ?? "";
        }

        static void SetImage(Image img, Sprite sprite)
        {
            if (img == null) return;
            img.sprite = sprite;
            img.enabled = sprite != null;
        }
    }
}
