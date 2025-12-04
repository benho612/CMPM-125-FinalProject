// using UnityEngine;
// using TMPro;

// public class ChecklistUI : MonoBehaviour
// {
//     private IngredientInventory inventory;
//     public TextMeshProUGUI checklistText;
//     public GameObject panel;

//     private string[] displayOrder = new string[]
//     {
//         "Horn",
//         "Water",
//         "Dough",
//         "Bone",
//         "Garlic",
//         "Condiments",
//         "Pork",
//         "Egg",
//         "Vegetable",
//         "Noodles",
//         "Soup",
//         "Chopped Vegetables"
//     };

//     void Start()
//     {
//         // Try auto-locating the player's inventory if none assigned
//         if (inventory == null)
//         {
//             // Find all avatars in the scene
//             var avatars = FindObjectsOfType<Alteruna.Avatar>();

//             foreach (var av in avatars)
//             {
//                 // Only pick the local player
//                 if (av.IsMe)
//                 {
//                     inventory = av.GetComponent<IngredientInventory>();
//                     if (inventory != null)
//                     {
//                         Debug.Log("ChecklistUI: Automatically found local player's IngredientInventory.");
//                     }
//                     break;
//                 }
//             }
//         }

//         // Hide panel initially (optional)
//         if (panel != null)
//             panel.SetActive(false);
//     }


//     void Update()
//     {
//         if (inventory == null || checklistText == null)
//             return;

//         string output = "Ingredients:\n\n";

//         foreach (var item in displayOrder)
//         {
//             int have = inventory.GetCount(item);
//             int need = inventory.GetRequired(item);

//             output += $"{item}: {have}/{need}\n";
//         }

//         checklistText.text = output;
//     }

//     public void Show()
//     {
//         if (panel != null){
//             Debug.Log("Showing checklist UI.");
//             panel.SetActive(true);
//         }
//     }

//     public void Hide()
//     {
//         if (panel != null) panel.SetActive(false);
//     }
// }


using UnityEngine;
using UnityEngine.UI;
using Alteruna;

public class ChecklistUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public TMPro.TextMeshProUGUI checklistText;

    // NOW PRIVATE — no need to assign manually
    private IngredientInventory inventory;

    // List of items to display (same as before)
    public string[] displayOrder = new string[]
    {
        "Water",
        "Dough",
        "Noodles",
        "Horn",
        "Bone",
        "Garlic",
        "Condiments",
        "Soup",
        "Pork",
        "Egg",
        "Vegetable",
        "Chopped Vegetables",
        "FinalRamenHalf"
    };

    void Start()
    {
        TryAutoFindInventory();

        if (panel != null)
            panel.SetActive(false);
    }

    void Update()
    {
        // If inventory not yet found (avatar may spawn late), keep searching
        if (inventory == null)
        {
            TryAutoFindInventory();
            return;
        }

        if (checklistText == null)
            return;

        // Build UI text
        string output = "Ingredients:\n\n";
        foreach (var item in displayOrder)
        {
            int have = inventory.GetCount(item);
            int need = inventory.GetRequired(item);
            output += $"{item}: {have}/{need}\n";
        }

        checklistText.text = output;
    }

    private void TryAutoFindInventory()
    {
        var avatars = FindObjectsOfType<Alteruna.Avatar>();
        foreach (var av in avatars)
        {
            if (av.IsMe)
            {
                inventory = av.GetComponent<IngredientInventory>();
                if (inventory != null)
                {
                    Debug.Log("ChecklistUI: Found local player's IngredientInventory.");
                }
                return;
            }
        }
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
