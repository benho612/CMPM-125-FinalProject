using UnityEngine;
using UnityEngine.SceneManagement;
using Alteruna;

public class LobbyFlowController : MonoBehaviour
{
    [Header("UI Roots")]
    public GameObject startMenuRoot;     // StartMenu panel
    public GameObject roomBrowserRoot;   // Room Browser prefab root
    public GameObject roomPanel;
    public MenuFade MenuFade;
    public CharacterSelectionNetwork selectionNetwork;

    [Header("Camera / Transition")]
    public Animator cameraAnimator;      // Main Camera Animator
    public float cameraAnimDuration = 2.0f; // seconds


    // Called by your main "Start" button
    public void OnMainStartClicked()
    {
        // Hide/disable the main start menu
        MenuFade.FadeOutMenu();
        cameraAnimator.SetTrigger("Play");

        if (startMenuRoot != null)
            startMenuRoot.SetActive(false);

        // Show the Alteruna UI
        if (roomBrowserRoot != null)
            roomBrowserRoot.SetActive(true);

        if(roomPanel != null)
            roomPanel.SetActive(true);


    }

}
