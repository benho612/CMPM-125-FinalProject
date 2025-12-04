using UnityEngine;

public class IngredientChopStartArea : MonoBehaviour
{

    private void OnTriggerStay(Collider other)
    {
        var avatar = other.GetComponent<Alteruna.Avatar>();
        if (avatar == null || !avatar.IsMe)
            return;

        var phase = other.GetComponent<CookingPhaseManager>();
        if (phase == null)
            return;


        // Only allow during correct phase
        if (phase.State != CookingState.CookIngredients)
            return;

        var ui = other.GetComponentInParent<PlayerPickupUI>();
        if (ui == null)
            return;

        ui.ShowCustom("Press E to cook ingredients");
        // Start chopping mini-game
        if (Input.GetKeyDown(KeyCode.E))
        {
            var chop = other.GetComponentInParent<IngredientChopController>();
            if (chop != null)
            {
                chop.StartMiniGame();
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
