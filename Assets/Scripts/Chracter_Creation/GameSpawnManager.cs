using UnityEngine;
using Alteruna;

public class GameSpawnManager : MonoBehaviour
{
    public Multiplayer multiplayer;
    public Transform[] spawnPoints;   // [0] host, [1] client

    void Start()
    {
        if (multiplayer == null)
            multiplayer = FindObjectOfType<Multiplayer>();

        if (multiplayer == null || !multiplayer.InRoom)
        {
            Debug.LogWarning("[GameSpawnManager] No Multiplayer or not in room.");
            return;
        }

        int index = multiplayer.GetUser().Index;
        index = Mathf.Clamp(index, 0, spawnPoints.Length - 1);
        Transform spawn = spawnPoints[index];

        // Find local avatar if it already exists
        Alteruna.Avatar localAvatar = null;
        foreach (var av in FindObjectsOfType<Alteruna.Avatar>())
        {
            if (av.IsMe)
            {
                localAvatar = av;
                break;
            }
        }

        if (localAvatar != null)
        {
            Debug.Log("[GameSpawnManager] Local avatar already exists, moving to spawn.");
            localAvatar.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        }
        else
        {
            Debug.Log("[GameSpawnManager] No avatar yet, spawning at spawn.");
            multiplayer.SpawnAvatar(spawn);
        }
    }
}
