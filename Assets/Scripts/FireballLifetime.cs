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
        // Don¡¦t move until the slash is activated
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

        if (other.CompareTag("Player"))
            return;  // ignore player completely

        _hasHit = true;

        // Spawn impact VFX
        if (hitVfxPrefab != null)
        {
            GameObject fx = Instantiate(hitVfxPrefab, transform.position, Quaternion.identity);
            Destroy(fx, hitVfxLifetime);
        }

        // Destroy slash immediately on impact
        Destroy(gameObject);
    }
}
