using UnityEngine;

public class StarTutorial : MonoBehaviour
{
    [Header("Tutorial")]
    public GameObject MenuPanel;
    public GameObject TutorialPanel;


    public void ShowTutorialPanel()
    {
        if (MenuPanel != null) MenuPanel.SetActive(false);
        if (TutorialPanel != null) TutorialPanel.SetActive(true);
    }
}
