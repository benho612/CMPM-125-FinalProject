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
            Debug.Log("CookingPhaseManager enabled for local player.");
            if (checklist != null)
                Debug.Log("Checklist UI assigned.");
                checklist.Show();
        }
    }

    public void AdvanceState(CookingState newState)
    {
        State = newState;
        Debug.Log("New cooking state: " + newState);
    }
}
