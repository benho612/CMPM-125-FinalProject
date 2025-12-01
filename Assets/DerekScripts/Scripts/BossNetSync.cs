using System;
using UnityEngine;
using Alteruna;
using Alteruna.Trinity; // Reader/Writer

public class BossNetSync : Synchronizable
{
    [Header("Hooked at runtime (warden = your EmberOvenWarden)")]
    public EmberOvenWarden warden;

    // Local events (optional UI hooks)
    public Action<int, int> OnStartAttack;
    public Action<int> OnPhaseChange;
    public Action<float> OnHealthSync;
    public Action<Vector3> OnImpactVfx;

    void Awake()
    {
        if (warden == null)
            warden = GetComponent<EmberOvenWarden>() ?? GetComponentInChildren<EmberOvenWarden>(true);

        Debug.Log($"[NetSync] Awake. Warden={(warden ? warden.name : "NULL")}");
    }

    // --- Required (no state to sync) ---
    public override void AssembleData(Writer writer, byte LOD = 100) { }
    public override void DisassembleData(Reader reader, byte LOD = 100) { }

    // ========== GENERIC EVENTS (optional) ==========
    public void BroadcastStartAttack(int attackId, int seed)
    {
        Debug.Log($"[NetSync] BroadcastStartAttack id={attackId} seed={seed}");
        BroadcastRemoteMethod(nameof(RPC_StartAttack), attackId, seed);
        OnStartAttack?.Invoke(attackId, seed);
    }
    [SynchronizableMethod] public void RPC_StartAttack(int id, int seed) { OnStartAttack?.Invoke(id, seed); }

    public void BroadcastPhase(int phase)
    {
        Debug.Log($"[NetSync] BroadcastPhase {phase}");
        BroadcastRemoteMethod(nameof(RPC_Phase), phase);
        OnPhaseChange?.Invoke(phase);
    }
    [SynchronizableMethod] public void RPC_Phase(int p) { OnPhaseChange?.Invoke(p); }

    public void BroadcastHealth(float n)
    {
        Debug.Log($"[NetSync] BroadcastHealth {n:0.000}");
        BroadcastRemoteMethod(nameof(RPC_Health), n);
        OnHealthSync?.Invoke(n);
    }
    [SynchronizableMethod] public void RPC_Health(float n) { OnHealthSync?.Invoke(n); }

    public void BroadcastImpact(Vector3 pos)
    {
        Debug.Log($"[NetSync] BroadcastImpact {pos}");
        BroadcastRemoteMethod(nameof(RPC_Impact), pos);
        OnImpactVfx?.Invoke(pos);
    }
    [SynchronizableMethod] public void RPC_Impact(Vector3 p) { OnImpactVfx?.Invoke(p); }

    // ========= YAW REPLICATION =========
    public void BroadcastYaw(float yaw)
    {
        // send to everyone (incl. host) and apply locally
        // NOTE: don't spam—caller should rate-limit
        BroadcastRemoteMethod(nameof(RPC_SetYaw), yaw);
        if (warden != null) warden.LocalSetYaw(yaw);
        Debug.Log($"[NetSync] BroadcastYaw -> {yaw:0.0}");
    }
    [SynchronizableMethod]
    public void RPC_SetYaw(float yaw)
    {
        if (warden != null) warden.LocalSetYaw(yaw);
        Debug.Log($"[NetSync] RPC_SetYaw -> {yaw:0.0}");
    }

    // ========= FIREBALL SPAWN =========
    public void BroadcastSpawnFireball(Vector3 start, Vector3 dest, float speedScale)
    {
        Debug.Log($"[NetSync] BroadcastSpawnFireball start={start} dest={dest} scale={speedScale:0.00}");
        BroadcastRemoteMethod(nameof(RPC_SpawnFireball), start, dest, speedScale);
        // apply locally too so host sees it instantly
        if (warden != null) warden.LocalSpawnFireball(start, dest, speedScale);
    }
    [SynchronizableMethod]
    public void RPC_SpawnFireball(Vector3 start, Vector3 dest, float speedScale)
    {
        if (warden != null) warden.LocalSpawnFireball(start, dest, speedScale);
        Debug.Log($"[NetSync] RPC_SpawnFireball start={start} dest={dest} scale={speedScale:0.00}");
    }

    // ========= VENT SPAWN =========
    public void BroadcastSpawnVent(Vector3 pos, float scale)
    {
        Debug.Log($"[NetSync] BroadcastSpawnVent pos={pos} scale={scale:0.00}");
        BroadcastRemoteMethod(nameof(RPC_SpawnVent), pos, scale);
        if (warden != null) warden.LocalSpawnVent(pos, scale);
    }
    [SynchronizableMethod]
    public void RPC_SpawnVent(Vector3 pos, float scale)
    {
        if (warden != null) warden.LocalSpawnVent(pos, scale);
        Debug.Log($"[NetSync] RPC_SpawnVent pos={pos} scale={scale:0.00}");
    }
}
