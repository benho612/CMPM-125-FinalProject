using UnityEngine;
using UnityEngine.SceneManagement;
using Alteruna;

[RequireComponent(typeof(Alteruna.Avatar))]
public class AvatarSceneSpawn : MonoBehaviour
{
    Alteruna.Avatar _avatar;

    void Awake()
    {
        _avatar = GetComponent<Alteruna.Avatar>();
        DontDestroyOnLoad(gameObject); // keep avatar across scenes
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only reposition our own local avatar; Alteruna will replicate transform to others
        if (_avatar == null || !_avatar.IsMe) return;

        // Choose spawn index: host ? A, client ? B (simple 2-player rule)
        bool isHost = SceneDirector.Instance && SceneDirector.Instance.IsHost;
        string spawnName = isHost ? "Spawn_A" : "Spawn_B";

        var target = GameObject.Find(spawnName);
        if (target != null)
        {
            transform.position = target.transform.position;
            transform.rotation = target.transform.rotation;
        }
        else
        {
            // Fallback: center
            transform.position = Vector3.zero;
        }
    }
}
