using UnityEngine;
using Alteruna;

public class SoupHeatController : MonoBehaviour, ICookingActionReceiver
{
    [Header("Temperature Settings")]
    public float minTemp = 0f;
    public float maxTemp = 100f;
    public float startingTemp = 50f;

    public float coolingSpeed = 5f;    // temp falls over time
    public float heatingAmount = 10f;  // amount added per action

    [Header("Green Zone")]
    public float greenStart = 40f;
    public float greenEnd = 70f;

    [Header("Progress Settings")]
    public float requiredTimeInGreen = 6f; // seconds
    private float greenTimer = 0f;

    [Header("UI")]
    public UnityEngine.UI.Slider tempSlider;
    public UnityEngine.UI.Slider progressSlider;

    private float temp;
    private bool isActive = false;

    private CookingPhaseManager phaseManager;
    private PlayerControl playerControl;

    void Start()
    {
        phaseManager = GetComponent<CookingPhaseManager>();
        playerControl = GetComponent<PlayerControl>();
    }

    public void StartMiniGame()
    {
        if (!phaseManager || !playerControl)
        {
            Debug.LogError("SoupHeatController missing references.");
            return;
        }

        temp = startingTemp;
        greenTimer = 0f;
        isActive = true;

        playerControl.canMove = false;
        phaseManager.CurrentMiniGame = this;

        if (tempSlider != null) tempSlider.gameObject.SetActive(true);
        if (progressSlider != null) progressSlider.gameObject.SetActive(true);

        Debug.Log("Soup mini-game started.");
    }

    void Update()
    {
        if (!isActive) return;

        // Temperature naturally cools
        temp -= coolingSpeed * Time.deltaTime;
        temp = Mathf.Clamp(temp, minTemp, maxTemp);

        // UI update
        if (tempSlider != null)
            tempSlider.value = temp;

        // Check if inside green zone
        if (temp >= greenStart && temp <= greenEnd)
        {
            greenTimer += Time.deltaTime;
        }

        if (progressSlider != null)
            progressSlider.value = greenTimer / requiredTimeInGreen;

        // Finish condition
        if (greenTimer >= requiredTimeInGreen)
        {
            CompleteMiniGame();
        }
    }

    // Called when warrior attacks OR wizard fireball hits
    public void OnPlayerPrimaryAction()
    {
        if (!isActive) return;

        temp += heatingAmount;
        temp = Mathf.Clamp(temp, minTemp, maxTemp);
    }

    private void CompleteMiniGame()
    {
        isActive = false;

        if (tempSlider != null) tempSlider.gameObject.SetActive(false);
        if (progressSlider != null) progressSlider.gameObject.SetActive(false);

        playerControl.canMove = true;
        phaseManager.CurrentMiniGame = null;

        phaseManager.AdvanceState(CookingState.CookIngredients);

        Debug.Log("Soup mini-game finished.");
    }
}
