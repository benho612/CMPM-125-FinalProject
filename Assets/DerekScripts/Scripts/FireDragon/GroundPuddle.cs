// Assets/Bosses/_Shared/GroundPuddle.cs
using UnityEngine;

public class GroundPuddle : MonoBehaviour
{
    public float radius = 2f;
    public float dps = 8f;
    public float lifetime = 7f;
    public bool authoritative = true;
    public LayerMask hitMask;
    float age;

    void Update()
    {
        age += Time.deltaTime;
        if (authoritative)
        {
            var cols = Physics.OverlapSphere(transform.position, radius, hitMask);
            foreach (var c in cols)
            {
                var dmg = c.GetComponent<PlayerDamageReceiver>();
                if (dmg) dmg.ApplyDamage(dps * Time.deltaTime);
            }
        }
        if (age >= lifetime) Destroy(gameObject);
    }
}
