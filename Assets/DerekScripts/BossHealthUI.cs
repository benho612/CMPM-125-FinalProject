using UnityEngine;
using UnityEngine.UI;

public class BossHealthUI : MonoBehaviour
{
    [SerializeField] private BossHealth boss;
    [SerializeField] private Slider slider;

    private bool _subscribed;

    void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>();

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
        }
    }

    void OnEnable()
    {
        // Start polling until BossHealth is found and subscribed
        if (!_subscribed)
            StartCoroutine(FindAndSubscribeBossRoutine());
    }

    void OnDisable()
    {
        if (_subscribed && boss != null)
        {
            boss.OnHealthChanged -= HandleHealthChanged;
            boss.OnDeath -= HandleDeath;
            _subscribed = false;
        }
    }

    System.Collections.IEnumerator FindAndSubscribeBossRoutine()
    {
        // Try every 0.25s until boss is found
        var wait = new WaitForSeconds(0.25f);
        while (boss == null)
        {
            boss = FindObjectOfType<BossHealth>();
            if (boss == null)
            {
                Debug.Log("[BossHealthUI] Waiting for BossHealth...");
                yield return wait;
                continue;
            }
        }

        boss.OnHealthChanged += HandleHealthChanged;
        boss.OnDeath += HandleDeath;
        _subscribed = true;
        Debug.Log($"[BossHealthUI] Subscribed to BossHealth: {boss.name}");

        // Initialize slider to current percentage
        if (slider != null)
        {
            float pct = Mathf.Clamp01(boss.HP / 1000f);
            slider.value = pct;
            Debug.Log($"[BossHealthUI] Init slider pct={pct:P0}");
        }
    }

    void HandleHealthChanged(float current, float max)
    {
        if (slider == null) return;
        float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        slider.value = pct;
        Debug.Log($"[BossHealthUI] HealthChanged: {current:0}/{max:0} ({pct:P0})");
    }

    void HandleDeath()
    {
        if (slider != null)
            slider.value = 0f;
        Debug.Log("[BossHealthUI] Boss died. Slider set to 0%.");
    }
}