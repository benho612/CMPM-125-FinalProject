using UnityEngine;

public class SoupStartArea : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        var phase = other.GetComponentInParent<CookingPhaseManager>();
        if (phase == null) return;

        if (phase.State == CookingState.CookSoup &&
            Input.GetKeyDown(KeyCode.E))
        {
            var soup = other.GetComponentInParent<SoupHeatController>();
            if (soup != null)
            {
                soup.StartMiniGame();
            }
        }
    }
}
