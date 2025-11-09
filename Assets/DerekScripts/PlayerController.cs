using Unity.Netcode;
using UnityEngine;

public class PlayerController : NetworkBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        if (!IsOwner) return;
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(x, 0, z).normalized;
        if (dir.sqrMagnitude > 0f)
            transform.Translate(dir * moveSpeed * Time.deltaTime, Space.World);
    }
}
