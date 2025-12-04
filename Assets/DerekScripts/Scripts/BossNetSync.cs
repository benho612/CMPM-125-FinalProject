using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Alteruna;

public class BossNetSync : Synchronizable
{
    [Header("Optional references (auto-found if missing)")]
    public EmberOvenWarden warden;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private BossHealth health;

    [Header("RPC resend safety")]
    [SerializeField] private int resendCount = 3;
    [SerializeField] private float resendInterval = 0.12f;

    // Client-side yaw smoothing
    public float LastReceivedYaw { get; private set; }

    private readonly HashSet<int> seen = new HashSet<int>();
    private readonly Queue<int> seenQueue = new Queue<int>();
    private const int SeenCap = 256;

    private Multiplayer multiplayer;

    private int seqCounter = 1;
    private int NextSeq()
    {
        seqCounter++;
        if (seqCounter == 0) seqCounter = 1;
        return seqCounter;
    }

    private Func<string, object[], bool> _sendRemote; // returns true if actually sent
    private string _sendRemoteName = "NONE";

    private void Awake()
    {
        multiplayer = FindObjectOfType<Multiplayer>();

        if (warden == null) warden = GetComponent<EmberOvenWarden>();
        if (bossAnimator == null) bossAnimator = GetComponentInChildren<Animator>();
        if (health == null) health = GetComponent<BossHealth>();

        LastReceivedYaw = transform.eulerAngles.y;

        InitRemoteSender();

        Debug.Log($"[BossNetSync] Awake on {(IsHost() ? "HOST" : "CLIENT")} (Object='{name}'), Sender={_sendRemoteName}.");
    }

    private void Start()
    {
        if (IsHost() && health != null)
        {
            SendRemote(nameof(RPC_Health), health.HP, health.MaxHP);
            StartCoroutine(ResendHealth(health.HP, health.MaxHP));
        }
    }

    private bool IsHost()
    {
        if (multiplayer == null) multiplayer = FindObjectOfType<Multiplayer>();
        return multiplayer != null && multiplayer.Me != null && multiplayer.Me.IsHost;
    }

    private void Update()
    {
        base.SyncUpdate();
    }

