using UnityEngine;

/// <summary>
/// Warrior-only combat controller (male + female).
/// Put this on Avatar root next to PlayerControl.
/// Active only if Character ID == 1 or 2.
/// </summary>
[RequireComponent(typeof(PlayerControl))]
public class WarriorCombatController : MonoBehaviour
{
    [Header("Character Selection")]
    [Header("Combat")]
    public float attackCooldown = 0.3f;   // delay between clicks
    public float comboResetTime = 1.0f;   // max gap between combo clicks

    [Header("Damage")]
    [Tooltip("Damage dealt for Attack01..Attack04.")]
    public float attack01Damage = 40f;
    public float attack02Damage = 55f;
    public float attack03Damage = 80f;
    public float attack04Damage = 30f;

    [Tooltip("How far in front to check for hit per attack.")]
    public float attack01Range = 2.1f;
    public float attack02Range = 2.2f;
    public float attack03Range = 2.6f;
    public float attack04Range = 2.4f;

    [Tooltip("Hit sphere radius for melee detection.")]
    public float hitRadius = 0.9f;

    [Tooltip("Delay from animation start to hit time (seconds).")]
    public float attack01HitDelay = 0.08f;
    public float attack02HitDelay = 0.10f;
    public float attack03HitDelay = 0.12f;
    public float attack04HitDelay = 0.14f;

    [Header("Slash VFX")]
    public GameObject slash01Prefab;     // front slash
    public GameObject slash02Prefab;     // reversed front slash (or reuse 01)
    public GameObject slash03Prefab;     // heavy / diagonal slash
    public GameObject slash04Prefab;     // AOE slash
    public float slashLifetime = 1.0f;

    Transform _slashSpawnPoint;

    [Header("Shield VFX")]
    [Tooltip("Prefab for the shield visual effect.")]
    public GameObject shieldVfxPrefab;

    [Tooltip("How far in front of the shieldPoint to spawn the VFX.")]
    public float shieldForwardOffset = 0.2f;

    private GameObject _shieldInstance;

    PlayerControl _playerControl;
    Animator _animator;
    Alteruna.Avatar _avatar;

    int _comboStep = 0;
    float _lastAttackClickTime = -999f;
    float _lastAttackTime = -999f;

    bool _isDefending = false;

    public bool IsDefending => _isDefending;

    // Cache the boss reference (auto-found)
    private BossHealth _boss;

    [Header("Slash Audio")]
    public AudioSource slashAudioSource;
    public AudioClip slashAttackClip;
    public float slashVolume = 1.0f;

