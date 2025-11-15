// Assets/Bosses/EmberOvenWarden/EmberOvenWarden.cs
using UnityEngine;
using System.Collections;
using Alteruna;

[RequireComponent(typeof(Animator))]
public class EmberOvenWarden : BossController
{
    [Header("Refs")]
    public Transform muzzle;
    public GameObject fireballPrefab;   // ParabolicProjectile
    public GameObject ringPrefab;       // ExpandingRing
    public GameObject ventPrefab;       // GroundPuddle
    public Transform[] ventPoints;
    public LayerMask playerMask;

    [Header("Aiming / Motion")]
    public float turnSpeedDegPerSec = 240f;
    public float aimWindup = 0.30f;         // pre-shot face time

    [Header("Animation: Fireball")]
    public float fireballAnimBlend = 0.05f; // fade into Fireball Shoot
    public float fireballAnimHold = 0.18f; // how long to display the shot anim each projectile

    [Header("Animation: Heat Wave (TakeOff?Glide?Land)")]
    public float takeoffBlend = 0.05f;
    public float glideBlend = 0.05f;
    public float landBlend = 0.05f;
    public float takeoffHold = 0.25f;      // show takeoff briefly
    public float glideHold = 0.25f;      // short glide (purely visual)
    public float landHold = 0.20f;      // show land, spawn ring on enter

    [Header("Animation: Vents (Scream)")]
    public float screamBlend = 0.06f;  // fade into Scream
    public float screamWindup = 0.35f;  // hold Scream before spawning vents
    public float screamOutro = 0.15f;  // brief hold after last vent, before Idle


    // Animator hashes (adjust strings to match your controller if needed)
    static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
    static readonly int WalkHash = Animator.StringToHash("Base Layer.Walk");
    static readonly int RunHash = Animator.StringToHash("Base Layer.Run");
    static readonly int FireballHash = Animator.StringToHash("Base Layer.Fireball Shoot");

    static readonly int TakeOffHash = Animator.StringToHash("Base Layer.Take Off");
    static readonly int FlyGlideHash = Animator.StringToHash("Base Layer.Fly Glide");
    static readonly int LandHash = Animator.StringToHash("Base Layer.Land");
    static readonly int MoveSpeedID = Animator.StringToHash("MoveSpeed");
    static readonly int InAirID = Animator.StringToHash("InAir"); // optional, if you wired it
    static readonly int ScreamHash = Animator.StringToHash("Scream");

    // Optional: if you still use these for Vents
    static readonly int BasicAttackHash = Animator.StringToHash("Base Layer.Basic Attack");
    static readonly int TailAttackHash = Animator.StringToHash("Base Layer.Tail Attack");

    Animator anim;

    // Attack IDs
    const int ThermalFan = 0; // fireballs
    const int HeatWave = 1; // ring on landing (takeoff?land)
    const int FlareVents = 2; // ground vents

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        if (anim) anim.applyRootMotion = false;
    }

    void Update()
    {
        // If the Warden doesn't translate, this stays at 0 ? Idle.
        anim.SetFloat(MoveSpeedID, 0f);

        if (IsHost && fightActive)
        {
            var t = ClosestPlayer();
            if (t) FaceTowards(t, Time.deltaTime);
        }
    }

    // ---------- helpers ----------
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
        float acc = 0f;
        while (acc < seconds && t != null)
        {
            acc += Time.deltaTime;
            FaceTowards(t, Time.deltaTime);
            yield return null;
        }
    }

    // ---------- BossController overrides ----------
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

    // ================= FIREBALLS =================
    // Now plays the Fireball Shoot animation for EACH projectile.
    IEnumerator ThermalFanHost()
    {
        var targets = GetTwoPlayersNearest();
        if (targets.Length == 0) yield break;

        int totalShots = 6;
        float perShotWindup = Mathf.Max(0.08f, aimWindup / Mathf.Max(0.1f, difficultyScale));
        float perShotDelay = 0.45f / Mathf.Max(0.1f, difficultyScale);

        for (int i = 0; i < totalShots; i++)
        {
            Transform tgt = (targets.Length > 1) ? targets[i % 2] : targets[0];
            if (tgt == null) break;

            // quick turn toward target
            yield return AimAt(tgt, perShotWindup);

            // *** play the Fireball animation for this shot ***
            anim.CrossFade(FireballHash, fireballAnimBlend, 0, 0f);

            // sample live target position at fire time
            Vector3 start = muzzle.position;
            Vector3 dest = tgt.position; dest.y = start.y;

            // spawn projectile
            var go = Object.Instantiate(fireballPrefab);
            go.transform.position = start;
            var proj = go.GetComponent<ParabolicProjectile>();
            proj.hitMask = playerMask;
            proj.speed *= difficultyScale;
            proj.Launch(start, dest, hostAuth: true);

            // small hold so the anim reads; then wait the rest of the cadence
            yield return new WaitForSeconds(fireballAnimHold);
            // (no need to force Idle; next crossfade restarts the shot anim)
            yield return new WaitForSeconds(Mathf.Max(0f, perShotDelay - fireballAnimHold));
        }

        // clean return
        anim.CrossFade(IdleHash, 0.06f);
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

    // ================= HEAT WAVE =================
    // Visual: Take Off ? (brief) Fly Glide ? Land; fire ring on Land enter.
    IEnumerator HeatWaveHost()
    {
        // Take Off (short)  ? we interrupt into Glide
        anim.CrossFade(TakeOffHash, takeoffBlend, 0, 0f);
        yield return new WaitForSeconds(takeoffHold);

        // Glide (brief, purely visual)
        anim.SetBool(InAirID, true);   // only needed if you wired transitions on this bool
        anim.CrossFade(FlyGlideHash, glideBlend, 0, 0f);
        yield return new WaitForSeconds(glideHold);

        // Land ? spawn ring immediately on entering Land
        anim.SetBool(InAirID, false);
        anim.CrossFade(LandHash, landBlend, 0, 0f);

        // *** spawn the heat ring now (landing impact) ***
        var pos = transform.position; pos.y = transform.position.y;
        var go = Object.Instantiate(ringPrefab, pos, ringPrefab.transform.rotation);
        var ring = go.GetComponent<ExpandingRing>();
        ring.authoritative = true;
        ring.speed *= difficultyScale;
        ring.hitMask = playerMask;
        ring.thickness = 2.5f;
        ring.lifetime = 4.5f;

        // brief display, then back to Idle
        yield return new WaitForSeconds(landHold);
        anim.CrossFade(IdleHash, 0.06f);
    }

    // ================= FLARE VENTS =================
    IEnumerator FlareVentsHost()
    {
        // Play Scream while vents erupt
        anim.CrossFade(ScreamHash, screamBlend, 0, 0f);

        // small windup so players read the attack
        yield return new WaitForSeconds(screamWindup);

        int vents = Mathf.Min(ventPoints.Length, 4);   // or whatever count you want
        for (int i = 0; i < vents; i++)
        {
            var t = ventPoints[RandInt(0, ventPoints.Length)];
            var v = Instantiate(ventPrefab, t.position, ventPrefab.transform.rotation);

            var gp = v.GetComponent<GroundPuddle>();
            gp.authoritative = true;
            gp.dps *= difficultyScale;
            gp.radius = 2f;
            gp.lifetime = 6f;

            // stagger spawns slightly for drama
            yield return new WaitForSeconds(0.12f);
        }

        // short outro then return to Idle
        yield return new WaitForSeconds(screamOutro);
        anim.CrossFade(IdleHash, 0.06f);
    }

}