    private void InitRemoteSender()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        MethodInfo Find(string name)
        {
            foreach (var m in GetType().GetMethods(flags))
            {
                if (m.Name != name) continue;
                var p = m.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(object[]))
                    return m;
            }
            var bt = typeof(Synchronizable);
            return bt.GetMethod(name, flags, null, new[] { typeof(string), typeof(object[]) }, null);
        }

        MethodInfo invoke = Find("InvokeRemoteMethod");
        if (invoke != null)
        {
            _sendRemoteName = "InvokeRemoteMethod(string, object[])";
            _sendRemote = (method, args) =>
            {
                invoke.Invoke(this, new object[] { method, args });
                return true;
            };
            return;
        }

        MethodInfo broadcast = Find("BroadcastRemoteMethod");
        if (broadcast != null)
        {
            _sendRemoteName = "BroadcastRemoteMethod(string, object[])";
            _sendRemote = (method, args) =>
            {
                broadcast.Invoke(this, new object[] { method, args });
                return true;
            };
            return;
        }

        _sendRemoteName = "NO_REMOTE_SENDER_FOUND";
        _sendRemote = (_, __) => false;
        Debug.LogError("[BossNetSync] Could not find InvokeRemoteMethod or BroadcastRemoteMethod that matches (string, object[]). Remote RPCs will NOT work.");
    }

    private bool SendRemote(string methodName, params object[] args)
    {
        if (_sendRemote == null) InitRemoteSender();
        bool ok = _sendRemote(methodName, args);
        if (!ok)
        {
            Debug.LogError($"[BossNetSync] FAILED to send remote '{methodName}'. Sender={_sendRemoteName}. Trying InvokeRemoteMethod fallback.");
            try
            {
                InvokeRemoteMethod(methodName, args);
                Debug.Log($"[BossNetSync] Fallback InvokeRemoteMethod succeeded for '{methodName}'.");
                ok = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BossNetSync] Fallback InvokeRemoteMethod failed for '{methodName}': {ex}");
            }
        }
        return ok;
    }

    // -------------------------
    // Host-authoritative health and damage
    // -------------------------

    public void RequestDamage(float amount)
    {
        if (IsHost())
        {
            RPC_RequestDamage(amount);
        }
        else
        {
            SendRemote(nameof(RPC_RequestDamage), amount);
        }
    }

    [SynchronizableMethod]
    public void RPC_RequestDamage(float amount)
    {
        if (!IsHost()) return;

        if (health == null) health = GetComponent<BossHealth>();
        if (health == null) return;

        float before = health.HP;
        health.Damage(amount);
        Debug.Log($"[BossNetSync] Host applied damage={amount:0}. HP {before:0} -> {health.HP:0}");

        SendRemote(nameof(RPC_Health), health.HP, health.MaxHP);
        StartCoroutine(ResendHealth(health.HP, health.MaxHP));
    }

    private IEnumerator ResendHealth(float current, float max)
    {
        for (int i = 0; i < resendCount; i++)
        {
            yield return new WaitForSeconds(resendInterval);
            if (!IsHost()) yield break;
            SendRemote(nameof(RPC_Health), current, max);
        }
    }

    [SynchronizableMethod]
    public void RPC_Health(float current, float max)
    {
        if (health == null) health = GetComponent<BossHealth>();
        if (health == null) return;

        health.SetFromNetwork(current, max);
        Debug.Log($"[BossNetSync] RPC_Health {(IsHost() ? "(LOCAL)" : "(REMOTE)")} -> {current:0}/{max:0}");
    }

    // -------------------------
    // Scene load RPC (similar to ScenePortal)
    // -------------------------

    public void BroadcastLoadSceneAfterDelay(string sceneName, float delaySeconds)
    {
        if (!IsHost()) return;
        StartCoroutine(DelayAndBroadcastLoadScene(sceneName, delaySeconds));
    }

    private IEnumerator DelayAndBroadcastLoadScene(string sceneName, float delaySeconds)
    {
        Debug.Log($"[BossNetSync] Scheduling scene load '{sceneName}' in {delaySeconds:0.##}s.");
        yield return new WaitForSeconds(Mathf.Max(0f, delaySeconds));

        // Explicitly invoke RPC over Alteruna Synchronizable
        try
        {
            InvokeRemoteMethod(nameof(RPC_LoadScene), sceneName);
            Debug.Log($"[BossNetSync] InvokeRemoteMethod(RPC_LoadScene, '{sceneName}') dispatched to peers.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[BossNetSync] InvokeRemoteMethod failed for RPC_LoadScene: {ex}");
        }

        // Host also loads locally
        RPC_LoadScene(sceneName);
    }

    [SynchronizableMethod]
    public void RPC_LoadScene(string sceneName)
    {
        var inRoom = Multiplayer != null && Multiplayer.InRoom;
        var userIdx = Multiplayer != null && Multiplayer.GetUser() != null ? Multiplayer.GetUser().Index : -1;
        Debug.Log($"[BossNetSync] RPC_LoadScene on {(IsHost() ? "HOST" : "CLIENT")} user={userIdx} InRoom={inRoom} -> '{sceneName}'");

        Multiplayer.LoadScene(sceneName, true);
    }

    // -------------------------
    // Existing broadcast helpers (HOST only)
    // -------------------------

    public void BroadcastPhase(int phase)
    {
        if (!IsHost()) return;
        SendRemote(nameof(RPC_Phase), phase);
        RPC_Phase(phase);
    }

    public void BroadcastStartAttack(int attackId, int seed)
    {
        if (!IsHost()) return;
        SendRemote(nameof(RPC_StartAttack), attackId, seed);
        RPC_StartAttack(attackId, seed);
    }

    public void BroadcastCrossFade(int stateHash, float blend)
    {
        if (!IsHost()) return;
        SendRemote(nameof(RPC_CrossFade), stateHash, blend);
        RPC_CrossFade(stateHash, blend);
    }

    public void BroadcastYaw(float yaw)
    {
        if (!IsHost()) return;
        SendRemote(nameof(RPC_Yaw), yaw);
    }

    public void BroadcastSpawnFireball(Vector3 start, Vector3 dest, float scale)
        => BroadcastSpawnFireball(NextSeq(), start, dest, scale);

    public void BroadcastSpawnVent(Vector3 pos, float scale)
        => BroadcastSpawnVent(NextSeq(), pos, scale);

    public void BroadcastSpawnVent(int seq, Vector3 pos, float scale)
    {
        if (!IsHost()) return;

        SendRemote(nameof(RPC_SpawnVent), seq, pos.x, pos.y, pos.z, scale);
        RPC_SpawnVent(seq, pos.x, pos.y, pos.z, scale);

        StartCoroutine(ResendVent(seq, pos, scale));
    }

    public void BroadcastSpawnFireball(int seq, Vector3 start, Vector3 dest, float scale)
    {
        if (!IsHost()) return;

        SendRemote(nameof(RPC_SpawnFireball),
            seq,
            start.x, start.y, start.z,
            dest.x, dest.y, dest.z,
            scale
        );

        RPC_SpawnFireball(
            seq,
            start.x, start.y, start.z,
            dest.x, dest.y, dest.z,
            scale
        );

        StartCoroutine(ResendFireball(seq, start, dest, scale));
    }

    private IEnumerator ResendVent(int seq, Vector3 pos, float scale)
    {
        for (int i = 0; i < resendCount; i++)
        {
            yield return new WaitForSeconds(resendInterval);
            if (!IsHost()) yield break;
            SendRemote(nameof(RPC_SpawnVent), seq, pos.x, pos.y, pos.z, scale);
        }
    }

    private IEnumerator ResendFireball(int seq, Vector3 start, Vector3 dest, float scale)
    {
        for (int i = 0; i < resendCount; i++)
        {
            yield return new WaitForSeconds(resendInterval);
            if (!IsHost()) yield break;

            SendRemote(nameof(RPC_SpawnFireball),
                seq,
                start.x, start.y, start.z,
                dest.x, dest.y, dest.z,
                scale
            );
        }
    }

    [SynchronizableMethod]
    public void RPC_Phase(int phase)
    {
        Debug.Log($"[BossNetSync] RPC_Phase {(IsHost() ? "(LOCAL)" : "(REMOTE)")} phase={phase}");
    }

    [SynchronizableMethod]
    public void RPC_StartAttack(int attackId, int seed)
    {
        Debug.Log($"[BossNetSync] RPC_StartAttack {(IsHost() ? "(LOCAL)" : "(REMOTE)")} attackId={attackId}, seed={seed}");
    }

    [SynchronizableMethod]
    public void RPC_CrossFade(int stateHash, float blend)
    {
        Debug.Log($"[BossNetSync] RPC_CrossFade {(IsHost() ? "(LOCAL)" : "(REMOTE)")} hash={stateHash}, blend={blend:0.##}");

        if (bossAnimator == null) bossAnimator = GetComponentInChildren<Animator>();
        if (bossAnimator != null)
            bossAnimator.CrossFade(stateHash, blend);
    }

    [SynchronizableMethod]
    public void RPC_Yaw(float yaw)
    {
        LastReceivedYaw = yaw;
        if (warden != null) warden.LocalSetYaw(yaw);
    }

    [SynchronizableMethod]
    public void RPC_SpawnVent(int seq, float x, float y, float z, float scale)
    {
        if (AlreadySeen(seq)) return;

        var pos = new Vector3(x, y, z);
        Debug.Log($"[BossNetSync] RPC_SpawnVent {(IsHost() ? "(LOCAL)" : "(REMOTE)")} seq={seq} pos={pos} scale={scale:0.##}");

        if (warden != null) warden.LocalSpawnVent(pos, scale);
    }

    [SynchronizableMethod]
    public void RPC_SpawnFireball(
        int seq,
        float sx, float sy, float sz,
        float dx, float dy, float dz,
        float scale
    )
    {
        if (AlreadySeen(seq)) return;

        var start = new Vector3(sx, sy, sz);
        var dest = new Vector3(dx, dy, dz);

        Debug.Log($"[BossNetSync] RPC_SpawnFireball {(IsHost() ? "(LOCAL)" : "(REMOTE)")} seq={seq} start={start} dest={dest} scale={scale:0.##}");

        if (warden != null) warden.LocalSpawnFireball(start, dest, scale);
    }

    private bool AlreadySeen(int seq)
    {
        if (seq == 0) return false;
        if (!seen.Add(seq)) return true;

        seenQueue.Enqueue(seq);
        while (seenQueue.Count > SeenCap)
            seen.Remove(seenQueue.Dequeue());

        return false;
    }

    public override void AssembleData(Writer writer, byte LOD = MAX_LOD) { }
    public override void DisassembleData(Reader reader, byte LOD = MAX_LOD) { }
}