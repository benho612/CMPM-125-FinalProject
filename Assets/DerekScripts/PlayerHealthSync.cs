using Alteruna;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Alteruna.Avatar))]
public class PlayerHealthSync : Synchronizable
{
    [Header("Links")]
    public PlayerDamageReceiver receiver;

    [Header("Debug")]
    [Tooltip("Enable to print lifecycle and RPC logs.")]
    public bool verbose = false;

    private Alteruna.Avatar _avatar;
    private bool _respawnScheduled;

    void Awake()
    {
        _avatar = GetComponent<Alteruna.Avatar>();

        if (receiver == null)
            receiver = GetComponent<PlayerDamageReceiver>();
        if (receiver == null)
            receiver = GetComponentInChildren<PlayerDamageReceiver>(true);

        if (verbose)
        {
            Debug.Log($"[PlayerHealthSync] Awake '{gameObject.name}': receiver={(receiver != null ? receiver.name : "NULL")}, owner={(_avatar?.Owner?.Name ?? "NULL")}, isMe={(_avatar?.IsMe ?? false)}");
        }

        StartCoroutine(LogAfterAvatarReady());
    }
    private void Update()
    {
        base.SyncUpdate();
    }
    private IEnumerator LogAfterAvatarReady()
    {
        // Wait one frame for Alteruna to finish setting up the avatar
        yield return null;

        // In some cases you may need to wait longer until Owner is assigned
        float timeout = 2f;
        while (timeout > 0f && (_avatar != null && _avatar.Owner == null))
        {
            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (receiver == null)
        {
            receiver = GetComponent<PlayerDamageReceiver>();
            if (receiver == null)
                receiver = GetComponentInChildren<PlayerDamageReceiver>(true);
        }

        if (verbose)
        {
            Debug.Log($"[PlayerHealthSync] Ready '{gameObject.name}': receiver={(receiver != null ? receiver.name : "NULL")}, owner={(_avatar?.Owner?.Name ?? "NULL")}, isMe={(_avatar?.IsMe ?? false)}");
        }
    }

    // Host → owner: apply blockable damage
    [SynchronizableMethod]
    public void RPC_ApplyDamage(float amount)
    {
        if (receiver != null) receiver.ApplyDamage(amount);
        else Debug.LogWarning("[PlayerHealthSync] RPC_ApplyDamage called but receiver is null.");
    }

    // Host → owner: apply unblockable damage
    [SynchronizableMethod]
    public void RPC_ApplyUnblockableDamage(float amount)
    {
        if (receiver != null) receiver.ApplyUnblockableDamage(amount);
        else Debug.LogWarning("[PlayerHealthSync] RPC_ApplyUnblockableDamage called but receiver is null.");
    }

    // Host → peers: directly set health (optional)
    [SynchronizableMethod]
    public void RPC_SetHealth(float newHp, float newMaxHp)
    {
        if (receiver == null)
        {
            Debug.LogWarning("[PlayerHealthSync] RPC_SetHealth called but receiver is null.");
            return;
        }

        receiver.maxHp = Mathf.Max(1f, newMaxHp);
        float before = receiver.hp;
        receiver.hp = Mathf.Clamp(newHp, 0f, receiver.maxHp);
        receiver.OnHealthChanged?.Invoke(receiver.hp, receiver.maxHp);

        if (verbose)
        {
            Debug.Log($"[PlayerHealthSync] SetHealth: {before:0.##}->{receiver.hp:0.##}/{receiver.maxHp:0.##} ({receiver.name}).");
        }

        // Only the owning client should run respawn; others will be mirrored via RPC.
        if (receiver.hp <= 0f && !_respawnScheduled && (_avatar?.IsMe ?? false))
        {
            _respawnScheduled = true;
            if (verbose) Debug.Log("[PlayerHealthSync] Schedule respawn in 10s (owner).");
            StartCoroutine(RespawnAfterDelay(10f));
        }
    }

    // Owner-only: perform respawn locally and then mirror to peers
    private IEnumerator RespawnAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (receiver == null)
        {
            _respawnScheduled = false;
            Debug.LogWarning("[PlayerHealthSync] Respawn skipped; receiver missing.");
            yield break;
        }

        try
        {
            receiver.SendMessage("Respawn", SendMessageOptions.DontRequireReceiver);
            receiver.hp = receiver.maxHp;
            receiver.OnHealthChanged?.Invoke(receiver.hp, receiver.maxHp);
            if (verbose) Debug.Log("[PlayerHealthSync] Respawn complete; mirroring health.");
        }
        finally
        {
            _respawnScheduled = false;
        }

        // Mirror the new health to other peers
        try
        {
            InvokeRemoteMethod(nameof(RPC_SetHealth), receiver.hp, receiver.maxHp);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerHealthSync] RPC_SetHealth after respawn error: {ex.Message}");
        }
    }

    public override void AssembleData(Writer writer, byte LOD = 100)
    {
        if (receiver == null)
        {
            writer.Write(0f);
            writer.Write(100f);
            return;
        }

        writer.Write(receiver.hp);
        writer.Write(receiver.maxHp);
    }

    public override void DisassembleData(Reader reader, byte LOD = 100)
    {
        float newHp = reader.ReadFloat();
        float newMaxHp = reader.ReadFloat();

        if (receiver == null)
        {
            receiver = GetComponent<PlayerDamageReceiver>();
            if (receiver == null)
                receiver = GetComponentInChildren<PlayerDamageReceiver>(true);
        }

        RPC_SetHealth(newHp, newMaxHp);
    }
}