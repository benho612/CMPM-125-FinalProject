// Assets/Bosses/_Shared/BossNetSync.cs
using System;
using UnityEngine;
using Alteruna;
using Alteruna.Trinity; // for Reader/Writer

public class BossNetSync : Synchronizable
{
    // Local events you can hook UI/VFX to
    public Action<int, int> OnStartAttack;   // (attackId, seed)
    public Action<int> OnPhaseChange;   // phaseIndex
    public Action<float> OnHealthSync;    // 0..1
    public Action<Vector3> OnImpactVfx;     // impact pos

    // ---- Required by Alteruna.Synchronizable (no state -> leave empty) ----
    public override void AssembleData(Writer writer, byte LOD = 100) { }
    public override void DisassembleData(Reader reader, byte LOD = 100) { }

    // ---- Host -> everyone (including host) ----
    public void BroadcastStartAttack(int attackId, int seed)
    {
        // includes sender so host UI also gets the event
        BroadcastRemoteMethod(nameof(RPC_StartAttack), attackId, seed);
        OnStartAttack?.Invoke(attackId, seed); // local invoke as well (safety)
    }

    public void BroadcastPhase(int phase)
    {
        BroadcastRemoteMethod(nameof(RPC_Phase), phase);
        OnPhaseChange?.Invoke(phase);
    }

    public void BroadcastHealth(float norm)
    {
        BroadcastRemoteMethod(nameof(RPC_Health), norm);
        OnHealthSync?.Invoke(norm);
    }

    public void BroadcastImpact(Vector3 pos)
    {
        BroadcastRemoteMethod(nameof(RPC_Impact), pos);
        OnImpactVfx?.Invoke(pos);
    }

    // ---- RPC targets (called on all peers) ----
    [SynchronizableMethod] public void RPC_StartAttack(int attackId, int seed) => OnStartAttack?.Invoke(attackId, seed);
    [SynchronizableMethod] public void RPC_Phase(int p) => OnPhaseChange?.Invoke(p);
    [SynchronizableMethod] public void RPC_Health(float n) => OnHealthSync?.Invoke(n);
    [SynchronizableMethod] public void RPC_Impact(Vector3 p) => OnImpactVfx?.Invoke(p);
}
