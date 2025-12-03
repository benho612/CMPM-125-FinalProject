using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MeteorProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float fallSpeed = 20f;
    public float maxLifetime = 5f;

    [Header("Damage")]
    public float damage = 25f;
    public LayerMask hitLayers;   // who can be hit

    [Header("Impact VFX (optional)")]
    public GameObject impactVfxPrefab;
    public float impactVfxLifetime = 2f;

    Vector3 _targetPosition;
    bool _hasTarget = false;

    void Start()
    {
        Destroy(gameObject, maxLifetime);
    }

    public void SetTarget(Vector3 targetPos)
    {
        _targetPosition = targetPos;
        _hasTarget = true;
    }

    void Update()
    {
        if (!_hasTarget)
            return;

        // move down towards target
        transform.position = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            fallSpeed * Time.deltaTime
        );

        // if we reached the ground point but OnTriggerEnter hasn't fired yet
        if (Vector3.Distance(transform.position, _targetPosition) < 0.1f)
        {
            Impact(null);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & hitLayers) == 0)
            return; // ignore layers we don't want to hit

        Impact(other);
    }

    void Impact(Collider hit)
    {
        // TODO: your damage system here
        // Example:
        // var hp = hit ? hit.GetComponent<Health>() : null;
        // if (hp != null) hp.TakeDamage(damage);

        if (impactVfxPrefab != null)
        {
            GameObject fx = Instantiate(
                impactVfxPrefab,
                transform.position,
                Quaternion.identity
            );
            Destroy(fx, impactVfxLifetime);
        }

        Destroy(gameObject);
    }
}
