using UnityEngine;
using Alteruna;
using System.Collections;

public class BossSceneBootstrap : MonoBehaviour
{
    [SerializeField] private int bossPrefabIndex = 0;
    [SerializeField] private Transform bossSpawn;

    [SerializeField] private float spawnDelay = 1.5f;
    [SerializeField] private int fightSeed = -1; // -1 = random

    private Multiplayer multiplayer;
    private Spawner spawner;

    private void Awake()
    {
        multiplayer = FindObjectOfType<Multiplayer>();
        spawner = FindObjectOfType<Spawner>();
    }

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        // Wait for Multiplayer and Spawner to exist
        while (multiplayer == null || spawner == null)
        {
            multiplayer = FindObjectOfType<Multiplayer>();
            spawner = FindObjectOfType<Spawner>();
            yield return null;
        }

        // Wait until we have a local user (Me) known by the SDK
        Alteruna.User me = null;
        int safety = 0;
        while (me == null && safety++ < 300) // ~5s at 60fps
        {
            try { me = multiplayer.Me; } catch { /* SDK may throw until ready */ }
            yield return null;
        }

        if (me == null)
        {
            Debug.LogError("[BossBootstrap] Multiplayer.Me not available; aborting boss spawn.");
            yield break;
        }

        // Host-only spawns the boss (networked)
        bool isHost = false;
        try { isHost = me.IsHost; } catch { isHost = (me.Index == 0); }

        if (!isHost)
        {
            Debug.Log("[BossBootstrap] Client detected; boss will be received via network spawn.");
            yield break;
        }

        yield return new WaitForSeconds(spawnDelay);

        Vector3 pos = bossSpawn ? bossSpawn.position : Vector3.zero;
        Quaternion rot = bossSpawn ? bossSpawn.rotation : Quaternion.identity;

        // Network instantiate through Alteruna Spawner so all peers receive the same object
        GameObject bossObj = spawner.Spawn(bossPrefabIndex, pos, rot);
        if (!bossObj)
        {
            Debug.LogError("[BossBootstrap] Spawn failed.");
            yield break;
        }

        var netSync = bossObj.GetComponent<BossNetSync>();
        var ctrl = bossObj.GetComponent<BossController>();

        if (!netSync || !ctrl)
        {
            Debug.LogError("[BossBootstrap] Boss is missing BossNetSync or BossController components.");
            yield break;
        }

        // Host-only explicit registration (safe for older Alteruna versions). Clients must not register.
        try
        {
            multiplayer.RegisterSynchronizable(netSync);
            Debug.Log("[BossBootstrap] RegisterSynchronizable called for BossNetSync (HOST).");
        }
        catch
        {
            // Ignore if not required.
        }

        // Give clients time to receive the boss
        yield return new WaitForSeconds(1.0f);

        // Smoke test RPC path
        netSync.BroadcastTest(77777777);

        int seed = fightSeed >= 0 ? fightSeed : Random.Range(0, int.MaxValue);
        ctrl.StartFight(seed);
        Debug.Log($"[BossBootstrap] Fight started with seed {seed} on HOST.");
    }
}