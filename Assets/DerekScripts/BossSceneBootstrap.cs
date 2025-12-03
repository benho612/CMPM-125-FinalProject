// Assets/DerekScripts/Scripts/BossSceneBootstrap.cs
using System.Collections;
using UnityEngine;
using Alteruna;

public class BossSceneBootstrap : MonoBehaviour
{
    public Multiplayer multiplayer;   // auto-wired if left null
    public Spawner spawner;           // auto-wired if left null
    public int bossPrefabIndex = 0;   // set to the index shown in Spawner
    public Transform bossSpawn;       // assign your BossSpawn
    public int expectedPlayers = 2;   // wait until both avatars present

    private IEnumerator Start()
    {
        if (!multiplayer) multiplayer = FindObjectOfType<Multiplayer>();
        if (!spawner) spawner = FindObjectOfType<Spawner>();

        // Wait until the room & local user exist
        while (multiplayer == null || !multiplayer.InRoom || multiplayer.Me == null)
            yield return null;

        // (Optional) wait for all avatars to be in the scene
        while (FindObjectsOfType<Alteruna.Avatar>().Length < expectedPlayers)
            yield return null;

        // Host check (Alteruna host)
        bool isHost = multiplayer.Me.IsHost || multiplayer.Me.Index == 0;
        if (!isHost)
        {
            Debug.Log("[BossBootstrap] Client ready (host will spawn boss).");
            yield break;
        }

        // Spawn boss on all peers
        Vector3 pos = bossSpawn ? bossSpawn.position : Vector3.zero;
        Quaternion rot = bossSpawn ? bossSpawn.rotation : Quaternion.identity;

        var bossGo = spawner.Spawn(bossPrefabIndex, pos, rot);
        if (bossGo == null)
        {
            Debug.LogError("[BossBootstrap] Failed to spawn boss. Check Spawner prefab index and Spawner on Multiplayer.");
            yield break;
        }

        var bc = bossGo.GetComponent<BossController>();
        if (bc == null)
        {
            Debug.LogError("[BossBootstrap] Spawned boss has no BossController.");
            yield break;
        }

        // Wait for client-ready handshake from spawned clients (with a timeout)
        var net = bossGo.GetComponent<BossNetSync>();
        float timeout = 8f; // increased to give clients and network a little more time
        float elapsed = 0f;
        int need = Mathf.Max(0, expectedPlayers - 1); // clients expected to report (exclude host)
        Debug.Log($"[BossBootstrap] Spawned boss; waiting for up to {timeout:0.0}s for {need} client ready(s).");
        while (elapsed < timeout && (net == null || net.ClientsReadyCount < need))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (net == null)
            Debug.LogWarning("[BossBootstrap] BossNetSync missing on spawned boss; proceeding anyway.");
        else if (net.ClientsReadyCount < need)
            Debug.LogWarning($"[BossBootstrap] Timeout waiting for client ready(s). Received={net.ClientsReadyCount}, Expected={need}");

        // Diagnostic: broadcast a test RPC now that clients reported ready
        if (net != null)
        {
            Debug.Log("[BossBootstrap] Sending diagnostic BroadcastTest(42) from host to spawned boss net.");
            net.BroadcastTest(42);
        }

        int seed = Random.Range(0, int.MaxValue);
        Debug.Log($"[BossBootstrap] Starting fight; seed={seed}");
        bc.StartFight(seed);
    }
}