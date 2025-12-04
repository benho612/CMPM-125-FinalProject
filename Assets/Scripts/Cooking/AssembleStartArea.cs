using UnityEngine;
using Alteruna;

public class AssembleStartArea : MonoBehaviour
{
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

        var ui = other.GetComponentInParent<PlayerPickupUI>();
        if (ui == null)
            return;

        // Check ingredients required for assemble
        if (!inv.HasAllForAssemble())
        {
            if (ui != null)
                ui.ShowCustom("Need: 1 Noodles, 1 Soup, 1 Chopped Vegetables");
            return;
        }

        if (ui != null)
            ui.ShowCustom("Press E to assemble ramen");

        // Begin mini-game on E
        if (Input.GetKeyDown(KeyCode.E))
        {
            var assemble = other.GetComponentInParent<AssembleMiniGameController>();
            if (assemble != null)
            {
                assemble.StartMiniGame();
                ui.Hide();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var ui = other.GetComponentInParent<PlayerPickupUI>();
        if (ui != null)
            ui.Hide();
    }
}



/*

Cooking Phase Manager
Dough Prep controller
Soup heat controller
ingredient inventory
player pickup UI
ingredient chop controller
assemble mini game controller
FirstPersonMiniCam
->MouseLookLimited

*/