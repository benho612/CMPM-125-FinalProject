using UnityEngine;

public class IngredientSliceable : MonoBehaviour
{
    private IngredientChopController owner; 
    private bool chopped = false;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Called by spawner when spawning (we’ll assign owner later)
    public void SetOwner(IngredientChopController controller)
    {
        owner = controller;
    }

    public void Chop()
    {
        if (chopped) return;  
        chopped = true;

        // Notify controller
        if (owner != null)
            owner.RegisterHit();

        // Add knockback force to fling ingredient away
        Vector3 randomDir = Random.onUnitSphere;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(randomDir * 12f, ForceMode.Impulse);
        }

        // Destroy shortly after knockback
        Destroy(gameObject, 0.5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Wizard’s fireball can call Chop()
        if (other.CompareTag("Fireball"))
        {
            Chop();
        }
    }
}
