using UnityEngine;
using Alteruna;

public class AssembleStartArea : MonoBehaviour
{
    [Header("UI Prompt")]
    public PlayerPickupUI pickupUI;

    private void OnTriggerStay(Collider other)
    {
        // Only local avatar
        var avatar = other.GetComponent<Alteruna.Avatar>();
        if (avatar == null || !avatar.IsMe)
            return;

        var phase = other.GetComponent<CookingPhaseManager>();
        if (phase == null || phase.State != CookingState.Assemble)
            return;

        var inv = other.GetComponent<IngredientInventory>();
        if (inv == null)
            return;

        // Check ingredients required for assemble
        if (!inv.HasAllForAssemble())
        {
            if (pickupUI != null)
                pickupUI.ShowCustom("Need: 1 Noodles, 1 Soup, 1 Chopped Vegetables");
            return;
        }

        if (pickupUI != null)
            pickupUI.ShowCustom("Press E to assemble ramen");

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (pickupUI != null)
                pickupUI.Hide();

            var assemble = other.GetComponent<AssembleMiniGameController>();
            if (assemble != null)
                assemble.StartMiniGame();
            else
                Debug.LogWarning("AssembleMiniGameController not found on player.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (pickupUI != null)
            pickupUI.Hide();
    }
}
