using UnityEngine;
using Alteruna;

public enum CookingState
{
    None,
    PrepDough,
    CookSoup,
    CookIngredients,
    CookNoodles,
    Assemble,
    Done
}

public class CookingPhaseManager : MonoBehaviour
{
    private Alteruna.Avatar avatar;
    public ChecklistUI checklist;
    public static int finalProductCount = 0;




    public CookingState State { get; private set; } = CookingState.PrepDough;
    public ICookingActionReceiver CurrentMiniGame { get; set; }


    void Start()
    {
        avatar = GetComponent<Alteruna.Avatar>();


        // Only local players run cooking logic
        if (!avatar.IsMe)
        {
            enabled = false;
            Debug.Log("CookingPhaseManager disabled for non-local player.");

        }
        if (avatar.IsMe)
        {
            CookingPhaseManager.finalProductCount = 0;
            Debug.Log("CookingPhaseManager enabled for local player.");
            if (checklist != null){
                Debug.Log("Checklist UI assigned.");
                checklist.Show();
            }
            else{
                Debug.Log("Checklist UI not assigned in CookingPhaseManager.");
            }
        }
    }

    void Update()
    {
        if (!avatar.IsMe)
            return;

        var inv = GetComponent<IngredientInventory>();
        if (inv == null) return;

        // Unlock noodle cutting
        if (State == CookingState.PrepDough && inv.GetCount("Dough") >= 1)
        {
            Debug.Log("Noodle cutting unlocked.");
            // Nothing to do — player simply goes to the table and presses E
        }

        // Unlock soup after ingredients are gathered
        if (State == CookingState.PrepDough &&
            inv.HasItem("Water") &&
            inv.HasItem("Garlic") &&
            inv.HasItem("Bone") &&
            inv.HasItem("Condiments"))
        {
            //Debug.Log("Soup cooking unlocked.");
            AdvanceState(CookingState.CookSoup);
        }
    }

    public void AdvanceState(CookingState newState)
    {
        State = newState;
        Debug.Log("New cooking state: " + newState);
    }

    public void CheckForPhaseUnlocks()
    {
        var inv = GetComponent<IngredientInventory>();
        if (inv == null)
            return;

        bool hasNoodles = inv.GetCount("Noodles") >= 1;
        bool hasSoup = inv.GetCount("Soup") >= 1;

        if (hasNoodles && hasSoup)
        {
            Debug.Log("Both soup and noodles ready → unlocking CookIngredients.");
            AdvanceState(CookingState.CookIngredients);
        }
    }

}
