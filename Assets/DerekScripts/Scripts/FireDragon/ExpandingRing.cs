using UnityEngine;

public class ExpandingRing : MonoBehaviour
{
    public float innerRadius = 0f;
    public float thickness = 2f;
    public float speed = 12f;
    public float damage = 15f;
    public float lifetime = 5f;
    public bool authoritative = true;
    public LayerMask hitMask;

    float age;

    void Update()
    {
        age += Time.deltaTime;
        innerRadius += speed * Time.deltaTime;

        // This line scales the whole prefab (including particles) so visuals expand in sync with logic
        transform.localScale = new Vector3(innerRadius * 2f, 1f, innerRadius * 2f);

        if (authoritative)
        {
            var cols = Physics.OverlapSphere(transform.position, innerRadius + thickness, hitMask)
                ?? System.Array.Empty<Collider>();
            foreach (var c in cols)
            {
                float d = Vector3.Distance(c.transform.position, transform.position);
                if (d > innerRadius && d < innerRadius + thickness)
                {
                    var dmg = c.GetComponent<PlayerDamageReceiver>();
                    if (dmg) dmg.ApplyDamage(damage * Time.deltaTime); // grazing over time
                }
            }
        }

        if (age >= lifetime) Destroy(gameObject);
    }
}
