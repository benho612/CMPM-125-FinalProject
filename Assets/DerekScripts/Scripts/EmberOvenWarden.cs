using UnityEngine;
using System.Collections;
using Alteruna;

public class EmberOvenWarden : BossController
{
    [Header("Refs")]
    public Transform muzzle;
    public GameObject fireballPrefab;   // ParabolicProjectile
    public GameObject ringPrefab;       // ExpandingRing
    public GameObject ventPrefab;       // GroundPuddle
    public Transform[] ventPoints;
    public LayerMask playerMask;

    [Header("Aiming")]
    public float turnSpeedDegPerSec = 240f;   // how fast the boss turns toward target
    public float aimWindup = 0.35f;           // brief face-target windup per shot

    // Attack IDs
    const int ThermalFan = 0;
    const int HeatWave = 1;
    const int FlareVents = 2;

    // ===== Helpers =====
    Transform ClosestPlayer()
    {
        float best = 999f; Transform bestT = null;
        foreach (var cc in FindObjectsOfType<CharacterController>())
        {
            float d = Vector3.Distance(transform.position, cc.transform.position);
            if (d < best) { best = d; bestT = cc.transform; }
        }
        return bestT;
    }

    Transform[] GetTwoPlayersNearest()
    {
        var ccs = FindObjectsOfType<CharacterController>();
        if (ccs == null || ccs.Length == 0) return new Transform[0];

        Transform first = null, second = null;
        float d1 = float.MaxValue, d2 = float.MaxValue;

        foreach (var cc in ccs)
        {
            float d = Vector3.Distance(transform.position, cc.transform.position);
            if (d < d1) { d2 = d1; second = first; d1 = d; first = cc.transform; }
            else if (d < d2) { d2 = d; second = cc.transform; }
        }

        if (second == null) return new Transform[] { first };
        return new Transform[] { first, second };
    }

    Vector3 FlatDirTo(Transform t)
    {
        Vector3 a = transform.position; a.y = 0;
        Vector3 b = t.position; b.y = 0;
        var d = (b - a).normalized;
        return d.sqrMagnitude < 0.001f ? transform.forward : d;
    }

    void FaceTowards(Transform target, float dt)
    {
        if (!target) return;
        var dir = FlatDirTo(target);
        var targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDegPerSec * dt);
    }

    IEnumerator AimAt(Transform t, float seconds)
    {
        float tAccum = 0f;
        while (tAccum < seconds && t != null)
        {
            tAccum += Time.deltaTime;
            FaceTowards(t, Time.deltaTime);
            yield return null;
        }
    }

    void Update()
    {
        // Host keeps gently facing the nearest player during the fight
        if (IsHost && fightActive)
        {
            var t = ClosestPlayer();
            if (t) FaceTowards(t, Time.deltaTime);
        }
    }

    // ===== BossController overrides =====
    protected override (int attackId, int seed) PickNextAttack()
    {
        int id = RandInt(0, 3);
        int seed = RandInt(0, int.MaxValue);
        return (id, seed);
    }

    protected override IEnumerator RunAttackHost(int attackId, int seed)
    {
        rng = new System.Random(seed);
        switch (attackId)
        {
            case ThermalFan: yield return ThermalFanHost(); break;
            case HeatWave: yield return HeatWaveHost(); break;
            case FlareVents: yield return FlareVentsHost(); break;
        }
    }

    // ========== NEW FIREBALL SYSTEM ==========
    // Fires 6 shots total, alternating targets: P1, P2, P1, P2, P1, P2.
    // Each shot locks to the player's CURRENT position at fire time.
    IEnumerator ThermalFanHost()
    {
        var targets = GetTwoPlayersNearest();
        if (targets.Length == 0) yield break;

        int totalShots = 6;
        float perShotWindup = Mathf.Max(0.1f, aimWindup / Mathf.Max(0.1f, difficultyScale));
        float perShotDelay = 0.45f / Mathf.Max(0.1f, difficultyScale);

        for (int i = 0; i < totalShots; i++)
        {
            Transform tgt = (targets.Length > 1) ? targets[i % 2] : targets[0];
            if (tgt == null) break;

            // brief windup to turn toward current target
            yield return AimAt(tgt, perShotWindup);

            // sample target's current position (forces real dodges)
            Vector3 start = muzzle.position;
            Vector3 dest = tgt.position;
            dest.y = start.y; // keep arc clean

            // fire one projectile
            var go = Instantiate(fireballPrefab);
            go.transform.position = start;

            var proj = go.GetComponent<ParabolicProjectile>();
            proj.hitMask = playerMask;
            proj.speed *= difficultyScale;
            proj.Launch(start, dest, hostAuth: true);

            yield return new WaitForSeconds(perShotDelay);
        }
    }

    IEnumerator HeatWaveHost()
    {
        var pos = transform.position; pos.y = transform.position.y;
        var go = Instantiate(ringPrefab, pos, ringPrefab.transform.rotation);
        var ring = go.GetComponent<ExpandingRing>();
        ring.authoritative = true;
        ring.speed *= difficultyScale;
        ring.hitMask = playerMask;
        ring.thickness = 2.5f;
        ring.lifetime = 4.5f;
        yield return new WaitForSeconds(2.5f / difficultyScale);
    }

    IEnumerator FlareVentsHost()
    {
        int vents = Mathf.Min(ventPoints.Length, 4);
        for (int i = 0; i < vents; i++)
        {
            var t = ventPoints[RandInt(0, ventPoints.Length)];
            var v = Instantiate(ventPrefab, t.position, ventPrefab.transform.rotation);

            var gp = v.GetComponent<GroundPuddle>();
            gp.authoritative = true;
            gp.dps *= difficultyScale;
            gp.radius = 2f;
            gp.lifetime = 6f;
        }
        yield return new WaitForSeconds(2f);
    }
}
