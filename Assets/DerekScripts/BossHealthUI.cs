using UnityEngine;
using UnityEngine.UI;

public class BossHealthUI : MonoBehaviour
{
    [SerializeField] private BossHealth boss;
    [SerializeField] private Slider slider;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float fadeSpeed = 6f;

    void Awake()
    {
        // Auto-find the active BossHealth if not assigned
        if (boss == null)
            boss = FindObjectOfType<BossHealth>();

        // Initialize slider if boss is already present
        if (boss != null && slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = boss.HP; // will be overwritten on first OnHealthChanged
            slider.value = boss.HP;
        }

        if (!group) group = GetComponent<CanvasGroup>();
        if (group) group.alpha = 0f;
    }

    void OnEnable()
    {
        if (boss != null)
        {
            boss.OnHealthChanged += HandleHealthChanged;
            boss.OnDeath += HandleDeath;
        }
    }

    void OnDisable()
    {
        if (boss != null)
        {
            boss.OnHealthChanged -= HandleHealthChanged;
            boss.OnDeath -= HandleDeath;
        }
    }

    void HandleHealthChanged(float current, float max)
    {
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = max;
            slider.value = current;
        }

        // Fade in when boss takes damage / is active
        if (group) group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.deltaTime * fadeSpeed);
    }

    void HandleDeath()
    {
        // No fade-out; leave UI as-is or let external logic hide it if desired.
        // Optionally set to zero to reflect death state:
        if (slider != null)
            slider.value = 0f;
    }
}