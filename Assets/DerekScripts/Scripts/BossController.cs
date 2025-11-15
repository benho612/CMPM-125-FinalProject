// Assets/Bosses/_Shared/BossController.cs
using UnityEngine;
using Alteruna;
using System.Collections;

[RequireComponent(typeof(BossNetSync), typeof(BossHealth))]
public abstract class BossController : MonoBehaviour
{
    [Header("Core")]
    public Multiplayer multiplayer; // drag your scene Multiplayer here
    public float attackCadence = 2.5f; // seconds between attacks
    public int baseSeed = 1337;      // per-run seed (set on scene load)
    [Range(0.5f, 5f)] public float difficultyScale = 1f; // multiplies speeds/damages
    [SerializeField] bool playOfflineIfNoRoom = true;
    protected BossNetSync net;
    protected BossHealth health;
    protected System.Random rng;
    protected bool fightActive;
    protected bool IsHost =>
        (playOfflineIfNoRoom && (multiplayer == null || !multiplayer.InRoom || multiplayer.Me == null))
        || (multiplayer != null && multiplayer.InRoom && multiplayer.Me != null && multiplayer.Me.IsHost);

    protected virtual void Awake()
    {
        net = GetComponent<BossNetSync>();
        health = GetComponent<BossHealth>();
        rng = new System.Random(baseSeed);
    }

    // Called by intro director after 5s pan
    public void StartFight(int seedOverride)
    {
        baseSeed = seedOverride;
        rng = new System.Random(baseSeed);
        fightActive = true;
        if (IsHost) StartCoroutine(FightLoop());
    }

    IEnumerator FightLoop()
    {
        int phase = 0;
        net.BroadcastPhase(phase);
        while (fightActive && health.HP > 0f)
        {
            // Pick an attack, seed it, broadcast, then run it (host).
            var (attackId, seed) = PickNextAttack();
            net.BroadcastStartAttack(attackId, seed);
            yield return RunAttackHost(attackId, seed);
            yield return new WaitForSeconds(attackCadence);
            // Optional phase changes by HP gates
            phase = ComputePhase();
            net.BroadcastPhase(phase);
        }
        fightActive = false;
        OnFightEnded();
    }

    int ComputePhase()
    {
        float n = health.HP; // just keep 0..maxHP
        // Example simple phases by %HP (override if needed)
        float pct = health.HP <= 0 ? 0 : health.HP / Mathf.Max(0.0001f, health.HP);
        return pct < 0.34f ? 2 : pct < 0.67f ? 1 : 0;
    }

    protected virtual void OnFightEnded() { }

    // Each boss implements these:
    protected abstract (int attackId, int seed) PickNextAttack();
    protected abstract IEnumerator RunAttackHost(int attackId, int seed);

    // Utility: deterministic spread angles
    protected float RandRange(float a, float b) => (float)(a + rng.NextDouble() * (b - a));
    protected int RandInt(int minInc, int maxExc) => rng.Next(minInc, maxExc);
}
