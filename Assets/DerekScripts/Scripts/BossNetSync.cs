using UnityEngine;
using Alteruna;
using System.Collections;

public class BossNetSync : Synchronizable
{
    [Header("Auto References")]
    public BossController bossController;
    public EmberOvenWarden warden;

    public System.Action<int, int> OnStartAttack;
    public System.Action<int> OnPhaseChange;
    public System.Action<float> OnHealthSync;
    public System.Action<Vector3> OnImpactVfx;

    public float LastReceivedYaw { get; private set; } = 0f;

    Multiplayer _mp;
    bool _registered;

    private void Awake()
    {
        bossController = GetComponent<BossController>();
        warden = GetComponent<EmberOvenWarden>() ?? GetComponentInChildren<EmberOvenWarden>();

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        _mp = FindFirstObjectByType<Multiplayer>(FindObjectsInactive.Include);
#else
        _mp = FindObjectOfType<Multiplayer>();
#endif

        // IMPORTANT: do not self-register here for spawned objects.
        // Registration will be:
        // - auto-done by Alteruna for network-spawned prefabs
        // - explicitly done by the host in BossSceneBootstrap
        Debug.Log($"[BossNetSync] Awake on {(TryIsHost(_mp) ? "HOST" : "CLIENT")} (Object='{gameObject.name}')");
    }

    private void OnEnable()
    {
        // If we got enabled after Multiplayer was ready, ensure we are registered.
        if (!_registered)
            StartCoroutine(WaitAndRegister());
    }

    IEnumerator WaitAndRegister()
    {
        // Wait for Multiplayer to exist
        while (_mp == null)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            _mp = FindFirstObjectByType<Multiplayer>(FindObjectsInactive.Include);
#else
            _mp = FindObjectOfType<Multiplayer>();
#endif
            yield return null;
        }

        // Optional: wait for a room/session connection (some Alteruna SDKs expose states differently)
        // We defensively wait a few frames to let Synchronizable system initialize.
        for (int i = 0; i < 5; i++) yield return null;

        if (!_registered)
        {
            Register();
            _registered = true;

            // Force immediate commit so this Synchronizable obtains a network ID replicated across peers.
            Commit();

            bool isHost = false;
            try { isHost = _mp.Me.IsHost; } catch { try { isHost = (_mp.GetUser().Index == 0); } catch { } }
            Debug.Log($"[BossNetSync] Registered and committed on {(isHost ? "HOST" : "CLIENT")} (Object='{gameObject.name}')");
        }
    }

    static bool TryIsHost(Multiplayer mp)
    {
        if (mp == null) return false;
        try { return mp.Me.IsHost; } catch { }
        try { return mp.GetUser().Index == 0; } catch { }
        return false;
    }

    public override void AssembleData(Writer writer, byte LOD = 100) { }
    public override void DisassembleData(Reader reader, byte LOD = 100) { }

    // TEST RPC
    public void BroadcastTest(int v) => BroadcastRemoteMethod(nameof(RPC_Test), v);
    [SynchronizableMethod] public void RPC_Test(int v) => Debug.Log($"[BossNetSync] TEST RPC WORKS! Value = {v} → CLIENT SYNCED!");

    // ANIMATION SYNC
    public void BroadcastCrossFade(int hash, float blend) => BroadcastRemoteMethod(nameof(RPC_CrossFade), hash, blend);
    [SynchronizableMethod]
    public void RPC_CrossFade(int hash, float blend)
    {
        if (warden?._anim != null) warden._anim.CrossFade(hash, blend);
        Debug.Log($"[BossNetSync] RPC_CrossFade received: hash = {hash}, blend = {blend}");
    }

    public void BroadcastTrigger(string trigger) => BroadcastRemoteMethod(nameof(RPC_Trigger), trigger);
    [SynchronizableMethod]
    public void RPC_Trigger(string trigger)
    {
        if (warden?._anim != null) warden._anim.SetTrigger(trigger);
        Debug.Log($"[BossNetSync] RPC_Trigger received: {trigger}");
    }

    // YAW SYNC
    public void BroadcastYaw(float yaw)
    {
        LastReceivedYaw = yaw;
        BroadcastRemoteMethod(nameof(RPC_SetYaw), yaw);
    }
    [SynchronizableMethod]
    public void RPC_SetYaw(float yaw)
    {
        LastReceivedYaw = yaw;
        warden?.LocalSetYaw(yaw);
        Debug.Log($"[BossNetSync] RPC_SetYaw received: {yaw}");
    }

    // SPAWN SYNC
    public void BroadcastSpawnFireball(Vector3 start, Vector3 dest, float scale)
        => BroadcastRemoteMethod(nameof(RPC_SpawnFireball), start, dest, scale);
    [SynchronizableMethod]
    public void RPC_SpawnFireball(Vector3 s, Vector3 d, float sc)
    {
        warden?.LocalSpawnFireball(s, d, sc);
        Debug.Log($"[BossNetSync] RPC_SpawnFireball received: start = {s}, dest = {d}, scale = {sc}");
    }

    public void BroadcastSpawnVent(Vector3 pos, float scale)
        => BroadcastRemoteMethod(nameof(RPC_SpawnVent), pos, scale);
    [SynchronizableMethod]
    public void RPC_SpawnVent(Vector3 p, float sc)
    {
        warden?.LocalSpawnVent(p, sc);
        Debug.Log($"[BossNetSync] RPC_SpawnVent received: pos = {p}, scale = {sc}");
    }

    // OTHER RPCs
    public void BroadcastStartAttack(int id, int seed) => BroadcastRemoteMethod(nameof(RPC_StartAttack), id, seed);
    [SynchronizableMethod] void RPC_StartAttack(int id, int seed) => OnStartAttack?.Invoke(id, seed);

    public void BroadcastPhase(int p) => BroadcastRemoteMethod(nameof(RPC_Phase), p);
    [SynchronizableMethod] void RPC_Phase(int p) => OnPhaseChange?.Invoke(p);

    public void BroadcastHealth(float n) => BroadcastRemoteMethod(nameof(RPC_Health), n);
    [SynchronizableMethod] void RPC_Health(float n) => OnHealthSync?.Invoke(n);

    public void BroadcastImpact(Vector3 p) => BroadcastRemoteMethod(nameof(RPC_Impact), p);
    [SynchronizableMethod] void RPC_Impact(Vector3 p) => OnImpactVfx?.Invoke(p);
}