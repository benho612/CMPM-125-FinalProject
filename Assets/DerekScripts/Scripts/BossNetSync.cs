using System;
using UnityEngine;
using Alteruna;
using Alteruna.Trinity; // Reader/Writer
using System.Collections;
using System.Collections.Generic;

public class BossNetSync : Synchronizable
{
    [Header("Hooked at runtime (warden = your EmberOvenWarden)")]
    public EmberOvenWarden warden;

    // Local events (optional UI hooks)
    public Action<int, int> OnStartAttack;
    public Action<int> OnPhaseChange;
    public Action<float> OnHealthSync;
    public Action<Vector3> OnImpactVfx;

    // Handshake state (host-side counter of client ready notifications)
    [NonSerialized] public int ClientsReadyCount = 0;
    bool _clientNotified = false;

    // host-side set of user indices that have reported ready
    private readonly HashSet<int> _readyUsers = new HashSet<int>();

    void Awake()
    {
        if (warden == null)
            warden = GetComponent<EmberOvenWarden>() ?? GetComponentInChildren<EmberOvenWarden>(true);

        Debug.Log($"[NetSync] Awake. Warden={(warden ? warden.name : "NULL")} (obj={gameObject.name} id={gameObject.GetInstanceID()})");

        var mp = FindObjectOfType<Multiplayer>();
        bool isHost = false;
        if (mp != null)
        {
            try { isHost = mp.Me != null && mp.Me.IsHost; } catch { }
            if (!isHost)
            {
                try { var u = mp.GetUser(); if (u != null) isHost = u.IsHost; } catch { }
            }
        }

        if (!isHost)
        {
            StartCoroutine(NotifyHostReadyWhenRegistered());
        }
    }

    // Fix for CS1061: Replace the problematic line with a check for the correct property or method in the Multiplayer class.
    // Based on the provided type signature, the `Room` property does not exist in the `Multiplayer` class.
    // Instead, we can use the `InRoom` property to check if the player is in a room.

    private IEnumerator NotifyHostReadyWhenRegistered()
    {
        var mp = FindObjectOfType<Multiplayer>();

        // Wait until Multiplayer exists and we are in a room
        while (mp == null || !mp.InRoom)
        {
            mp = FindObjectOfType<Multiplayer>();
            yield return null;
        }

        // Small extra delay to ensure this Synchronizable is fully registered
        yield return new WaitForSeconds(0.5f);

        if (_clientNotified) yield break;

        try
        {
            int userIndex = -1;
            try { userIndex = mp?.GetUser()?.Index ?? mp?.Me?.Index ?? -1; } catch { }

            Debug.Log($"[NetSync] Client notifying host: RPC_ClientReady (obj={gameObject.name} id={gameObject.GetInstanceID()}) sending userIndex={userIndex}");
            BroadcastRemoteMethod(nameof(RPC_ClientReady), userIndex);
            _clientNotified = true;
            Debug.Log("[NetSync] Client-ready broadcast sent.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetSync] Failed to broadcast client-ready: {ex.Message}. Will retry once shortly.");
            StartCoroutine(RetryNotifyHostReady());
        }
    }

    private IEnumerator RetryNotifyHostReady()
    {
        yield return new WaitForSeconds(0.5f);
        try
        {
            var mp = FindObjectOfType<Multiplayer>();
            int userIndex = -1;
            try { userIndex = mp?.GetUser()?.Index ?? mp?.Me?.Index ?? -1; } catch { }

            Debug.Log($"[NetSync] Client retry notifying host: RPC_ClientReady userIndex={userIndex}");
            BroadcastRemoteMethod(nameof(RPC_ClientReady), userIndex);
            _clientNotified = true;
            Debug.Log("[NetSync] Client-ready broadcast sent on retry.");
        }
        catch (Exception ex2)
        {
            Debug.LogWarning($"[NetSync] Retry failed: {ex2.Message}");
        }
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

    // ======= DIAGNOSTIC TEST RPC =======
    public void BroadcastTest(int n)
    {
        Debug.Log($"[NetSync] BroadcastTest -> {n}");
        BroadcastRemoteMethod(nameof(RPC_Test), n);
    }

    [SynchronizableMethod]
    public void RPC_Test(int n)
    {
        Debug.Log($"[NetSync] RPC_Test received -> {n} (obj={gameObject.name} id={gameObject.GetInstanceID()})");
    }

    // ======= CLIENT READY RPC =======
    [SynchronizableMethod]
    public void RPC_ClientReady(int userIndex)
    {
        Debug.Log($"[NetSync] RPC_ClientReady received on object '{gameObject.name}' (id={gameObject.GetInstanceID()}) from userIndex={userIndex}");

        if (userIndex < 0)
        {
            Debug.LogWarning("[NetSync] RPC_ClientReady: received invalid userIndex.");
            return;
        }

        if (_readyUsers.Add(userIndex))
        {
            ClientsReadyCount = _readyUsers.Count;
            Debug.Log($"[NetSync] New client-ready registered for user {userIndex}. ClientsReadyCount={ClientsReadyCount}");
        }
        else
        {
            Debug.Log($"[NetSync] Duplicate client-ready received for user {userIndex} (ignored).");
        }
    }
}