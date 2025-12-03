// Assets/Bosses/_Shared/BossController.cs
using UnityEngine;
using Alteruna;
using System.Collections;

[RequireComponent(typeof(BossNetSync), typeof(BossHealth))]
public abstract class BossController : MonoBehaviour
{
    [Header("Core")]
    public Multiplayer multiplayer;        // auto-wired in Awake if null
    public float attackCadence = 2.5f;     // seconds between attacks
    public int baseSeed = 1337;            // per-run seed (set on scene load)
    [Range(0.5f, 5f)] public float difficultyScale = 1f;

    protected BossNetSync net;
    protected BossHealth health;
    protected System.Random rng;
    protected bool fightActive;
    bool _loopRunning;                     // re-entrancy guard

    // -------- Host check (robust + null-safe) --------
    protected bool IsHost
    {
        get
        {
            var mp = multiplayer;
            if (mp == null) return true; // offline fallback: treat as host

            Alteruna.User me = null;
            try { me = mp.Me; } catch { }
            if (me == null)
            {
                try { me = mp.GetUser(); } catch { }
            }

            if (me == null) return true; // offline / not initialized yet

            bool isHost = false;
            try { isHost = me.IsHost; } catch { }

            if (!isHost)
            {
                try { isHost = (me.Index == 0); } catch { }
            }

            return isHost;
        }
    }

    protected virtual void Awake()
    {
        net = GetComponent<BossNetSync>();
        health = GetComponent<BossHealth>();

        if (multiplayer == null)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            multiplayer = FindFirstObjectByType<Multiplayer>(FindObjectsInactive.Include);
#else
            multiplayer = FindObjectOfType<Multiplayer>();
#endif
            if (multiplayer == null)
                Debug.Log("[BossController] Multiplayer not found; running with offline host logic.");
        }

        rng = new System.Random(baseSeed);
    }

    /// <summary>Start the fight with a deterministic seed (host starts loop).</summary>
    public void StartFight(int seedOverride)
    {
        baseSeed = seedOverride;
        rng = new System.Random(baseSeed);
        fightActive = true;

        Debug.Log($"[BossController] StartFight(seed={seedOverride}) on {(IsHost ? "HOST" : "CLIENT")}.");

        if (IsHost && !_loopRunning)
            StartCoroutine(FightLoop());
    }

    public void StopFight()
    {
        fightActive = false;
        if (_loopRunning)
        {
            StopAllCoroutines();
            _loopRunning = false;
            Debug.Log("[BossController] Fight stopped.");
        }
    }

    IEnumerator FightLoop()
    {
        _loopRunning = true;

        int phase = ComputePhase();
        if (net != null) net.BroadcastPhase(phase);

        Debug.Log($"[BossController] Fight loop started. Cadence={attackCadence:0.00}s, Phase={phase}.");

        while (fightActive && IsAlive())
        {
            var (attackId, seed) = PickNextAttack();
            if (net != null) net.BroadcastStartAttack(attackId, seed);

            Debug.Log($"[BossController] Running attack {attackId} (seed {seed}).");
            yield return RunAttackHost(attackId, seed);

            float wait = Mathf.Max(0.05f, attackCadence);
            yield return new WaitForSeconds(wait);

            int newPhase = ComputePhase();
            if (newPhase != phase)
            {
                phase = newPhase;
                if (net != null) net.BroadcastPhase(phase);
                Debug.Log($"[BossController] Phase changed -> {phase}");
            }
        }

        fightActive = false;
        _loopRunning = false;

        Debug.Log("[BossController] Fight loop ended.");
        OnFightEnded();
    }

    protected bool IsAlive()
    {
        if (health == null) return true;
        return health.HP > 0f;
    }

    int ComputePhase()
    {
        return 0;
    }

    protected virtual void OnFightEnded() { }

    protected abstract (int attackId, int seed) PickNextAttack();
    protected abstract IEnumerator RunAttackHost(int attackId, int seed);

    protected float RandRange(float a, float b) => (float)(a + rng.NextDouble() * (b - a));
    protected int RandInt(int minInc, int maxExc) => rng.Next(minInc, maxExc);
}
