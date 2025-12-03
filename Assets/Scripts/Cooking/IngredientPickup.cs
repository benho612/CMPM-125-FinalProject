using UnityEngine;

public class IngredientPickup : MonoBehaviour
{
    [Header("Ingredient Settings")]
    public string ingredientName = "Garlic";
    public int amount = 1;

    private bool playerInRange = false;
    private PlayerPickupUI playerUI;
    private IngredientInventory inventory;

    private void OnTriggerEnter(Collider other)
    {
        // Only respond to players (root or child colliders)
        var inv = other.GetComponentInParent<IngredientInventory>();
        if (inv == null) return;

        inventory = inv;
        playerInRange = true;

        // Access that player's UI prompt
        playerUI = other.GetComponentInParent<PlayerPickupUI>();
        if (playerUI != null)
            playerUI.Show(ingredientName);
    }

    private void OnTriggerExit(Collider other)
    {
        var inv = other.GetComponentInParent<IngredientInventory>();
        if (inv == null) return;

        playerInRange = false;

        if (playerUI != null)
            playerUI.Hide();

        playerUI = null;
        inventory = null;
    }

    private void Update()
    {
        if (!playerInRange || inventory == null)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            inventory.Add(ingredientName, amount);

            if (playerUI != null)
                playerUI.Hide();

            //Destroy(gameObject);  // remove the ingredient from the table
        }
    }
}
