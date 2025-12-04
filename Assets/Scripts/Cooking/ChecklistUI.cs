using UnityEngine;
using TMPro;

public class ChecklistUI : MonoBehaviour
{
    public IngredientInventory inventory;
    public TextMeshProUGUI checklistText;
    public GameObject panel;

    private string[] displayOrder = new string[]
    {
        "Horn",
        "Water",
        "Dough",
        "Bone",
        "Garlic",
        "Condiments",
        "Pork",
        "Egg",
        "Vegetable",
        "Noodles",
        "Soup",
        "Chopped Vegetables"
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
        if (panel != null){
            Debug.Log("Showing checklist UI.");
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }
}
