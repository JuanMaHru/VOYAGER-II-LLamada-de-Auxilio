using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Voyager.UI
{
    /// <summary>
    /// Muestra, de a uno, los textos narrativos que surgen durante el día
    /// (intro del día, consecuencias ShowNarrative de propuestas, umbrales críticos).
    /// Los del cierre del día los muestra DayResultUI.
    /// </summary>
    public class NarrativePopupUI : MonoBehaviour
    {
        public CampaignManager campaign;
        [Tooltip("Panel a mostrar. Conviene que sea un hijo de este objeto.")]
        public GameObject panel;
        public TMP_Text bodyText;
        public Button closeButton;

        readonly Queue<string> queue = new Queue<string>();

        void Awake()
        {
            if (campaign == null) campaign = FindAnyObjectByType<CampaignManager>();
            if (closeButton != null) closeButton.onClick.AddListener(ShowNext);
            campaign.OnNarrative += Enqueue;
            campaign.OnCampaignStarted += ClearAll;
            if (panel != null) panel.SetActive(false);
        }

        void OnDestroy()
        {
            if (campaign == null) return;
            campaign.OnNarrative -= Enqueue;
            campaign.OnCampaignStarted -= ClearAll;
        }

        void Enqueue(string text)
        {
            queue.Enqueue(text);
            if (panel != null && !panel.activeSelf) ShowNext();
        }

        void ShowNext()
        {
            if (queue.Count == 0) { if (panel != null) panel.SetActive(false); return; }
            if (panel != null) panel.SetActive(true);
            if (bodyText != null) bodyText.text = queue.Dequeue();
        }

        void ClearAll()
        {
            queue.Clear();
            if (panel != null) panel.SetActive(false);
        }
    }
}
