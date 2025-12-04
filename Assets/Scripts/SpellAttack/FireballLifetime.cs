using UnityEngine;

public class FireballLifetime : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("How long the slash lasts AFTER it begins moving.")]
    public float activeLifetime = 3f;

    [Tooltip("Delay before the slash begins animating / moving.")]
    public float startDelay = 0.5f;

    [Header("Movement")]
    public float speed = 20f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the boss when the fireball hits.")]
    public float bossDamage = 50f;

    [Tooltip("How far from the impact we consider a boss collider hit if the trigger didn't report it directly.")]
    public float bossHitRadius = 0.8f;

    [Header("Hit Effects")]
    public GameObject hitVfxPrefab;
    public float hitVfxLifetime = 1f;

    private bool _active = false;    // becomes true after delay
    private bool _hasHit = false;
    private float _activationTime = 0f;

    private void Start()
    {
        // Start the delayed activation
        Invoke(nameof(ActivateSlash), startDelay);
    }

    private void ActivateSlash()
    {
        _active = true;
        _activationTime = Time.time;
    }

    private void Update()
    {
        // Don't move until the slash is activated
        if (!_active || _hasHit)
            return;

        // Move forward
        transform.position += transform.forward * speed * Time.deltaTime;

        // Auto lifetime expire
        if (Time.time > _activationTime + activeLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit)
            return;

        // Ignore local player completely
        if (other.CompareTag("Player"))
            return;

        // Try to damage boss if present (collider or parent/root)
        bool damagedBoss = TryDamageBoss(other, transform.position);

        _hasHit = true;

        // Spawn impact VFX
        if (hitVfxPrefab != null)
        {
            GameObject fx = Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            Destroy(fx, hitVfxLifetime);
        }

        // Destroy fireball on any hit (visual projectile)
        Destroy(gameObject);
    }

    private bool TryDamageBoss(Collider hitCol, Vector3 impactPos)
    {
        // Look for BossHealth on the collider first, then on any parent
        BossHealth boss = null;
        if (!hitCol.TryGetComponent(out boss))
            boss = hitCol.GetComponentInParent<BossHealth>();

        // If not directly on the hit collider, validate proximity using ClosestPoint against boss colliders
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
                        break; // boss found by proximity
                    }
                }
            }
        }

        if (boss == null)
            return false;

        // Route damage through BossNetSync (host authoritative), fallback to local damage
        var net = boss.GetComponent<BossNetSync>();
        if (net != null)
            net.RequestDamage(bossDamage);
        else
            boss.Damage(bossDamage);

        return true;
    }
}
