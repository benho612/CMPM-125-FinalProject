using UnityEngine;
using TMPro;

public class PlayerPickupUI : MonoBehaviour
{
    private TextMeshProUGUI promptText;  // assign in Inspector
    private bool showing = false;

    private GameObject FindUI(string name)
    {
        var objs = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var o in objs)
        {
            if (o.name == name)
                return o;
        }
        return null;
    }
    
    void Start()
    {
        if (promptText == null)
        {
            GameObject ui = FindUI("PickupPrompt");
            if (ui != null)
                promptText = ui.GetComponent<TextMeshProUGUI>();
        }
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

    public void ShowCustom(string text)
    {
        if (promptText == null) return;

        promptText.text = text;
        promptText.gameObject.SetActive(true);
    }

}
