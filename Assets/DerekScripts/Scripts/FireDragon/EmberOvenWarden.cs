using UnityEngine;
using System.Collections;
using Alteruna;

[RequireComponent(typeof(Animator), typeof(BossNetSync))]
public class EmberOvenWarden : BossController
{
    [Header("References")]
    public Transform muzzle;
    public GameObject fireballPrefab;
    public GameObject ventPrefab;
    public Transform[] ventPoints;
    public LayerMask playerMask = ~0;

    [Header("Fireball Attack")]
    public float aimWindup = 0.30f;
    public float fireballAnimBlend = 0.05f;
    public float fireballAnimHold = 0.18f;
    public int totalShots = 6;

    [Header("Vents Attack")]
    public float screamBlend = 0.06f;
    public float screamWindup = 0.35f;
    public float screamOutro = 0.15f;
    public int maxVentsPerCast = 4;

    [Header("Rotation")]
    public float turnSpeedDegPerSec = 240f;
    public float yawSendThresholdDeg = 2f;
    public float clientYawLerpSpeed = 12f;

    [Header("Auto Setup")]
    public bool autoFindVentPoints = true;
    public Transform ventParent;
    public string ventPointsParentName = "VentLocations";
    public int limitVentPoints = 32;

    // Animator Hashes
    private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
    private static readonly int FireballHash = Animator.StringToHash("Base Layer.Fireball Shoot");
    private static readonly int ScreamHash = Animator.StringToHash("Base Layer.Scream");
    private static readonly int MoveSpeedID = Animator.StringToHash("MoveSpeed");

    public Animator _anim;
    private float _lastSentYaw;
    private float _lastYawSendTime;
    private const float YawSendInterval = 0.05f;
    private const float YawDeltaThreshold = 0.8f;

    protected override void Awake()
    {
        base.Awake();

        if (multiplayer == null)
            multiplayer = FindObjectOfType<Multiplayer>();

        _anim = GetComponent<Animator>();
        if (_anim) _anim.applyRootMotion = false;

        if (net == null) net = GetComponent<BossNetSync>();
        if (net != null) net.warden = this;

        AutoPopulateVentPoints();
        _lastSentYaw = transform.eulerAngles.y;

        Debug.Log($"[EmberOvenWarden] Awake on {(IsHost ? "HOST" : "CLIENT")} | obj='{gameObject.name}' | hasNetSync={(GetComponent<BossNetSync>() != null)}");
    }

