using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Pantalla final: muestra el EndingData elegido y, debajo, cómo reaccionaron los
    /// alienígenas a algunas propuestas aprobadas. "Reiniciar" empieza otra partida.
    /// </summary>
    public class EndingScreenUI : MonoBehaviour
    {
        public CampaignManager campaign;
        [Tooltip("Panel a mostrar. Conviene que sea un hijo de este objeto (así este script sigue activo).")]
        public GameObject panel;

        public TMP_Text titleText;
        public TMP_Text descriptionText;
        public Image endingImage;
        public Button restartButton;

        [Header("Reacciones de los alienígenas (opcional)")]
        public TMP_Text reactionsText;
        public string reactionsHeader = "Lo que más les llamó la atención:";

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
                endingImage.gameObject.SetActive(ending.image != null); // sin imagen, el texto sube
            }

            if (reactionsText != null)
            {
                var sb = new StringBuilder();
                if (campaign.FinalReactions.Count > 0)
                {
                    sb.AppendLine(reactionsHeader);
                    foreach (var p in campaign.FinalReactions)
                        sb.Append("• <b>").Append(p.DisplayName).Append(":</b> ").AppendLine(p.alienReaction);
                }
                reactionsText.text = sb.ToString();
            }
        }

        void Hide() { if (panel != null) panel.SetActive(false); }
    }
}
