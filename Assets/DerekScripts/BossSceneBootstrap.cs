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

        // Start the fight
        var bc = bossGo.GetComponent<BossController>();
        if (bc == null)
        {
            Debug.LogError("[BossBootstrap] Spawned boss has no BossController.");
            yield break;
        }

        int seed = Random.Range(0, int.MaxValue);
        Debug.Log($"[BossBootstrap] Spawning boss and starting fight; seed={seed}");
        bc.StartFight(seed);
    }
}
