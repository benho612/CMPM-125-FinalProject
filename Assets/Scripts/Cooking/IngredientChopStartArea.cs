using UnityEngine;

public class IngredientChopStartArea : MonoBehaviour
{
    public PlayerPickupUI pickupUI;

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

        pickupUI.ShowCustom("Press E to cook ingredients");

        if (Input.GetKeyDown(KeyCode.E))
        {
            pickupUI.Hide();

            var chop = other.GetComponent<IngredientChopController>();
            if (chop != null)
                chop.StartMiniGame();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (pickupUI != null)
            pickupUI.Hide();
    }
}
