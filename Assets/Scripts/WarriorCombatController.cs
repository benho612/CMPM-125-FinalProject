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
    [Tooltip("Point on the character where the shield effect should appear (e.g. an empty under the shield).")]
    public Transform shieldPoint;

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

        int id = CharacterDatabase.SelectedCharacterID;

        if (shieldPoint == null)
        {
            // Try male first (Shield08), then female (Shield05)
            shieldPoint = FindChildByName(transform, "Shield08");

            if (shieldPoint == null)
                shieldPoint = FindChildByName(transform, "Shield05");
        }

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

        // Fire-and-forget delayed hit
        StartCoroutine(DelayedHit(delay, range, dmg));
    }

    System.Collections.IEnumerator DelayedHit(float delay, float range, float damage)
    {
        yield return new WaitForSeconds(delay);
        TryDealDamage(range, damage);
    }

    void TryDealDamage(float range, float damage)
    {
        // Determine forward and origin from player root
        Transform root = _playerControl != null ? _playerControl.transform : transform;

        Vector3 origin = root.position + Vector3.up * 1.0f; // roughly chest height
        Vector3 forward = root.forward;

        // Center of hit sphere is a bit in front of the player
        Vector3 center = origin + forward * range;

        // Collect all colliders in the hit sphere
        Collider[] hits = Physics.OverlapSphere(center, hitRadius, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return;

        string enemyTag = _playerControl != null ? _playerControl.enemyTag : "Enemy";

        // Damage first BossHealth found with the right tag
        foreach (var col in hits)
        {
            if (!col || (enemyTag.Length > 0 && !col.CompareTag(enemyTag)))
                continue;

            if (col.TryGetComponent<BossHealth>(out var boss))
            {
                boss.Damage(damage);
                // Register combat action so PlayerControl can keep you in combat
                _playerControl?.RegisterCombatAction();
                break; // stop at first boss hit
            }
        }
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
            if (_shieldInstance == null && shieldVfxPrefab != null && shieldPoint != null)
            {
                ShowShieldVfx();
            }
        }
    }

    /// <summary>
    /// Optional: if your animator uses DefendMoveX / DefendMoveZ for
    /// defend strafing animations, update them here.
    /// </summary>
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
            case 1: // Attack01 – normal slash
                prefab = slash01Prefab;
                localEulerOffset = new Vector3(0f, 0f, 0f);
                break;
            case 2: // Attack02 – reversed slash
                prefab = slash02Prefab != null ? slash02Prefab : slash01Prefab;
                localEulerOffset = new Vector3(0f, 0f, 180f);
                break;
            case 3: // Attack03 – maybe vertical / diagonal
                prefab = slash03Prefab;
                localEulerOffset = new Vector3(90f, 0f, 0f);
                break;
            case 4: // Attack04 – AOE
                prefab = slash04Prefab;
                localEulerOffset = Vector3.zero;
                break;
        }

        if (prefab == null)
            return;

        // 1) Decide parent = player root, NOT the sword
        Transform root = _playerControl != null ? _playerControl.transform : transform;

        // 2) Spawn position:
        //    - for normal slashes: at sword position
        //    - for AOE: at player center
        Vector3 spawnPos = (step == 4)
            ? root.position
            : _slashSpawnPoint.position;

        // 3) Base rotation: only use player yaw, ignore sword animation
        Quaternion playerYaw = Quaternion.Euler(0f, root.eulerAngles.y + 180f, 0f);

        // 4) Apply simple offset (vertical / horizontal / flipped)
        Quaternion rot = playerYaw * Quaternion.Euler(localEulerOffset);

        // 5) Instantiate as child of the player root so it moves with the player
        GameObject fx = Instantiate(prefab, spawnPos, rot, root);

        // 6) Ensure it dies after a bit (if prefab doesn’t already self-destruct)
        Destroy(fx, slashLifetime);
    }

    void ShowShieldVfx()
    {
        if (shieldVfxPrefab == null || shieldPoint == null)
            return;

        // If we already spawned one, just reactivate it
        if (_shieldInstance != null)
        {
            _shieldInstance.SetActive(true);
            return;
        }

        // Spawn slightly in front of the shieldPoint
        Vector3 spawnPos = shieldPoint.position + shieldPoint.forward * shieldForwardOffset;

        // Make the effect face outward from the shield
        Quaternion rot = Quaternion.LookRotation(shieldPoint.forward, Vector3.up);

        _shieldInstance = Instantiate(shieldVfxPrefab, spawnPos, rot);

        // Parent to the shieldPoint so it moves with the shield
        _shieldInstance.transform.SetParent(shieldPoint, worldPositionStays: true);
    }

    void HideShieldVfx()
    {
        if (_shieldInstance == null)
            return;

        // Don’t destroy – just disable so we can reuse it instantly next defend
        _shieldInstance.SetActive(false);
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        // search in all children, including inactive ones
        var allChildren = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            if (t.name == targetName)
                return t;
        }
        return null;
    }
}