    private void Update()
    {
        if (_anim) _anim.SetFloat(MoveSpeedID, 0f);

        if (IsHost && fightActive)
        {
            var target = ClosestPlayer();
            if (target) FaceTowards(target, Time.deltaTime);

            float currentYaw = transform.eulerAngles.y;
            float deltaYaw = Mathf.Abs(Mathf.DeltaAngle(currentYaw, _lastSentYaw));

            if (deltaYaw >= YawDeltaThreshold && Time.time - _lastYawSendTime >= YawSendInterval)
            {
                if (net != null) net.BroadcastYaw(currentYaw);
                _lastSentYaw = currentYaw;
                _lastYawSendTime = Time.time;
            }
        }
        else if (!IsHost && net != null)
        {
            float current = transform.eulerAngles.y;
            float targetYaw = net.LastReceivedYaw;
            float lerped = Mathf.LerpAngle(current, targetYaw, clientYawLerpSpeed * Time.deltaTime);
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, lerped, transform.eulerAngles.z);
        }
    }

    public void LocalSetYaw(float yaw)
    {
        if (IsHost) return;
        float current = transform.eulerAngles.y;
        if (Mathf.Abs(Mathf.DeltaAngle(current, yaw)) > 30f)
            transform.eulerAngles = new Vector3(transform.eulerAngles.x, yaw, transform.eulerAngles.z);
    }

    protected override (int attackId, int seed) PickNextAttack()
    {
        int id = RandInt(0, 2);
        int seed = RandInt(0, int.MaxValue);
        return (id, seed);
    }

    protected override IEnumerator RunAttackHost(int attackId, int seed)
    {
        rng = new System.Random(seed);
        return attackId switch
        {
            0 => ThermalFanHost(),
            1 => FlareVentsHost(),
            _ => null
        };
    }

    IEnumerator ThermalFanHost()
    {
        var targets = GetTwoPlayersNearest();
        if (targets.Length == 0 || muzzle == null || fireballPrefab == null) yield break;

        for (int i = 0; i < totalShots; i++)
        {
            var target = targets.Length > 1 ? targets[i % 2] : targets[0];
            if (!target) continue;

            yield return AimAt(target, aimWindup);

            if (net != null) net.BroadcastCrossFade(FireballHash, fireballAnimBlend);
            yield return new WaitForSeconds(fireballAnimHold);

            Vector3 start = muzzle.position;
            Vector3 dest = target.position;
            dest.y = start.y;

            if (net != null) net.BroadcastSpawnFireball(start, dest, difficultyScale);
            yield return new WaitForSeconds(0.35f);
        }

        if (net != null) net.BroadcastCrossFade(IdleHash, 0.1f);
    }

    public void LocalSpawnFireball(Vector3 start, Vector3 dest, float speedScale)
    {
        if (!fireballPrefab) { Debug.LogWarning("[Warden] fireballPrefab missing!"); return; }

        var go = Instantiate(fireballPrefab, start, Quaternion.identity);
        var proj = go.GetComponent<ParabolicProjectile>();
        if (proj)
        {
            proj.hitMask = playerMask;
            proj.speed *= speedScale;
            proj.Launch(start, dest, hostAuth: false);
        }
    }

    IEnumerator FlareVentsHost()
    {
        if (net != null) net.BroadcastCrossFade(ScreamHash, screamBlend);
        yield return new WaitForSeconds(screamWindup);

        if (ventPrefab == null) yield break;

        int count = Mathf.Min(maxVentsPerCast, ventPoints?.Length ?? 8);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = ventPoints != null && ventPoints.Length > 0
                ? ventPoints[RandInt(0, ventPoints.Length)].position
                : transform.position + Quaternion.Euler(0, i * (360f / count), 0) * Vector3.forward * 6f;

            pos.y = transform.position.y;

            if (net != null) net.BroadcastSpawnVent(pos, difficultyScale);
            yield return new WaitForSeconds(0.12f);
        }

        yield return new WaitForSeconds(screamOutro);
        if (net != null) net.BroadcastCrossFade(IdleHash, 0.1f);
    }

    public void LocalSpawnVent(Vector3 pos, float scale)
    {
        if (!ventPrefab) return;

        var vent = Instantiate(ventPrefab, pos, ventPrefab.transform.rotation);
        var puddle = vent.GetComponent<GroundPuddle>();
        if (puddle)
        {
            puddle.authoritative = false;
            puddle.dps *= scale;
            puddle.radius = 2f;
            puddle.lifetime = 6f;
            puddle.hitMask = playerMask;
        }
    }

    // Helpers
    void AutoPopulateVentPoints()
    {
        if (!autoFindVentPoints || (ventPoints != null && ventPoints.Length > 0)) return;

        Transform parent = ventParent;
        if (parent == null)
        {
            var go = GameObject.Find(ventPointsParentName);
            if (go) parent = go.transform;
        }

        if (parent != null)
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in parent)
                if (child.gameObject.activeInHierarchy)
                    list.Add(child);

            if (list.Count > limitVentPoints)
                list.RemoveRange(limitVentPoints, list.Count - limitVentPoints);

            ventPoints = list.ToArray();
        }

        Debug.Log($"[Warden] Auto-found {ventPoints?.Length ?? 0} vent points.");
    }

    Transform ClosestPlayer() => GetTwoPlayersNearest().Length > 0 ? GetTwoPlayersNearest()[0] : null;

    Transform[] GetTwoPlayersNearest()
    {
        var players = FindObjectsOfType<CharacterController>();
        if (players.Length == 0) return new Transform[0];
        if (players.Length == 1) return new Transform[] { players[0].transform };

        System.Array.Sort(players, (a, b) =>
            Vector3.Distance(transform.position, a.transform.position)
                .CompareTo(Vector3.Distance(transform.position, b.transform.position)));

        return new Transform[] { players[0].transform, players.Length > 1 ? players[1].transform : players[0].transform };
    }

    void FaceTowards(Transform target, float dt)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.1f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeedDegPerSec * dt);
    }

    IEnumerator AimAt(Transform t, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && t)
        {
            FaceTowards(t, Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
