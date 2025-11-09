using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject), typeof(Collider))]
public class Ingredient : NetworkBehaviour
{
    bool taken;

    void OnTriggerEnter(Collider other)
    {
        if (!IsSpawned || taken) return;
        if (!other.CompareTag("Player")) return;
        TryTakeServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void TryTakeServerRpc()
    {
        if (taken) return;
        taken = true;
        GetComponent<NetworkObject>().Despawn(true);
    }
}
