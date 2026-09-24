using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>Pantalla final: muestra el EndingData elegido. "Reiniciar" empieza otra partida.</summary>
    public class EndingScreenUI : MonoBehaviour
    {
        public CampaignManager campaign;
        [Tooltip("Panel a mostrar. Conviene que sea un hijo de este objeto (así este script sigue activo).")]
        public GameObject panel;

        public TMP_Text titleText;
        public TMP_Text descriptionText;
        public Image endingImage;
        public Button restartButton;

        void Awake()
        {
            if (campaign == null) campaign = FindAnyObjectByType<CampaignManager>();
            if (restartButton != null) restartButton.onClick.AddListener(() => campaign.StartNewCampaign());
            campaign.OnCampaignEnded += Show;
            campaign.OnCampaignStarted += Hide;
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (campaign == null) return;
            campaign.OnCampaignEnded -= Show;
            campaign.OnCampaignStarted -= Hide;
        }

        void Show(EndingData ending)
        {
            if (panel != null) panel.SetActive(true);
            if (ending == null) return;
            if (titleText != null) titleText.text = ending.DisplayName;
            if (descriptionText != null) descriptionText.text = ending.description;
            if (endingImage != null)
            {
                endingImage.sprite = ending.image;
                endingImage.enabled = ending.image != null;
            }
        }

        void Hide() { if (panel != null) panel.SetActive(false); }
    }
}
