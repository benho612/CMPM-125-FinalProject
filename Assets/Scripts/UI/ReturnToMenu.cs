using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToMenu : MonoBehaviour
{
    // Call this from the button OnClick event
    public void GoToMenu()
    {
        // Replace "Menu" with your actual menu scene name
        SceneManager.LoadScene("CreateMenu");
    }
}
