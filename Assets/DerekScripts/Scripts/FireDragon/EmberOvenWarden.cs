// Assets/Bosses/EmberOvenWarden/EmberOvenWarden.cs
using UnityEngine;
using System.Collections;
using Alteruna;

[RequireComponent(typeof(Animator))]
public class EmberOvenWarden : BossController
{
    [Header("Net")]
    public BossNetSync net; // auto-wired in Awake

    [Header("Refs")]
    public Transform muzzle;
    public GameObject fireballPrefab;   // requires ParabolicProjectile
    public GameObject ventPrefab;       // requires GroundPuddle (or your vent script)
    public Transform[] ventPoints;
    public LayerMask playerMask;

    [Header("Fireball Timing")]
    public float aimWindup = 0.30f;       // time to face target before each shot
    public float fireballAnimBlend = 0.05f;
    public float fireballAnimHold = 0.18f; // brief hold so shot anim reads
    public int totalShots = 6;             // will alternate targets if 2 players

    [Header("Vents Timing")]
    public float screamBlend = 0.06f;
    public float screamWindup = 0.35f;
    public float screamOutro = 0.15f;
    public int maxVentsPerCast = 4;

    [Header("Rotation / Sync")]
    public float turnSpeedDegPerSec = 240f;     // rotate boss toward a target
    public float yawSendThresholdDeg = 2f;      // send when host yaw changes more than this
    public float clientYawLerpSpeed = 12f;      // client smoothing toward host yaw

    [Header("Vent Point Auto-Setup")]
    public bool autoFindVentPoints = true;
    public Transform ventParent;                       // drag "VentLocations" here (best)
    public string ventPointsParentName = "VentLocations"; // fallback by name
    public int limitVentPoints = 32;

    // Animator hashes (only the ones we use now)
    static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
    static readonly int FireballHash = Animator.StringToHash("Base Layer.Fireball Shoot");
    static readonly int ScreamHash = Animator.StringToHash("Base Layer.Scream");
    static readonly int MoveSpeedID = Animator.StringToHash("MoveSpeed");

    // Attack IDs (no HeatWave anymore)
    const int ThermalFan = 0;  // fireballs
    const int FlareVents = 1;  // vents

    Animator _anim;

    // yaw replication
    float _lastSentYaw;
    float _targetYaw;

    protected override void Awake()
    {
        base.Awake();

        _anim = GetComponent<Animator>();
        if (_anim) _anim.applyRootMotion = false;

        if (!net) net = GetComponent<BossNetSync>();
        if (net) net.warden = this;

        AutoPopulateVentPoints();
        _targetYaw = transform.eulerAngles.y;

        Debug.Log($"[Warden] Awake. ventPoints={(ventPoints != null ? ventPoints.Length : 0)}");
    }

    private void OnValidate()
    {
        if (autoFindVentPoints)
            AutoPopulateVentPoints();
    }

    void Update()
    {
        if (_anim) _anim.SetFloat(MoveSpeedID, 0f);

        if (IsHost && fightActive)
        {
            var t = ClosestPlayer();
            if (t) FaceTowards(t, Time.deltaTime);

            float yaw = transform.eulerAngles.y;
            if (Mathf.Abs(Mathf.DeltaAngle(_lastSentYaw, yaw)) > yawSendThresholdDeg)
            {
                net?.BroadcastYaw(yaw);
                _lastSentYaw = yaw;
            }
        }
        else
        {
            var e = transform.eulerAngles;
            e.y = Mathf.LerpAngle(e.y, _targetYaw, Time.deltaTime * clientYawLerpSpeed);
            transform.eulerAngles = e;
        }
    }

