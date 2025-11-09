using Unity.Netcode;
using UnityEngine;

public class IngredientSpawner : NetworkBehaviour
{
    public GameObject ingredientPrefab;
    public int count = 5;
    public Vector2 area = new Vector2(10, 10);

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = new Vector3(Random.Range(-area.x, area.x), 0.5f, Random.Range(-area.y, area.y));
            var obj = Instantiate(ingredientPrefab, pos, Quaternion.identity);
            obj.GetComponent<NetworkObject>().Spawn();
        }
    }
}
