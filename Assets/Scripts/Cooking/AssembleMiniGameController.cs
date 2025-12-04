using UnityEngine;
using UnityEngine.UI;

public class AssembleMiniGameController : MonoBehaviour, ICookingActionReceiver
{
    [Header("Timing UI")]
    private Slider timingSlider;
    [Tooltip("0 to 1, inclusive range where a press counts as success.")]
    public float successMin = 0.4f;
    public float successMax = 0.6f;

    [Header("Settings")]
    public float speed = 1.5f;  // how fast the marker bounces

    private bool active = false;
    private float t = 0f;
    private int direction = 1;

    private PlayerControl playerControl;
    private CookingPhaseManager phaseManager;
    private IngredientInventory inventory;

    private GameObject FindUI(string name)
    {
        var objs = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var o in objs)
        {
            if (o.name == name)
                return o;
        }
        return null;
    }


    void Start()
    {
        // Auto-find references if not assigned
        if (playerControl == null)
            playerControl = GetComponent<PlayerControl>();

        if (phaseManager == null)
            phaseManager = GetComponent<CookingPhaseManager>();

        if (inventory == null)
            inventory = GetComponent<IngredientInventory>();

        // Auto-find timing slider
        if (timingSlider == null)
        {
            var go = FindUI("AssembleTimingSlider");
            if (go != null)
                timingSlider = go.GetComponent<Slider>();
        }

        // Find ending panel in scene
        if (GameObject.Find("EndingPanel") != null)
        {
            // Just confirming existence; no assignment needed unless you want a variable
            Debug.Log("EndingPanel found in scene.");
        }


        if (timingSlider != null)
        {
            timingSlider.minValue = 0f;
            timingSlider.maxValue = 1f;
            timingSlider.gameObject.SetActive(false);
        }
    }

    public void StartMiniGame()
    {
        if (active)
            return;

        if (inventory == null || phaseManager == null)
        {
            Debug.LogWarning("AssembleMiniGameController missing inventory or phaseManager.");
            return;
        }

        // Must have all for assemble
        if (!inventory.HasAllForAssemble())
        {
            Debug.Log("Cannot start assemble mini-game: missing ingredients.");
            return;
        }

        active = true;
        t = 0f;
        direction = 1;

        if (timingSlider != null)
        {
            timingSlider.value = t;
            timingSlider.gameObject.SetActive(true);
        }

        if (playerControl != null)
            playerControl.canMove = false;

        phaseManager.CurrentMiniGame = this;

        Debug.Log("Assemble mini-game started.");
    }

    void Update()
    {
        if (!active)
            return;

        // Move marker back and forth between 0 and 1
        t += direction * speed * Time.deltaTime;

        if (t >= 1f)
        {
            t = 1f;
            direction = -1;
        }
        else if (t <= 0f)
        {
            t = 0f;
            direction = 1;
        }

        if (timingSlider != null)
            timingSlider.value = t;
    }

    // Called via ICookingActionReceiver from your PlayerControl when primary action is pressed
    public void OnPlayerPrimaryAction()
    {
        if (!active)
            return;

        // Check if current position is within success zone
        if (t >= successMin && t <= successMax)
        {
            Debug.Log("Assemble mini-game SUCCESS at t=" + t.ToString("F2"));
            CompleteMiniGame();
        }
        else
        {
            Debug.Log("Assemble mini-game MISS at t=" + t.ToString("F2"));
            // You can either let them retry or auto-complete. For now we just let them try again.
        }
    }

    private void CompleteMiniGame()
    {
        active = false;

        // Optionally consume ingredients
        // inventory.Add("Noodles", -1);
        // inventory.Add("Soup", -1);
        // inventory.Add("Chopped Vegetables", -1);

        // Add final ramen half
        inventory.Add("FinalRamenHalf", 1);
        Debug.Log("Assemble complete: +1 FinalRamenHalf");

        // Hide UI
        if (timingSlider != null)
            timingSlider.gameObject.SetActive(false);

        // Re-enable movement
        if (playerControl != null)
            playerControl.canMove = true;

        // Clear current mini-game
        if (phaseManager != null)
            phaseManager.CurrentMiniGame = null;

        // Update global final ramen count
        CookingPhaseManager.finalProductCount++;
        FindObjectOfType<FinalRamenCounterUI>()?.UpdateCounter(CookingPhaseManager.finalProductCount);

        Debug.Log($"Final ramen count = {CookingPhaseManager.finalProductCount}");

        // If both players finished, show end game or go to menu
        if (CookingPhaseManager.finalProductCount >= 2)
        {
            Debug.Log("Both final ramen halves complete. Game over!");

            // Option A: Show a simple end panel
            //var ending = GameObject.Find("EndingPanel");
            var ending = FindUI("EndingPanel");
            if (ending != null)
            {
                ending.SetActive(true);
            }
            // Option B: you can use SceneManager.LoadScene("MenuSceneName") here instead
        }
    }
}
