using UnityEngine;

public class IngredientChopController : MonoBehaviour, ICookingActionReceiver
{
    [Header("Settings")]
    public int hitsRequired = 10;

    [Header("References")]
    public Camera fpsCamera;
    public MouseLookLimited fpsLook;
    public UnityEngine.UI.Slider progressBar;
    public IngredientSpawner spawner;

    private int currentHits = 0;
    private bool active = false;

    private PlayerControl playerControl;
    private CookingPhaseManager phaseManager;
    private IngredientInventory inventory;
    private Vector3 originalPlayerPos;
    public float heightBoost = 1f;
    public bool IsActive => active;
    private Camera originalCamera;


    public Vector3 GetAttackDirection()
    {
        return fpsCamera.transform.forward;
    }



    void Start()
    {
        playerControl = GetComponent<PlayerControl>();
        phaseManager = GetComponent<CookingPhaseManager>();
        inventory = GetComponent<IngredientInventory>();

        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        if (fpsCamera != null)
            fpsCamera.enabled = false;
    }

    public void StartMiniGame()
    {
        if (active) return;
        // Save original height and raise player to match table height
        originalPlayerPos = transform.position;
        transform.position = new Vector3(originalPlayerPos.x, originalPlayerPos.y + heightBoost, originalPlayerPos.z);


        active = true;
        currentHits = 0;

        // Lock movement
        if (playerControl != null)
            playerControl.canMove = false;

        // Save which camera was active so we can restore it later
        originalCamera = Camera.main;

        // Disable it
        if (originalCamera != null)
            originalCamera.enabled = false;

        // Enable FPS camera
        fpsCamera.enabled = true;
        fpsLook.EnableLook();


        // Initialize UI
        progressBar.value = 0;
        progressBar.gameObject.SetActive(true);

        // Mark as current mini-game
        phaseManager.CurrentMiniGame = this;

        // Start spawning
        spawner.Begin(fpsCamera.transform);
    }

    public void OnPlayerPrimaryAction()
    {
        if (!active) return;

        // Raycast from camera center
        Ray ray = new Ray(fpsCamera.transform.position, fpsCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 50f))
        {
            var slice = hit.collider.GetComponent<IngredientSliceable>();
            if (slice != null)
            {
                slice.Chop();
            }
        }
    }

    public void RegisterHit()
    {
        if (!active) return;
        Debug.Log("REGISTER HIT CALLED");

        currentHits++;

        progressBar.value = (float)currentHits / hitsRequired;

        if (currentHits >= hitsRequired)
        {
            CompleteMiniGame();
        }
    }

    private void CompleteMiniGame()
    {
        active = false;

        // Restore player height
        transform.position = originalPlayerPos;

        // Stop spawning
        if (spawner != null)
        {
            spawner.StopSpawning();
            spawner.CleanupIngredients();
        }

        // Hide UI
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        // Disable FPS camera
        if (fpsCamera != null)
            fpsCamera.enabled = false;

        if (fpsLook != null)
            fpsLook.DisableLook();

        // Restore original camera
        if (originalCamera != null)
            originalCamera.enabled = true;

        // Enable movement again
        if (playerControl != null)
            playerControl.canMove = true;

        // GIVE PLAYER CHOPPED VEGETABLES FOR ASSEMBLE PHASE
        if (inventory != null)
        {
            inventory.Add("Chopped Vegetables", 1);
            Debug.Log("Chopping complete: +1 Chopped Vegetables");
        }

        // Clear mini-game registration
        if (phaseManager != null)
        {
            phaseManager.CurrentMiniGame = null;

            // Move to Assemble phase
            phaseManager.AdvanceState(CookingState.Assemble);
            Debug.Log("Cooking state moved to ASSEMBLE after chopping.");
        }
    }

}