    void Start()
    {
        _avatar = GetComponent<Alteruna.Avatar>();
        if (_avatar != null && !_avatar.IsMe)
        {
            enabled = false;
            return;
        }

        _playerControl = GetComponent<PlayerControl>();
        _animator = _playerControl.Animator;

        // Auto-find boss once
        _boss = FindObjectOfType<BossHealth>();

        int id = CharacterDatabase.SelectedCharacterID;

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("OHS"))   // catches OHS03, OHS06‥
            {
                _slashSpawnPoint = t;
                break;
            }
        }

        // Only warriors use this script
        if (id != 1 && id != 2)
        {
            enabled = false;
            return;
        }
    }

    void Update()
    {
        if (_avatar != null && !_avatar.IsMe)
            return;

        HandleDefend();
        HandleAttackCombo();
        UpdateDefendMoveAnimations();
    }

    void HandleAttackCombo()
    {
        // Block attacks while defending
        if (_isDefending)
            return;

        if (Input.GetMouseButtonDown(0)) // LMB
        {
            var c = GetComponent<CookingPhaseManager>();
            if (c != null && c.CurrentMiniGame != null)
            {
                c.CurrentMiniGame.OnPlayerPrimaryAction();
                return; 
            }

            // basic cooldown
            if (Time.time - _lastAttackTime < attackCooldown)
                return;

            _lastAttackTime = Time.time;

            float sinceLast = Time.time - _lastAttackClickTime;

            if (sinceLast > comboResetTime)
                _comboStep = 1;
            else
            {
                _comboStep++;
                if (_comboStep > 4)
                    _comboStep = 1;
            }

            _lastAttackClickTime = Time.time;

            PlayAttackAnimation(_comboStep);
            ScheduleAttackHit(_comboStep);

            if (_playerControl != null)
                _playerControl.EnterCombatFromAttack();


        }
    }

    void PlayAttackAnimation(int step)
    {
        if (_animator == null)
            return;

        string stateName;
        switch (step)
        {
            case 1: stateName = "Attack01"; break;
            case 2: stateName = "Attack02"; break;
            case 3: stateName = "Attack03"; break;
            case 4: stateName = "Attack04"; break;
            default: stateName = "Attack01"; break;
        }

        // Blend on base layer
        _animator.CrossFade(stateName, 0.05f, 0);

        SpawnSlashVfx(step);
    }

    void ScheduleAttackHit(int step)
    {
        float delay = step switch
        {
            1 => attack01HitDelay,
            2 => attack02HitDelay,
            3 => attack03HitDelay,
            4 => attack04HitDelay,
            _ => 0.1f
        };

        float range = step switch
        {
            1 => attack01Range,
            2 => attack02Range,
            3 => attack03Range,
            4 => attack04Range,
            _ => 2.0f
        };

        float dmg = step switch
        {
            1 => attack01Damage,
            2 => attack02Damage,
            3 => attack03Damage,
            4 => attack04Damage,
            _ => 30f
        };

        Debug.Log($"[Warrior] Scheduled hit: step={step}, delay={delay:0.00}s, range={range:0.00}, dmg={dmg:0}");

        // Fire-and-forget delayed hit
        StartCoroutine(DelayedHit(delay, range, dmg));
    }

    System.Collections.IEnumerator DelayedHit(float delay, float range, float damage)
    {
        yield return new WaitForSeconds(delay);
        TryDealDamage(range, damage);
    }

    // Simplified: find BossHealth once and test hits against any colliders on that object (including children)
    void TryDealDamage(float range, float damage)
    {
        // Lazy-refresh boss reference if needed
        if (_boss == null)
            _boss = FindObjectOfType<BossHealth>();
        if (_boss == null)
        {
            Debug.LogWarning("[Warrior] No BossHealth found in scene; cannot apply damage.");
            return;
        }

        // Determine forward and origin from player root
        Transform root = _playerControl != null ? _playerControl.transform : transform;

        Vector3 origin = root.position + Vector3.up * 1.0f; // roughly chest height
        // override attack direction during chopping mini-game
        Vector3 forward;

        var chop = GetComponent<IngredientChopController>();
        if (chop != null && chop.IsActive)
            forward = chop.GetAttackDirection();
        else
            forward = root.forward;


        // Center of hit sphere is a bit in front of the player
        Vector3 center = origin + forward * range;

        Debug.DrawLine(origin, center, Color.red, 0.25f);
        Debug.Log($"[Warrior] HitCheck(center={center}, radius={hitRadius:0.00}) vs boss='{_boss.name}'.");

        // Get all colliders under the boss object
        var bossColliders = _boss.GetComponentsInChildren<Collider>();
        if (bossColliders == null || bossColliders.Length == 0)
        {
            Debug.LogWarning("[Warrior] Boss has no colliders; cannot be hit.");
            return;
        }

        // Test the sphere against each boss collider using ClosestPoint
        foreach (var col in bossColliders)
        {
            if (col == null || !col.enabled)
                continue;

            Vector3 closest = col.ClosestPoint(center);
            float dist = Vector3.Distance(closest, center);

            Debug.Log($"[Warrior] Check collider='{col.name}' closest={closest} dist={dist:0.00}");

            // inside TryDealDamage, when a hit is confirmed:
            if (dist <= hitRadius)
            {
                float before = _boss.HP;
                var net = _boss.GetComponent<BossNetSync>();
                if (net != null)
                {
                    Debug.Log($"[Warrior] HIT boss via collider='{col.name}'. Requesting network damage={damage:0}. HP before={before:0}.");
                    net.RequestDamage(damage); // host authoritative
                }
                else
                {
                    Debug.Log($"[Warrior] HIT boss via collider='{col.name}'. Applying local damage={damage:0}. HP before={before:0}.");
                    _boss.Damage(damage); // single-player fallback
                }
                _playerControl?.RegisterCombatAction();
                return;
            }
        }

        Debug.Log("[Warrior] No boss collider intersected by hit sphere.");
    }

    void HandleDefend()
    {
        bool rightDown = Input.GetMouseButton(1);   // hold RMB

        if (rightDown && !_isDefending)
        {
            // Start defending
            _isDefending = true;
            _animator?.SetBool("IsDefending", true);

            ShowShieldVfx();
        }
        else if (!rightDown && _isDefending)
        {
            // Stop defending
            _isDefending = false;
            _animator?.SetBool("IsDefending", false);

            HideShieldVfx();
        }
        else if (rightDown && _isDefending)
        {
            // Still defending – just make sure the VFX is alive
            if (_shieldInstance == null && shieldVfxPrefab != null )
            {
                ShowShieldVfx();
            }
        }
    }

    void UpdateDefendMoveAnimations()
    {
        if (!_isDefending || _animator == null)
            return;

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        _animator.SetFloat("DefendMoveX", x);
        _animator.SetFloat("DefendMoveZ", z);
    }

    void SpawnSlashVfx(int step)
    {
        if (_slashSpawnPoint == null)
            return;

        GameObject prefab = null;
        Vector3 localEulerOffset = Vector3.zero;

        switch (step)
        {
            case 1: prefab = slash01Prefab; localEulerOffset = new Vector3(0f, 0f, 0f); break;
            case 2: prefab = slash02Prefab != null ? slash02Prefab : slash01Prefab; localEulerOffset = new Vector3(0f, 0f, 180f); break;
            case 3: prefab = slash03Prefab; localEulerOffset = new Vector3(90f, 0f, 0f); break;
            case 4: prefab = slash04Prefab; localEulerOffset = Vector3.zero; break;
        }

        if (prefab == null)
            return;

        Transform root = _playerControl != null ? _playerControl.transform : transform;
        Vector3 spawnPos = (step == 4) ? root.position : _slashSpawnPoint.position;
        Quaternion playerYaw = Quaternion.Euler(0f, root.eulerAngles.y + 180f, 0f);
        Quaternion rot = playerYaw * Quaternion.Euler(localEulerOffset);

        GameObject fx = Instantiate(prefab, spawnPos, rot, root);
        Destroy(fx, slashLifetime);

        if (slashAudioSource != null && slashAttackClip != null)
        {
            slashAudioSource.PlayOneShot(slashAttackClip, slashVolume);
        }
    }

    void ShowShieldVfx()
    {
        if (shieldVfxPrefab == null)
            return;

        if (_shieldInstance != null)
        {
            _shieldInstance.SetActive(true);
            return;
        }

        // Spawn directly at player center (no need for shieldPoint)
        Vector3 spawnPos = transform.position;

        // Keep rotation identity so the sphere looks correct
        Quaternion rot = Quaternion.identity;

        // Parent to the player root so it follows the whole character
        _shieldInstance = Instantiate(shieldVfxPrefab, spawnPos, rot, transform);

    }

    void HideShieldVfx()
    {
        if (_shieldInstance == null)
            return;

        _shieldInstance.SetActive(false);
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        var allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            if (t.name == targetName)
                return t;
        }
        return null;
    }
}