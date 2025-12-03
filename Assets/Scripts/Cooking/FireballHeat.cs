using UnityEngine;

public class FireballHeat : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var soup = other.GetComponentInParent<SoupHeatController>();
        if (soup != null)
        {
            soup.OnPlayerPrimaryAction();
        }
    }
}
