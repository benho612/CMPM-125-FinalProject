using Alteruna;
using UnityEngine;
using System.Collections;

public class SceneRelay : AttributesSync
{
    [Tooltip("Exact scene name as in Build Settings (e.g., KitchenScene or Scenes/KitchenScene).")]
    [SerializeField] private string kitchenSceneName = "KitchenScene";

    [Tooltip("Optional delay before broadcasting the load (seconds).")]
    [SerializeField] private float defaultDelaySeconds = 0f;

    public void HostBroadcastKitchen(float delaySeconds = -1f)
    {
        if (delaySeconds < 0f) delaySeconds = defaultDelaySeconds;
        StartCoroutine(BroadcastAfterDelay(delaySeconds));
    }

    private IEnumerator BroadcastAfterDelay(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        BroadcastRemoteMethod(nameof(RpcLoadKitchen), kitchenSceneName);
        // Host also loads locally to keep in sync with clients
        RpcLoadKitchen(kitchenSceneName);
    }

    [SynchronizableMethod]
    private void RpcLoadKitchen(string sceneName)
    {
        Debug.Log($"[SceneRelay] RpcLoadKitchen -> '{sceneName}'");
        Multiplayer?.LoadScene(sceneName, true);
    }
}