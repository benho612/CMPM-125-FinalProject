using UnityEngine;
using UnityEngine.UI;

public class IngredientChopController : MonoBehaviour, ICookingActionReceiver
{
    [Header("Settings")]
    public int hitsRequired = 10;

    [Header("References")]
    private Camera fpsCamera;
    private MouseLookLimited fpsLook;
    private UnityEngine.UI.Slider progressBar;
    private IngredientSpawner spawner;

    private int currentHits = 0;
    private bool active = false;

    private PlayerControl playerControl;
    private CookingPhaseManager phaseManager;
    private IngredientInventory inventory;
    private Vector3 originalPlayerPos;
    public float heightBoost = 1f;
    public bool IsActive => active;
    private Camera originalCamera;
    private Camera tpsCamera;
    //private Camera fpsCamera;

    private float autoProgress = 0f;
    public float autoFillDuration = 15f;   // time (seconds) to finish chopping




    public Vector3 GetAttackDirection()
    {
        return fpsCamera.transform.forward;
    }

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
    playerControl = GetComponent<PlayerControl>();
    phaseManager = GetComponent<CookingPhaseManager>();
    inventory = GetComponent<IngredientInventory>();

    // FIRST: Find FPS Camera by exact name anywhere in scene
    if (fpsCamera == null)
    {
        var go = FindUI("FirstPersonMiniCam");
        if (go != null)
        {
            fpsCamera = go.GetComponent<Camera>();
            fpsLook = go.GetComponent<MouseLookLimited>();
        }
    }

    // SECOND: Find TPS camera by exact name
    if (tpsCamera == null)
    {
        var go = FindUI("Camera");
        if (go != null)
            tpsCamera = go.GetComponent<Camera>();
    }

    // THIRD: NEVER overwrite cameras again (REMOVE the old GetComponentsInChildren loop!)
    // (Your previous code was destroying these references.)

    // FOURTH: progress bar
    if (progressBar == null)
    {
        var go = FindUI("IngredientProgressBar");
        if (go != null)
            progressBar = go.GetComponent<Slider>();
    }

    // Spawner auto-find
    if (spawner == null)
        spawner = FindObjectOfType<IngredientSpawner>(true);

    // Hide bar initially
    if (progressBar != null)
        progressBar.gameObject.SetActive(false);

    // Disable FPS camera by default
    if (fpsCamera != null)
        fpsCamera.enabled = false;
}

void Update()
    {
        if (!active)
            return;

        UpdateWhileChopping();
    }

    private void UpdateWhileChopping()
    {
        // Allow limited camera look
        if (fpsLook != null)
            fpsLook.EnableLook();

        // Still allow visual attacks
        if (Input.GetKeyDown(KeyCode.Mouse0))
            OnPlayerPrimaryAction();

        // --- AUTO PROGRESS FILL ---
        autoProgress += Time.deltaTime;
        float ratio = Mathf.Clamp01(autoProgress / autoFillDuration);

        if (progressBar != null)
            progressBar.value = ratio;

        // When complete
        if (ratio >= 1f)
        {
            CompleteMiniGame();
        }
    }

    public void StartMiniGame()
    {
        if (active) return;
        // Save original height and raise player to match table height
        originalPlayerPos = transform.position;
        transform.position = new Vector3(originalPlayerPos.x, originalPlayerPos.y + heightBoost, originalPlayerPos.z);


        active = true;
        currentHits = 0;
        fpsCamera.gameObject.SetActive(true);

        // Switch attack direction to FPS camera
        var warrior = GetComponent<WarriorCombatController>();
        if (warrior != null)
            warrior.OverrideAttackCamera(fpsCamera.transform);

        var wizard = GetComponent<WizardCombatController>();
        if (wizard != null)
            wizard.OverrideAttackCamera(fpsCamera.transform);


        // Lock movement
        if (playerControl != null)
            playerControl.canMove = false;

        // Save which camera was active so we can restore it later
        originalCamera = tpsCamera;

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
        spawner.Begin(fpsCamera.transform, this);

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

        fpsCamera.gameObject.SetActive(false);

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
