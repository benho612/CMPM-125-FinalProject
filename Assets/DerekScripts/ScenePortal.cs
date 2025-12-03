// ScenePortal.cs
using Alteruna;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ScenePortal : AttributesSync
{
    [Tooltip("Exact name from Build Settings")]
    public string targetSceneName = "FireScene";
    public string playerTag = "Player";
    public bool hostOnly = true;
    public float cooldown = 1f;
    float _last;

    void Reset() { GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (Time.time - _last < cooldown) return;

        var mp = Multiplayer;
        if (hostOnly && (mp == null || !mp.InRoom || mp.GetUser().Index != 0)) return;

        _last = Time.time;
        BroadcastRemoteMethod(nameof(RpcLoadScene), targetSceneName);
    }

    [SynchronizableMethod]
    void RpcLoadScene(string sceneName)
    {
        Multiplayer?.LoadScene(sceneName, true); // moves avatars with the scene
    }
}
