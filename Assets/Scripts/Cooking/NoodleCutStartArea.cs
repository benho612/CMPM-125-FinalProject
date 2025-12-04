using UnityEngine;

public class NoodleCutStartArea : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        var phase = other.GetComponentInParent<CookingPhaseManager>();
        if (phase == null) return;

        var inventory = other.GetComponentInParent<IngredientInventory>();
        if (inventory == null) return;

        var ui = other.GetComponentInParent<PlayerPickupUI>();

        bool hasDough = inventory.GetCount("Dough") >= 1;

        // --------------------------------------------------
        // Show / Hide prompt
        // --------------------------------------------------
        if (hasDough)
        {
            // show prompt: same UI as ingredient pickup
            if (ui != null)
                ui.Show("to cut noodles");
        }
        else
        {
            // hide prompt if present
            if (ui != null)
                ui.Hide();
        }

        // --------------------------------------------------
        // Press E to start cutting noodles
        // --------------------------------------------------
        if (hasDough &&
            Input.GetKeyDown(KeyCode.E))
        {
            var cutter = other.GetComponentInParent<DoughPrepController>();
            if (cutter != null)
            {
                cutter.StartMiniGame();

                // hide prompt after starting
                if (ui != null)
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
