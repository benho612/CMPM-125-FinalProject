// Assets/_Net/AvatarSpawnOnLoad.cs
using Alteruna;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(Alteruna.Avatar))]
[RequireComponent(typeof(CharacterController))]
public class AvatarSpawnOnLoad : MonoBehaviour
{
    private Alteruna.Avatar _avatar;
    private CharacterController _cc;

    [Tooltip("Prefix of spawn transforms, e.g. Spawn_0, Spawn_1")]
    public string spawnPrefix = "Spawn_";

    [Tooltip("Max time to wait for spawns to appear in the new scene")]
    public float waitForSpawnTimeout = 1.0f; // seconds

    void Awake()
    {
        _avatar = GetComponent<Alteruna.Avatar>();
        _cc = GetComponent<CharacterController>();
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_avatar == null || !_avatar.IsMe) return;
        StartCoroutine(PlaceAtSpawnWhenReady());
    }

    IEnumerator PlaceAtSpawnWhenReady()
    {
        // wait a few frames (and up to timeout) for scene content to activate
        float t = 0f;
        Transform spawn = null;
        while (t < waitForSpawnTimeout)
        {
            spawn = FindMySpawn();
            if (spawn != null) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (spawn == null)
        {
            Debug.LogWarning("[Spawn] No spawn found. Staying at current position.");
            yield break;
        }

        // Teleport safely with CC off to avoid ground penetration issues
        _cc.enabled = false;
        transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        _cc.enabled = true;
    }

    Transform FindMySpawn()
    {
        var mp = FindFirstObjectByType<Multiplayer>();
        int idx = 0;
        if (mp != null && mp.InRoom && mp.GetUser() != null)
            idx = mp.GetUser().Index; // 0 = host, 1 = client

        var go = GameObject.Find(spawnPrefix + idx); // requires active objects
        return go ? go.transform : null;
    }
}
