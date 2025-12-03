// Assets/Bosses/_Shared/BossHealth.cs
using UnityEngine;
using System;

public class BossHealth : MonoBehaviour
{
    [SerializeField] float maxHp = 1000f;
    [SerializeField] BossNetSync net;
    [SerializeField] Animator bossAnimator;

    public float HP { get; private set; }
    public Action OnDeath;
    public Action<float, float> OnHealthChanged; // (current, max)

    void Awake()
    {
        HP = maxHp;
        if (!net) net = GetComponent<BossNetSync>();
        if (!bossAnimator) bossAnimator = GetComponent<Animator>();
        // Optionally broadcast initial health here if you add networking later
        OnHealthChanged?.Invoke(HP, maxHp);
    }

    public void Damage(float amount)
    {
        if (HP <= 0f) return;

        float oldHP = HP;
        HP = Mathf.Max(0f, HP - amount);

        // Notify UI listeners
        if (!Mathf.Approximately(HP, oldHP))
            OnHealthChanged?.Invoke(HP, maxHp);

        //net?.BroadcastHealth(HP / maxHp);

        if (HP <= 0f)
        {
            // Play death animation (if animator exists)
            if (bossAnimator)
            {
                // Assumes a "Die" state or trigger is present; adjust to your controller
                bossAnimator.CrossFade("Die", 0.1f);
                // Alternatively: bossAnimator.SetTrigger("Die");
            }

            OnDeath?.Invoke();
        }
    }

    // Optional helpers
    //public void SetMax(float newMax) { maxHp = newMax; HP = maxHp; net?.BroadcastHealth(1f); }
}