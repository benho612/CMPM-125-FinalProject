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
            // Include triggers explicitly in case players use trigger colliders
            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Collide);
            if (cols.Length == 0)
            {
                Debug.Log($"[GroundPuddle] No players in radius={radius:0.##} at {transform.position}.");
            }

            foreach (var c in cols)
            {
                var dmg = c.GetComponent<PlayerDamageReceiver>();
                if (dmg)
                {
                    float amount = dps * Time.deltaTime;
                    dmg.ApplyUnblockableDamage(amount);
                    Debug.Log($"[GroundPuddle] Damaged '{dmg.name}' for {amount:0.##} DPS at pos={transform.position}.");
                }
                else
                {
                    Debug.Log($"[GroundPuddle] Overlap hit '{c.name}' but no PlayerDamageReceiver found.");
                }
            }
        }

        if (age >= lifetime)
        {
            Debug.Log($"[GroundPuddle] Destroy after lifetime={lifetime:0.##}s.");
            Destroy(gameObject);
        }
    }
}