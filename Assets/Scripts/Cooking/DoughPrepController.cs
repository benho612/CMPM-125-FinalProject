using UnityEngine;

public class DoughPrepController : MonoBehaviour, ICookingActionReceiver
{
    [Header("Settings")]
    public float mixDuration = 4f; // seconds required to finish dough

    private float timer = 0f;
    private bool isActive = false;

    private CookingPhaseManager phaseManager;
    private PlayerControl playerControl;
    private IngredientInventory inventory;


    [Header("UI")]
    public UnityEngine.UI.Slider progressBar; // drag in inspector later


    void Start()
    {
        phaseManager = GetComponent<CookingPhaseManager>();
        playerControl = GetComponent<PlayerControl>();
        inventory = GetComponent<IngredientInventory>();


        if (phaseManager == null)
            Debug.LogError("DoughPrepController needs CookingPhaseManager on same GameObject.");
    }


    public void StartMiniGame()
    {
        // Player MUST have at least 1 dough
        if (inventory != null && inventory.GetCount("Dough") < 1)
        {
            Debug.Log("You need Dough before cutting noodles.");
            return;
        }
        // lock movement
        if (playerControl != null)
            playerControl.canMove = false;

        // set self as current mini-game receiver
        phaseManager.CurrentMiniGame = this;

        timer = 0f;
        isActive = true;

        if (progressBar != null)
            progressBar.gameObject.SetActive(true);

        Debug.Log("Dough prep started.");
    }


    void Update()
    {
        if (!isActive)
            return;

        timer += Time.deltaTime;

        if (progressBar != null)
            progressBar.value = timer / mixDuration;

        if (timer >= mixDuration)
        {
            CompleteMiniGame();
        }
    }


    public void OnPlayerPrimaryAction()
    {
        // Dough mini-game does not use player actions.
        // This is here to satisfy ICookingActionReceiver.
    }


    private void CompleteMiniGame()
    {
        isActive = false;
        inventory.Add("Noodles", 1);
        inventory.Add("Dough", -1); // remove one dough

        // turn off UI
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);

        // unlock movement
        if (playerControl != null)
            playerControl.canMove = true;

        // clear mini-game link
        phaseManager.CurrentMiniGame = null;

        // advance to next phase
        phaseManager.AdvanceState(CookingState.CookSoup);

        Debug.Log("Noodle prep finished.");
    }
}
