using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ReturnToMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject creditPanel;

    [Header("Buttons")]
    public Button creditButton;
    public Button returnButton;
    public Button quitButton;

    void Start()
    {
        // Make sure panels start in correct state
        mainMenuPanel.SetActive(true);
        creditPanel.SetActive(false);

        // Hook up button events
        creditButton.onClick.AddListener(OpenCredits);
        returnButton.onClick.AddListener(CloseCredits);
    }

    private void OpenCredits()
    {
        mainMenuPanel.SetActive(false);
        creditPanel.SetActive(true);
    }

    private void CloseCredits()
    {
        creditPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
    
    // Call this from the button OnClick event
    public void GoToMenu()
    {
        // Replace "Menu" with your actual menu scene name
        SceneManager.LoadScene("CreateMenu");
    }

        private void QuitGame()
        {
            // Works in build
            Application.Quit();

            // Helps when testing inside Unity Editor
        #if UNITY_EDITOR
                Debug.Log("QuitGame() called — App would close in a build.");
        #endif
        }
}
