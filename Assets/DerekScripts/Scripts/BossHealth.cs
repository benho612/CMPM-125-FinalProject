using UnityEngine;
using System;

public class BossHealth : MonoBehaviour
{
    [SerializeField] float maxHp = 1000f;
    [SerializeField] BossNetSync net;
    [SerializeField] Animator bossAnimator;

    [Header("Death sequence")]
    [Tooltip("Seconds to wait after death before returning to Kitchen. Host will RPC load for all players.")]
    [SerializeField] float deathDelaySeconds = 10f;
    [Tooltip("Scene name to load after the boss dies.")]
    [SerializeField] string returnSceneName = "KitchenScene";

    public float HP { get; private set; }
    public float MaxHP => maxHp;

    public Action OnDeath;
    public Action<float, float> OnHealthChanged; // (current, max)

    void Awake()
    {
        HP = maxHp;
        if (!net) net = GetComponent<BossNetSync>();
        if (!bossAnimator) bossAnimator = GetComponent<Animator>();

        OnHealthChanged?.Invoke(HP, maxHp);
    }

    // Host-side local damage only (clients should call BossNetSync.RequestDamage)
    public void Damage(float amount)
    {
        if (HP <= 0f) return;

        float old = HP;
        HP = Mathf.Max(0f, HP - Mathf.Max(0f, amount));

        if (!Mathf.Approximately(old, HP))
            OnHealthChanged?.Invoke(HP, maxHp);

        if (HP <= 0f)
        {
            if (bossAnimator) bossAnimator.CrossFade("Die", 0.1f);
            OnDeath?.Invoke();

            // Host will RPC scene load to all players after delay
            net?.BroadcastLoadSceneAfterDelay(returnSceneName, deathDelaySeconds);
        }
    }

    // Applied on all peers from host replication
    public void SetFromNetwork(float current, float max)
    {
        float old = HP;
        maxHp = max;
        HP = Mathf.Clamp(current, 0f, maxHp);

        OnHealthChanged?.Invoke(HP, maxHp);

        if (old > 0f && HP <= 0f)
        {
            if (bossAnimator) bossAnimator.CrossFade("Die", 0.1f);
            OnDeath?.Invoke();

            // Host will handle scene change broadcast; clients do nothing here.
        }
    }
}