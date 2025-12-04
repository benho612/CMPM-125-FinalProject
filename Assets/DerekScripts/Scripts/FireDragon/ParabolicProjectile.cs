// Assets/Bosses/_Shared/ParabolicProjectile.cs
using UnityEngine;
using System.Collections;

public class ParabolicProjectile : MonoBehaviour
{
    public float speed = 7f;
    public float arcHeight = 3f;
    public float radius = 0.6f;

    public float damage = 5f;
    public float lifetime = 8f;

    // Players/HurtBoxes should be in this mask (must include trigger HurtBox layers)
    public LayerMask hitMask;

    // Ground/Terrain layers to collide with
    public LayerMask groundMask;

    public bool authoritative = true; // host true, clients false (visual only)

    Vector3 start, target;
    float t;

    // Optional: impact VFX prefab when hitting ground
    public GameObject impactVfx;

    // Debug
    public bool verbose = false;

    public void Launch(Vector3 from, Vector3 to, bool hostAuth)
    {
        start = from;
        target = to;
        authoritative = hostAuth;
        t = 0f;
        if (verbose) Debug.Log($"[ParabolicProjectile] Launch auth={authoritative} start={from} dest={to} hitMask={hitMask.value} groundMask={groundMask.value}");
        StartCoroutine(Fly());
    }

    // Key changes: larger radius, overlap counts, head-height check, guaranteed ground impact
    IEnumerator Fly()
    {
        float dist = Vector3.Distance(start, target);
        float dur = Mathf.Max(0.01f, dist / Mathf.Max(0.01f, speed));
        float life = 0f;
        Vector3 prevPos = start;

        while (t < 1f && life < lifetime)
        {
            t += Time.deltaTime / dur;
            life += Time.deltaTime;

            Vector3 p = Vector3.Lerp(start, target, t);
            p.y += arcHeight * 4f * (t - t * t); // parabola
            transform.position = p;

            Vector3 delta = p - prevPos;
            float deltaMag = delta.magnitude;
            Vector3 dir = deltaMag > 1e-4f ? (delta / deltaMag) : Vector3.down;

            if (authoritative)
            {
                // 1) Check players at position and slightly below head height
                var colsAtP = Physics.OverlapSphere(p, radius, hitMask, QueryTriggerInteraction.Collide);
                var colsBelowHead = Physics.OverlapSphere(p + Vector3.down * 0.6f, radius, hitMask, QueryTriggerInteraction.Collide);

                if (verbose) Debug.Log($"[ParabolicProjectile] Overlaps: atP={colsAtP.Length}, belowHead={colsBelowHead.Length}");

                foreach (var c in colsAtP)
                {
                    var recv = c.GetComponentInParent<PlayerDamageReceiver>();
                    if (recv != null && recv.enabled)
                    {
                        ApplyDamageAndSync(recv, p);
                        yield break;
                    }
                }
                foreach (var c in colsBelowHead)
                {
                    var recv = c.GetComponentInParent<PlayerDamageReceiver>();
                    if (recv != null && recv.enabled)
                    {
                        ApplyDamageAndSync(recv, p + Vector3.down * 0.6f);
                        yield break;
                    }
                }

                // 1b) Sweep for players along motion
                if (deltaMag > 1e-4f)
                {
                    var hits = Physics.SphereCastAll(prevPos, radius, dir, deltaMag, hitMask, QueryTriggerInteraction.Collide);
                    if (verbose) Debug.Log($"[ParabolicProjectile] Player sweep hits={hits.Length}");
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        var recv = hit.collider.GetComponentInParent<PlayerDamageReceiver>();
                        if (recv != null && recv.enabled)
                        {
                            ApplyDamageAndSync(recv, hit.point);
                            yield break;
                        }
                    }
                }

                // 2) Ground below current position
                if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out RaycastHit groundHit, 2f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    SpawnImpactVfx(groundHit.point, groundHit.normal);
                    Destroy(gameObject);
                    yield break;
                }

                // 3) Ground sweep
                if (deltaMag > 1e-4f)
                {
                    if (Physics.SphereCast(prevPos, radius, dir, out RaycastHit sweepHit, deltaMag, groundMask, QueryTriggerInteraction.Ignore))
                    {
                        SpawnImpactVfx(sweepHit.point, sweepHit.normal);
                        Destroy(gameObject);
                        yield break;
                    }
                }

                // Note: proximity fallback removed for clarity once layers are aligned.
            }

            prevPos = p;
            yield return null;
        }

        // If arc ended midair, descend until ground and impact (guarantee floor contact)
        if (authoritative)
        {
            float descendTime = 0f;
            Vector3 p = transform.position;
            while (descendTime < 1.0f)
            {
                p += Vector3.down * Mathf.Max(2f, speed) * Time.deltaTime;
                transform.position = p;

                if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out RaycastHit fallHit, 2f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    SpawnImpactVfx(fallHit.point, fallHit.normal);
                    break;
                }
                descendTime += Time.deltaTime;
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    private void ApplyDamageAndSync(PlayerDamageReceiver recv, Vector3 impactPoint)
    {
        if (verbose) Debug.Log($"[ParabolicProjectile] Authoritative hit -> '{recv.name}' dmg={damage}");

        // Send damage to the owner so the owning client plays local hit/defend logic
        var avatar = recv.GetComponent<Alteruna.Avatar>();
        var sync = avatar != null ? avatar.GetComponent<PlayerHealthSync>() : recv.GetComponentInParent<PlayerHealthSync>();
        if (sync != null)
        {
            try
            {
                // Owner-side effect: unblockable damage (animations, defend state, etc.)
                sync.InvokeRemoteMethod(nameof(PlayerHealthSync.RPC_ApplyUnblockableDamage), damage);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ParabolicProjectile] RPC_ApplyUnblockableDamage error: {ex.Message}");
            }
        }

        // Host applies damage locally to keep authority and then mirrors final HP to peers
        float before = recv.hp;
        recv.ApplyUnblockableDamage(damage);

        if (sync != null)
        {
            try
            {
                // Mirror the resulting health to everyone (including owner UI consistency)
                sync.InvokeRemoteMethod(nameof(PlayerHealthSync.RPC_SetHealth), recv.hp, recv.maxHp);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ParabolicProjectile] RPC_SetHealth error: {ex.Message}");
            }
        }

        if (verbose) Debug.Log($"[ParabolicProjectile] Hit '{recv.name}' dmg={damage:0.##} {before:0.##}->{recv.hp:0.##}");

        SpawnImpactVfx(impactPoint, Vector3.up);
        Destroy(gameObject);
    }

    private void SpawnImpactVfx(Vector3 pos, Vector3 normal)
    {
        if (impactVfx != null)
        {
            var vfx = Instantiate(impactVfx, pos, Quaternion.LookRotation(normal));
            Destroy(vfx, 5f);
        }
    }
}