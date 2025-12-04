// Assets/Bosses/_Shared/GroundPuddle.cs
using UnityEngine;

public class GroundPuddle : MonoBehaviour
{
    [Header("Settings")]
    public float radius = 2f;
    public float dps = 5f;              // 5 HP per second
    public float lifetime = 7f;
    public bool authoritative = true;   // host deals damage
    public LayerMask hitMask;           // must include the HurtBox layer

    [Header("Debug")]
    [Tooltip("Enable to print diagnostic logs about overlap and damage.")]
    public bool verbose = false;

    float age;
    private bool _lastAuth;

    void OnEnable()
    {
        _lastAuth = authoritative;
        Debug.Log($"[GroundPuddle] Enabled '{name}' at {transform.position}. auth={authoritative}, radius={radius}, dps={dps}, hitMask={hitMask.value}");
        if (hitMask.value == 0)
            Debug.LogWarning("[GroundPuddle] hitMask=0. Assign the Player/HurtBox layer(s).");

        if (!authoritative)
            Debug.Log($"[GroundPuddle] Non-authoritative instance (visual-only).");

        if (verbose)
        {
            string maskBits = "";
            for (int i = 0; i < 32; i++)
                if (((1 << i) & hitMask.value) != 0) maskBits += i + ",";
            Debug.Log($"[GroundPuddle] hitMask bits: [{maskBits.TrimEnd(',')}]");
        }
    }

    void Update()
    {
        if (authoritative != _lastAuth)
        {
            Debug.LogWarning($"[GroundPuddle] authoritative changed {_lastAuth} -> {authoritative} on '{name}'.");
            _lastAuth = authoritative;
        }

        age += Time.deltaTime;

        // Damage only on authoritative side, but lifetime/despawn always runs.
        if (authoritative)
        {
            var cols = Physics.OverlapSphere(transform.position, radius, hitMask, QueryTriggerInteraction.Collide);

            if ((cols == null || cols.Length == 0) && verbose)
            {
                bool anyInRadiusAnyLayer = Physics.CheckSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
                Debug.Log($"[GroundPuddle] No colliders in hitMask. CheckSphere(any layer)={anyInRadiusAnyLayer}, radius={radius:0.##}, pos={transform.position}");
            }
            else if (verbose)
            {
                Debug.Log($"[GroundPuddle] OverlapSphere found {cols.Length} collider(s) within radius={radius:0.##}.");
            }

            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (c == null) continue;

                var recv = c.GetComponentInParent<PlayerDamageReceiver>();
                if (recv != null)
                {
                    if (!recv.enabled)
                    {
                        Debug.LogWarning($"[GroundPuddle] PlayerDamageReceiver on '{recv.name}' disabled. Skipping.");
                        continue;
                    }

                    float perFrame = dps * Time.deltaTime;
                    if (perFrame <= 0f)
                    {
                        Debug.LogWarning($"[GroundPuddle] dps={dps} -> perFrame={perFrame}.");
                        continue;
                    }

                    float before = recv.hp;
                    recv.ApplyUnblockableDamage(perFrame);

                    var avatar = recv.GetComponent<Alteruna.Avatar>();
                    var sync = (avatar != null ? avatar.GetComponent<PlayerHealthSync>() : recv.GetComponentInParent<PlayerHealthSync>());
                    if (sync != null)
                    {
                        try
                        {
                            sync.InvokeRemoteMethod(nameof(PlayerHealthSync.RPC_SetHealth), recv.hp, recv.maxHp);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[GroundPuddle] RPC_SetHealth error: {ex.Message}");
                        }
                    }

                    if (verbose)
                    {
                        Debug.Log($"[GroundPuddle] Damage '{recv.name}': {perFrame:0.##} → {before:0.##}->{recv.hp:0.##}.");
                    }
                }
                else if (verbose)
                {
                    Debug.Log($"[GroundPuddle] Collider '{c.name}' had no PlayerDamageReceiver in parent chain.");
                }
            }
        }

        // Lifetime/despawn always runs for both host and clients
        if (age >= lifetime)
        {
            Debug.Log($"[GroundPuddle] Destroy after lifetime={lifetime:0.##}s.");
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);

        if (verbose)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.05f);
        }
    }
#endif
}