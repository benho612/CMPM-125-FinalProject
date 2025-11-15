// Assets/Bosses/_Shared/ParabolicProjectile.cs
using UnityEngine;
using System.Collections;

public class ParabolicProjectile : MonoBehaviour
{
    public float speed = 12f;
    public float arcHeight = 3f;
    public float radius = 0.6f;
    public float damage = 12f;
    public float lifetime = 8f;
    public LayerMask hitMask;
    public bool authoritative = true; // host true, clients false (visual only)

    Vector3 start, target;
    float t;

    public void Launch(Vector3 from, Vector3 to, bool hostAuth)
    {
        start = from; target = to; authoritative = hostAuth;
        t = 0f;
        StartCoroutine(Fly());
    }

    IEnumerator Fly()
    {
        float dist = Vector3.Distance(start, target);
        float dur = Mathf.Max(0.01f, dist / speed);
        float life = 0f;
        while (t < 1f && life < lifetime)
        {
            t += Time.deltaTime / dur;
            life += Time.deltaTime;
            Vector3 p = Vector3.Lerp(start, target, t);
            p.y += arcHeight * 4f * (t - t * t); // parabola
            transform.position = p;

            if (authoritative)
            {
                // simple sphere hit
                var cols = Physics.OverlapSphere(p, radius, hitMask, QueryTriggerInteraction.Ignore);
                foreach (var c in cols)
                {
                    var dmg = c.GetComponent<PlayerDamageReceiver>();
                    if (dmg) { dmg.ApplyDamage(damage); Destroy(gameObject); yield break; }
                }
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
