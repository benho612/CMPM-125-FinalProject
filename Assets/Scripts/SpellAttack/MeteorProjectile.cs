using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MeteorProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float fallSpeed = 20f;
    public float maxLifetime = 5f;

    [Header("Damage")]
    public float damage = 25f;
    [Tooltip("Layers this meteor can interact with (boss/environment).")]
    public LayerMask hitLayers;   // who can be hit

    [Tooltip("Radius around impact used to validate boss proximity when no collider was passed.")]
    public float bossHitRadius = 1.2f;

    [Header("Impact VFX (optional)")]
    public GameObject impactVfxPrefab;
    public float impactVfxLifetime = 2f;

    [Header("Camera Shake")]
    public float shakeDuration = 0.25f;
    public float shakeMagnitude = 0.35f;

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
        // Respect layer filtering
        if (((1 << other.gameObject.layer) & hitLayers) == 0)
            return;

        Impact(other);
    }

    void Impact(Collider hit)
    {
        // Damage boss if we hit it directly or are close enough at the impact position
        Vector3 impactPos = transform.position;
        TryDamageBoss(hit, impactPos, damage);

        if (impactVfxPrefab != null)
        {
            GameObject fx = Instantiate(
                impactVfxPrefab,
                impactPos,
                Quaternion.identity
            );
            Destroy(fx, impactVfxLifetime);
        }

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(shakeDuration, shakeMagnitude);
        }

        Destroy(gameObject);
    }

    private bool TryDamageBoss(Collider hitCol, Vector3 impactPos, float dmg)
    {
        BossHealth boss = null;

        // If a collider was provided, try that first
        if (hitCol != null)
        {
            if (!hitCol.TryGetComponent(out boss))
                boss = hitCol.GetComponentInParent<BossHealth>();
        }

        // Otherwise or if not found, proximity validate against boss colliders near the impact
        if (boss == null)
        {
            boss = FindObjectOfType<BossHealth>();
            if (boss != null)
            {
                var bossCols = boss.GetComponentsInChildren<Collider>();
                foreach (var bc in bossCols)
                {
                    if (bc == null || !bc.enabled) continue;
                    var closest = bc.ClosestPoint(impactPos);
                    if (Vector3.Distance(closest, impactPos) <= bossHitRadius)
                    {
                        break; // boss found in range
                    }
                }
            }
        }

        if (boss == null)
            return false;

        var net = boss.GetComponent<BossNetSync>();
        if (net != null)
            net.RequestDamage(dmg);
        else
            boss.Damage(dmg);

        return true;
    }
}
