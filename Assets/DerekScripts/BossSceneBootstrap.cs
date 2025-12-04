using System.Collections;
using UnityEngine;
using Alteruna;

public class BossSceneBootstrap : MonoBehaviour
{
    [Header("Spawner settings")]
    [SerializeField] private Spawner spawner;
    [SerializeField] private int bossPrefabIndex = 0;
    [SerializeField] private Transform bossSpawnPoint;

    [Header("Start timing")]
    [SerializeField] private int expectedPlayers = 2;      // set 0 to skip waiting
    [SerializeField] private float maxWaitSeconds = 10f;
    [SerializeField] private float startFightDelay = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool runTestRpcBurst = true;

    private Multiplayer multiplayer;

    private void Awake()
    {
        if (spawner == null) spawner = FindObjectOfType<Spawner>();
        multiplayer = FindObjectOfType<Multiplayer>();
    }

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private bool IsHost()
    {
        return multiplayer != null && multiplayer.Me != null && multiplayer.Me.IsHost;
    }

    private IEnumerator BootstrapRoutine()
    {
        if (multiplayer == null) multiplayer = FindObjectOfType<Multiplayer>();
        if (spawner == null) spawner = FindObjectOfType<Spawner>();

        if (!IsHost())
        {
            Debug.Log("[BossBootstrap] Client detected; boss will be received via network spawn.");
            yield break;
        }

        // Wait for players/avatars so clients don’t miss early RPCs.
        float t = 0f;
        while (expectedPlayers > 0 && CountAvatars() < expectedPlayers && t < maxWaitSeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
        Debug.Log($"[BossBootstrap] Avatars={CountAvatars()} target={expectedPlayers} (waited {t:0.0}s).");

        if (bossSpawnPoint == null)
        {
            Debug.LogError("[BossBootstrap] Missing bossSpawnPoint.");
            yield break;
        }

        Debug.Log($"[BossBootstrap] Spawning boss via Spawner index={bossPrefabIndex}");
        spawner.Spawn(bossPrefabIndex, bossSpawnPoint.position, bossSpawnPoint.rotation);

        // Give Alteruna a moment to finish registration across peers.
        yield return new WaitForSeconds(startFightDelay);

        var bossNet = FindObjectOfType<BossNetSync>();
        var bossController = FindObjectOfType<BossController>();

        if (bossNet == null || bossController == null)
        {
            Debug.LogError($"[BossBootstrap] Missing bossNet/bossController after spawn. bossNet={(bossNet ? "OK" : "NULL")} bossController={(bossController ? "OK" : "NULL")}");
            yield break;
        }

        // IMPORTANT: DO NOT call RegisterSynchronizable here.
        // Spawner/network spawn already registers it. Manual registration causes the “already registered” warning
        // and can break RPC routing.



        int seed = Random.Range(int.MinValue, int.MaxValue);
        bossController.StartFight(seed);
        Debug.Log($"[BossBootstrap] Fight started with seed {seed} on HOST.");
    }

    private int CountAvatars()
    {
        // Works even if Alteruna changes room/user APIs.
        return FindObjectsOfType<Alteruna.Avatar>().Length;
    }
}
