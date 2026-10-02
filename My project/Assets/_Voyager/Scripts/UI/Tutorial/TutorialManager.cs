using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("Tutorial")]
    public TMP_Text PrimerPanel;
    public TMP_Text SegundoPanel;
    public Button PlayButton;
    public Button ContinueButton;

    private bool isTutorialCompleted = false;
    void Awake()
    {
        if (PrimerPanel != null) PrimerPanel.gameObject.SetActive(true);
        if (SegundoPanel != null) SegundoPanel.gameObject.SetActive(false);
    }

    // Update is called once per frame
    public void ShowSecondPanel()
    {
        if (PrimerPanel != null) PrimerPanel.gameObject.SetActive(false);
        if (SegundoPanel != null) SegundoPanel.gameObject.SetActive(true);
        isTutorialCompleted = true;

        if (isTutorialCompleted)
        {
            ShowPlayButton();
        }
    }
    public void ShowPlayButton()
    {
        if (PlayButton != null) PlayButton.gameObject.SetActive(true);
        if(ContinueButton != null) ContinueButton.gameObject.SetActive(false);
    }
}
