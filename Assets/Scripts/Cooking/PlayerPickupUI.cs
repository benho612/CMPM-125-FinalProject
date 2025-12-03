using UnityEngine;
using TMPro;

public class PlayerPickupUI : MonoBehaviour
{
    public TextMeshProUGUI promptText;  // assign in Inspector
    private bool showing = false;

    void Start()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);
    }

    public void Show(string itemName)
    {
        if (promptText == null) return;

        if (itemName == "to cut noodles")
        {
            promptText.text = "Press E " + itemName;
        }
        else
        {
            promptText.text = "Press E to pick up " + itemName;
        }
        
        if (!showing)
        {
            promptText.gameObject.SetActive(true);
            showing = true;
        }
    }

    public void Hide()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);

        showing = false;
    }
}
