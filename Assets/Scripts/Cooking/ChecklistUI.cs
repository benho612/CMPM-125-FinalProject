using UnityEngine;
using TMPro;

public class ChecklistUI : MonoBehaviour
{
    public IngredientInventory inventory;
    public TextMeshProUGUI checklistText;
    public GameObject panel;

    private string[] displayOrder = new string[]
    {
        "Water",
        "Dough",
        "Noodles",
        "Horn",
        "Bones",
        "Garlic",
        "Condiments",
        "Soup",
        "Pork",
        "Egg",
        "Vegetable"
    };

    void Start()
    {
        // Hide panel initially (optional)
        if (panel != null)
            panel.SetActive(false);
    }

    void Update()
    {
        if (inventory == null || checklistText == null)
            return;

        string output = "Ingredients:\n\n";

        foreach (var item in displayOrder)
        {
            int have = inventory.GetCount(item);
            int need = inventory.GetRequired(item);

            output += $"{item}: {have}/{need}\n";
        }

        checklistText.text = output;
    }

    public void Show()
    {
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