    // called by BossNetSync yaw RPC
    public void LocalSetYaw(float yaw)
    {
        _targetYaw = yaw;
        if (!IsHost && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yaw)) > 45f)
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, yaw, transform.eulerAngles.z);
    }

    // ---------- BossController hooks ----------
    protected override (int attackId, int seed) PickNextAttack()
    {
        int id = RandInt(0, 2); // 0 or 1
        int seed = RandInt(0, int.MaxValue);
        return (id, seed);
    }

    protected override IEnumerator RunAttackHost(int attackId, int seed)
    {
        rng = new System.Random(seed);
        switch (attackId)
        {
            case ThermalFan: yield return ThermalFanHost(); break;
            case FlareVents: yield return FlareVentsHost(); break;
        }
    }

    // ================= FIREBALLS =================
    IEnumerator ThermalFanHost()
    {
        var targets = GetTwoPlayersNearest();
        if (targets.Length == 0 || muzzle == null || fireballPrefab == null || net == null)
            yield break;

        float perShotWindup = Mathf.Max(0.08f, aimWindup / Mathf.Max(0.1f, difficultyScale));
        float perShotDelay = 0.45f / Mathf.Max(0.1f, difficultyScale);

        for (int i = 0; i < totalShots; i++)
        {
            Transform tgt = (targets.Length > 1) ? targets[i % 2] : targets[0];
            if (tgt == null) break;

            yield return AimAt(tgt, perShotWindup);

            _anim.CrossFade(FireballHash, fireballAnimBlend, 0, 0f);

            Vector3 start = muzzle.position;
            Vector3 dest = tgt.position; dest.y = start.y;

            net.BroadcastSpawnFireball(start, dest, difficultyScale);

            yield return new WaitForSeconds(fireballAnimHold);
            yield return new WaitForSeconds(Mathf.Max(0f, perShotDelay - fireballAnimHold));
        }

        _anim.CrossFade(IdleHash, 0.06f);
    }

    public void LocalSpawnFireball(Vector3 start, Vector3 dest, float speedScale)
    {
        if (!fireballPrefab) return;

        var go = Instantiate(fireballPrefab);
        go.transform.position = start;
        Debug.Log($"[Warden] LocalSpawnFireball start={start} dest={dest} scale={speedScale:0.00}");

        var proj = go.GetComponent<ParabolicProjectile>();
        if (proj != null)
        {
            proj.hitMask = playerMask;
            proj.speed *= Mathf.Max(0.01f, speedScale);
            proj.Launch(start, dest, hostAuth: false);
        }
    }

    // ================= VENTS =================
    IEnumerator FlareVentsHost()
    {
        _anim.CrossFade(ScreamHash, screamBlend, 0, 0f);
        yield return new WaitForSeconds(screamWindup);

        if ((ventPoints == null || ventPoints.Length == 0) && autoFindVentPoints)
            AutoPopulateVentPoints();

        if (ventPrefab == null || net == null)
        {
            _anim.CrossFade(IdleHash, 0.06f);
            yield break;
        }

        if (ventPoints == null || ventPoints.Length == 0)
        {
            // fallback ring if none found
            Debug.LogWarning("[Warden] No ventPoints; using fallback ring.");
            int count = Mathf.Max(1, maxVentsPerCast);
            float radius = 6f;
            for (int i = 0; i < count; i++)
            {
                float ang = (360f * i / count) * Mathf.Deg2Rad;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * radius;
                p.y = transform.position.y;
                net.BroadcastSpawnVent(p, difficultyScale);
                yield return new WaitForSeconds(0.12f);
            }
        }
        else
        {
            int vents = Mathf.Min(ventPoints.Length, Mathf.Max(1, maxVentsPerCast));
            for (int i = 0; i < vents; i++)
            {
                var t = ventPoints[RandInt(0, ventPoints.Length)];
                if (t == null) continue;

                var pos = t.position;
                if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 20f))
                    pos = hit.point;

                Debug.Log($"[Warden] Casting vent at '{t.name}' -> {pos}");
                net.BroadcastSpawnVent(pos, difficultyScale);
                yield return new WaitForSeconds(0.12f);
            }
        }

        yield return new WaitForSeconds(screamOutro);
        _anim.CrossFade(IdleHash, 0.06f);
    }

    public void LocalSpawnVent(Vector3 pos, float scale)
    {
        if (!ventPrefab) return;

        var v = Instantiate(ventPrefab, pos, ventPrefab.transform.rotation);
        Debug.Log($"[Warden] LocalSpawnVent pos={pos} scale={scale:0.00}");

        var gp = v.GetComponent<GroundPuddle>(); // or your vent script
        if (gp != null)
        {
            gp.authoritative = false;                 // spawned via RPC on each client
            gp.dps *= Mathf.Max(0.01f, scale);
            gp.radius = 2f;
            gp.lifetime = 6f;
            gp.hitMask = playerMask;
        }
    }

    // ---------- helpers ----------
    void AutoPopulateVentPoints()
    {
        if (!autoFindVentPoints) return;
        if (ventPoints != null && ventPoints.Length > 0) return;

        var found = new System.Collections.Generic.List<Transform>();

        // 1) Explicit parent reference
        Transform parentT = ventParent != null ? ventParent : null;

        // 2) Fallback by name: "VentLocations"
        if (parentT == null && !string.IsNullOrEmpty(ventPointsParentName))
        {
            var go = GameObject.Find(ventPointsParentName);
            if (go != null) parentT = go.transform;
        }

        // 3) If we have a parent, collect its children
        if (parentT != null)
        {
            foreach (Transform child in parentT)
                if (child != null) found.Add(child);
        }


        // Cap and assign
        if (found.Count > limitVentPoints)
            found.RemoveRange(limitVentPoints, found.Count - limitVentPoints);

        ventPoints = found.ToArray();
        Debug.Log($"[Warden] AutoPopulateVentPoints -> {ventPoints.Length} points found.");
    }

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
        float acc = 0f;
        while (acc < seconds && t != null)
        {
            acc += Time.deltaTime;
            FaceTowards(t, Time.deltaTime);
            yield return null;
        }
    }
}
