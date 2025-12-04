using UnityEngine;

public class SoupStartArea : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        // Get player components
        var phase = other.GetComponentInParent<CookingPhaseManager>();
        if (phase == null) return;

        var inv = other.GetComponentInParent<IngredientInventory>();
        if (inv == null) return;

        var ui = other.GetComponentInParent<PlayerPickupUI>();

        // Check if player has required soup ingredients
        bool hasSoupIngredients =
            inv.GetCount("Water") >= 2 &&
            inv.GetCount("Bone") >= 1 &&
            inv.GetCount("Pork") >= 1 &&
            inv.GetCount("Garlic") >= 1 &&
            inv.GetCount("Condiments") >= 1;

        // Show or hide the prompt
        if (hasSoupIngredients)
        {
            if (ui != null)
            {
                ui.ShowCustom("Press E to cook soup");
            }
            else
            {
                Debug.Log("PlayerPickupUI not found on player.");
            }
                

            // Start mini-game on E
            if (Input.GetKeyDown(KeyCode.E))
            {
                var soup = other.GetComponentInParent<SoupHeatController>();
                if (soup != null)
                {
                    soup.StartMiniGame();
                    if (ui != null)
                        ui.Hide();
                }
            }
        }
        else
        {
            if (ui != null)
                ui.Hide();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var ui = other.GetComponentInParent<PlayerPickupUI>();
        if (ui != null)
            ui.Hide();
    }
}
