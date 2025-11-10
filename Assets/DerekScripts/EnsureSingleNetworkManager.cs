using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkManager))]
public class EnsureSingleNetworkManager : MonoBehaviour
{
    void Awake()
    {
        var nm = GetComponent<NetworkManager>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton != nm)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        // Confirm there's only one NetworkManager instance in the scene
        int count = FindObjectsOfType<NetworkManager>().Length;
        if (count == 1)
        {
            Debug.Log($"[EnsureSingleNetworkManager] Confirmed single NetworkManager instance ({nm.gameObject.name}).");
        }
        else
        {
            Debug.LogWarning($"[EnsureSingleNetworkManager] Found {count} NetworkManager instances; expected 1. This instance: {nm.gameObject.name}");
        }
    }
}