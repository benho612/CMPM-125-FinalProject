using UnityEngine;
using System.Collections;
using Alteruna;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class GroundDragon : BossController
{
    [Header("Refs")]
    public LayerMask playerMask;
    public GameObject shockRingPrefab;     // ExpandingRing (visual only)
    public GameObject telegraphPrefab;     // flat circle for jump landing

    [Header("Movement")]
    public float moveSpeed = 3.6f;
    public float stopDistance = 3.0f;
    public float turnSpeedDeg = 360f;

    [Header("Scratch (2s CD)")]
    public float scratchDamage = 18f;
    public float scratchRange = 2.6f;
    public float scratchArcDeg = 90f;
    public float scratchWindup = 0.25f;
    public float scratchLungeDist = 1.2f;
    public float scratchCooldown = 2f;

    [Header("Jump (10s CD)")]
    public float jumpDamage = 40f;
    public float jumpRadius = 8f;
    public float jumpWindup = 0.8f;      // telegraph time
    public float jumpTravel = 0.55f;     // “air time?
    public float jumpArcHeight = 3.5f;
    public float jumpCooldown = 10f;
    public float slowMultiplier = 0.6f;
    public float slowDuration = 2.5f;

    [Header("Animator (interrupt)")]
    public float glideBlend = 0.05f;        // crossfade duration into Fly Glide
    public float landBlend = 0.05f;        // crossfade duration into Land
    public float landHold = 0.20f;        // how long to show Land before Idle

    // Animator param hashes
    Animator anim;
    static readonly int MoveSpeedID = Animator.StringToHash("MoveSpeed");
    static readonly int InAirID = Animator.StringToHash("InAir");
    static readonly int AttackTrigID = Animator.StringToHash("Attack");
    static readonly int AttackVarID = Animator.StringToHash("AttackVariant"); // 0 basic, 1 tail
    static readonly int JumpTrigID = Animator.StringToHash("Jump");
    static readonly int LandTrigID = Animator.StringToHash("Land");
    static readonly int GetHitTrigID = Animator.StringToHash("GetHit");
    static readonly int DieTrigID = Animator.StringToHash("Die");
    static readonly int ScreamTrigID = Animator.StringToHash("Scream"); // optional intro roar

    // Animator state hashes used for CrossFade (layer and state names must match your Animator)
    static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
    static readonly int TakeOffHash = Animator.StringToHash("Base Layer.Take Off");
    static readonly int FlyGlideHash = Animator.StringToHash("Base Layer.Fly Glide");
    static readonly int LandHash = Animator.StringToHash("Base Layer.Land");

    CharacterController cc;
    float cdScratch, cdJump;
    bool acting;            // block locomotion while true
    Vector3 lastJumpDest;   // for impact AoE (also used if an AnimEvent calls Impact)

    protected override void Awake()
    {
        base.Awake();
        cc = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        if (anim) anim.applyRootMotion = false; // we drive movement with CharacterController
    }

    void Update()
    {
        // Animator locomotion parameters (both peers)
        var horizVel = cc.velocity; horizVel.y = 0;
        anim.SetFloat(MoveSpeedID, horizVel.magnitude);

        // Host runs behavior
        if (!IsHost || !fightActive) return;

        // cooldowns tick
        cdScratch = Mathf.Max(0f, cdScratch - Time.deltaTime);
        cdJump = Mathf.Max(0f, cdJump - Time.deltaTime);

        if (!acting)
        {
            var t = ClosestPlayer();
            if (t)
            {
                FaceTowards(t, Time.deltaTime);

                Vector3 me = transform.position; me.y = 0;
                Vector3 tp = t.position; tp.y = 0;
                float dist = Vector3.Distance(me, tp);

                if (dist > stopDistance)
                {
                    var dir = (tp - me).normalized;
                    cc.Move(dir * moveSpeed * Time.deltaTime);
                }
            }
        }
    }

    // ---------------- BossController overrides ----------------
    protected override (int attackId, int seed) PickNextAttack()
    {
        // 0 = Scratch, 1 = Jump
        var t = ClosestPlayer();
        float dist = t ? Vector3.Distance(new Vector3(t.position.x, 0, t.position.z),
                                          new Vector3(transform.position.x, 0, transform.position.z)) : 999f;

        // prefer Jump when off CD, otherwise Scratch; if both on CD, controller waits via cadence
        if (cdJump <= 0f) return (1, RandInt(0, int.MaxValue));
        if (cdScratch <= 0f) return (0, RandInt(0, int.MaxValue));
        return (0, RandInt(0, int.MaxValue));
    }

    protected override IEnumerator RunAttackHost(int attackId, int seed)
    {
        rng = new System.Random(seed);
        Debug.Log($"[GroundDragon] RunAttackHost - starting attack {attackId} (seed {seed})");
        if (attackId == 1 && cdJump <= 0f) yield return JumpHost();
        else if (cdScratch <= 0f) yield return ScratchHost();
        else yield return null;
    }

    // ---------------- Helpers ----------------
    Transform ClosestPlayer()
    {
        float best = 999f; Transform bestT = null;
        foreach (var c in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
        {
            if (c.gameObject == gameObject) continue;
            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d < best) { best = d; bestT = c.transform; }
        }
        return bestT;
    }

    void FaceTowards(Transform target, float dt)
    {
        if (!target) return;
        Vector3 a = transform.position; a.y = 0;
        Vector3 b = target.position; b.y = 0;
        var dir = (b - a).normalized; if (dir.sqrMagnitude < 1e-4f) return;
        var to = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, to, turnSpeedDeg * dt);
    }

    // ---------------- Scratch (uses Basic Attack OR Tail Attack clips) ----------------
    IEnumerator ScratchHost()
    {
        acting = true;

        var t = ClosestPlayer();
        // small face-up windup (lets the animation start aligned)
        float w = Mathf.Max(0.1f, scratchWindup / Mathf.Max(0.1f, difficultyScale));
        float e = 0f; while (e < w) { e += Time.deltaTime; if (t) FaceTowards(t, Time.deltaTime); yield return null; }

        // randomly choose variant: 0 = Basic Attack, 1 = Tail Attack
        int variant = rng.Next(0, 2);
        Debug.Log($"[GroundDragon] ScratchHost - chosen variant {variant}");
        anim.SetInteger(AttackVarID, variant);
        Debug.Log("[GroundDragon] ScratchHost - Set AttackVariant integer");

        anim.ResetTrigger(AttackTrigID); // resync if spammed
        Debug.Log("[GroundDragon] ScratchHost - Reset Attack trigger");
        anim.SetTrigger(AttackTrigID);
        Debug.Log("[GroundDragon] ScratchHost - Triggered Attack animation");

        // tiny lunge forward during the swing (not root motion)
        float dur = 0.22f;
        float u = 0; while (u < dur) { u += Time.deltaTime; cc.Move(transform.forward * (scratchLungeDist / dur) * Time.deltaTime); yield return null; }

        // If you don't add an Animation Event, we still apply damage once per scratch:
        DoScratchDamage();

        cdScratch = scratchCooldown;
        acting = false;
    }

    void DoScratchDamage()
    {
        Vector3 center = transform.position + transform.forward * (scratchRange * 0.6f);
        var cols = Physics.OverlapSphere(center, scratchRange, playerMask, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            Vector3 to = (c.transform.position - transform.position); to.y = 0;
            float ang = Vector3.Angle(transform.forward, to);
            if (ang <= scratchArcDeg * 0.5f)
            {
                var dmg = c.GetComponent<PlayerDamageReceiver>();
                if (dmg) dmg.ApplyDamage(scratchDamage * difficultyScale);
            }
        }
    }

    // Public hook if you add an Animation Event on the hit frame:
    public void AnimEvent_ScratchHit()
    {
        Debug.Log("[GroundDragon] AnimEvent_ScratchHit called");
        if (IsHost && fightActive) DoScratchDamage();
    }

    // ---------------- Jump (Take Off -> Fly Glide -> Land) with interrupts ----------------
    IEnumerator JumpHost()
    {
        acting = true;

        var t = ClosestPlayer();
        lastJumpDest = t ? t.position : transform.position + transform.forward * 6f;

        // Try to snap landing position to the ground below the target point.
        // Cast from a point above the intended destination downwards and use the hit point if found.
        {
            const float rayStartYOffset = 5f;
            const float rayDistance = 20f;
            RaycastHit hit;
            Vector3 rayStart = lastJumpDest + Vector3.up * rayStartYOffset;

            // Exclude player layers from the ground raycast so we don't hit the player's collider (landing on their head).
            // Use the inverse of playerMask to raycast against everything except player layers.
            int rayLayerMask = ~playerMask.value;

            Debug.Log($"[GroundDragon] JumpHost - raycast start {rayStart}, down, mask={rayLayerMask:X8}");
            if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDistance, rayLayerMask, QueryTriggerInteraction.Ignore))
            {
                lastJumpDest.y = hit.point.y;
                Debug.Log($"[GroundDragon] JumpHost - ground hit '{hit.collider.name}' at {hit.point}");
            }
            else
            {
                Debug.Log("[GroundDragon] JumpHost - no ground hit, falling back to boss Y");
                // fallback to current boss Y if ground not found
                lastJumpDest.y = transform.position.y;
            }
        }

        // compute final transform target that accounts for CharacterController center and height
        // We need transform.position such that capsule bottom sits at lastJumpDest.y:
        // transformY + cc.center.y - cc.height*0.5f == lastJumpDest.y
        // => transformY = lastJumpDest.y - cc.center.y + cc.height*0.5f
        float centerY = cc != null ? cc.center.y : 0f;
        float halfHeight = cc != null ? cc.height * 0.5f : 0f;
        float finalY = lastJumpDest.y - centerY + halfHeight;
        Vector3 finalLandingPos = new Vector3(lastJumpDest.x, finalY, lastJumpDest.z);
        Debug.Log($"[GroundDragon] JumpHost - cc.center.y={centerY}, cc.height={(cc != null ? cc.height : 0f)}, finalLandingPos={finalLandingPos}");

        // Telegraph disc at landing
        GameObject tele = null;
        if (telegraphPrefab)
        {
            tele = Instantiate(telegraphPrefab, lastJumpDest, telegraphPrefab.transform.rotation);
            tele.transform.localScale = new Vector3(jumpRadius * 2f, 1f, jumpRadius * 2f);
        }

        // Trigger Take Off (animator), but we'll interrupt it after windup
        anim.ResetTrigger(JumpTrigID);
        anim.SetTrigger(JumpTrigID);
        Debug.Log("[GroundDragon] JumpHost - Triggered Jump (Take Off)");

        // windup (roar/scream optional) while facing target
        float w = Mathf.Max(0.2f, jumpWindup / Mathf.Max(0.1f, difficultyScale));
        float e = 0f; while (e < w) { e += Time.deltaTime; if (t) FaceTowards(t, Time.deltaTime); yield return null; }

        // Interrupt Take Off -> go straight to Fly Glide (fast crossfade)
        anim.CrossFade(FlyGlideHash, glideBlend, 0, 0f);
        anim.SetBool(InAirID, true);
        Debug.Log($"[GroundDragon] JumpHost - CrossFaded to Fly Glide (blend={glideBlend}), InAir=true");

        // Parabolic motion (gameplay-driven)
        Vector3 from = transform.position;
        Vector3 to = finalLandingPos;
        float time = Mathf.Max(0.2f, jumpTravel / Mathf.Max(0.1f, difficultyScale));
        float x = 0f;
        while (x < 1f)
        {
            x += Time.deltaTime / time;
            float yOff = jumpArcHeight * 4f * (x - x * x);
            Vector3 pos = Vector3.Lerp(from, to, Mathf.Clamp01(x));
            pos.y += yOff;
            cc.enabled = false; transform.position = pos; cc.enabled = true;
            yield return null;
        }

        // Snap exactly to computed landing position (accounting for controller center)
        cc.enabled = false;
        transform.position = to;
        cc.enabled = true;
        Debug.Log($"[GroundDragon] JumpHost - Snapped to landing position {to}");

        // Interrupt Glide -> Land
        anim.SetBool(InAirID, false);
        anim.CrossFade(LandHash, landBlend, 0, 0f);
        Debug.Log($"[GroundDragon] JumpHost - CrossFaded to Land (blend={landBlend}), InAir=false");

        if (tele) Destroy(tele);

        // Impact AoE + slow
        ApplyJumpAoE(lastJumpDest);

        // Optional: spawn a visual shock ring (no damage here)
        if (shockRingPrefab)
        {
            var ring = Instantiate(shockRingPrefab, lastJumpDest, shockRingPrefab.transform.rotation)
                       .GetComponent<ExpandingRing>();
            if (ring)
            {
                ring.hitMask = playerMask;
                ring.damage = 0f;
                ring.speed *= difficultyScale;
                ring.thickness = 2.5f;
                ring.lifetime = 3.5f;
                ring.authoritative = false;
            }
        }

        // Briefly show Land animation then force Idle so we don't remain stuck in Land state
        yield return new WaitForSeconds(Mathf.Max(0.05f, landHold));
        anim.CrossFade(IdleHash, 0.06f, 0, 0f);
        Debug.Log("[GroundDragon] JumpHost - CrossFaded to Idle after landHold");

        cdJump = jumpCooldown;
        acting = false;
    }

    void ApplyJumpAoE(Vector3 center)
    {
        var cols = Physics.OverlapSphere(center, jumpRadius, playerMask, QueryTriggerInteraction.Ignore);
        foreach (var c in cols)
        {
            var dmg = c.GetComponent<PlayerDamageReceiver>();
            if (dmg) dmg.ApplyDamage(jumpDamage * difficultyScale);

            var slow = c.GetComponent<PlayerSlow>();
            if (slow) slow.ApplySlow(slowMultiplier, slowDuration);
        }
    }

    // Public hook if you add an Animation Event at the Land impact frame:
    public void AnimEvent_JumpImpact()
    {
        Debug.Log("[GroundDragon] AnimEvent_JumpImpact called");
        if (IsHost && fightActive) ApplyJumpAoE(transform.position);
    }

    // Optional hooks (call these from your health/death systems if you want hit/roar anims)
    public void PlayGetHit() { Debug.Log("[GroundDragon] PlayGetHit - Triggering GetHit animation"); anim.SetTrigger(GetHitTrigID); }
    public void PlayScream() { Debug.Log("[GroundDragon] PlayScream - Triggering Scream animation"); anim.SetTrigger(ScreamTrigID); }
    public void PlayDie() { Debug.Log("[GroundDragon] PlayDie - Triggering Die animation"); anim.SetTrigger(DieTrigID); }
}