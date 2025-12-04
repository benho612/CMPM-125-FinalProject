using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Wizard-only combat controller (fireball + charge spell + roll).
/// Attach to Avatar root next to PlayerControl.
/// Only active when CharacterDatabase.SelectedCharacterID == 0 (wizard).
/// </summary>
[RequireComponent(typeof(PlayerControl))]
public class WizardCombatController : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Attack action (same one you use for warrior LMB).")]
    public InputActionReference attackAction;

    [Header("Fireball Prefabs")]
    public GameObject smallFireballPrefab;   // tap shot
    public Transform firePoint;

    [Header("Tap vs Hold Settings")]
    public float tapMaxDuration = 0.20f;
    public float chargeThreshold = 0.60f;
    public float attack01Cooldown = 0.5f;

    [Header("Damage")]
    [Tooltip("Damage dealt by the wizard's tap fireball.")]
    public float tapFireballDamage = 50f;

    [Tooltip("Maximum hitscan distance for tap fireball.")]
    public float tapFireballRange = 20f;

    [Tooltip("Radius used to validate a boss hit near the ray impact point.")]
    public float bossHitRadius = 0.9f;

    [Header("Animation state names")]
    public string stateAttack01 = "Attack01";
    public string stateAttack02Start = "Attack02Start";
    public string stateAttack02Maintain = "Attack02Maintain";
    public string stateBattleIdle = "Battle_Idle";

    [Header("Roll Settings")]
    public float rollLockDuration = 0.7f;
    public string stateRollForward = "RollForward";
    public string stateRollBackward = "RollBackward";
    public string stateRollLeft = "RollLeft";
    public string stateRollRight = "RollRight";

    [Header("Animator parameter names")]
    public string paramIsChanneling = "IsChanneling";
    public string paramInCombat = "InCombat";

    private Camera playerCamera;

    [Header("Meteor AOE")]
    [Tooltip("Prefab for the final Meteor AOE effect.")]
    public GameObject meteorAoePrefab;

    [Tooltip("Prefab for the ground indicator while aiming.")]
    public GameObject meteorIndicatorPrefab;

    [Tooltip("Layers the ground raycast can hit (e.g., Ground).")]
    public LayerMask groundMask = ~0;   // everything by default

    [Tooltip("Max aiming distance for the meteor.")]
    public float maxAimDistance = 30f;

    [Header("Audio")]
    [Tooltip("One-shot SFX (tap fireball, meteor release).")]
    public AudioSource oneShotSource;

    public AudioClip leftClickClip;      // tap attack
    public AudioClip meteorReleaseClip;  // AOE release
    public float oneShotVolume = 1f;

    [Header("Channel Audio")]
    [Tooltip("Looping source for channel/aiming SFX.")]
    public AudioSource channelSource;
    public AudioClip channelLoopClip;
    public float channelVolume = 1f;

    // ---- runtime ----
    private PlayerControl _playerControl;
    private Animator _animator;
    private Alteruna.Avatar _avatar;

    private bool _isAiming = false;
    private GameObject _currentIndicator;
    private bool _isChanneling = false;
    private float _lastAttack01Time = -999f;

    private int _hashIsChanneling;
    private int _hashInCombat;

    // Cached boss reference for quick damage routing
    private BossHealth _boss;

    // NEW: Optional override for attack direction (FPS during chopping)
    private Transform _overrideAttackCamera;
    public void OverrideAttackCamera(Transform cam)
    {
        _overrideAttackCamera = cam;
    }


    void Awake()
    {
        _playerControl = GetComponent<PlayerControl>();
        _avatar = GetComponent<Alteruna.Avatar>();
        _animator = GetComponentInChildren<Animator>();

        _hashIsChanneling = Animator.StringToHash(paramIsChanneling);
        _hashInCombat = Animator.StringToHash(paramInCombat);
    }

    void OnEnable()
    {
        if (attackAction != null)
            attackAction.action.Enable();
    }

    void OnDisable()
    {
        if (attackAction != null)
            attackAction.action.Disable();
    }

    void Start()
    {
        // Only wizard should run this script
        if (CharacterDatabase.SelectedCharacterID != 0)
        {
            enabled = false;
            return;
        }

        // AUTO-FIND CAMERA FOR THIS AVATAR
        if (_avatar != null && _avatar.IsMe)
        {
            playerCamera = GetComponentInChildren<Camera>(true);

            if (playerCamera == null)
                playerCamera = transform.GetComponentInChildren<Camera>(true);

            if (playerCamera == null)
                Debug.LogWarning("WizardCombatController: No camera found in this avatar!");
        }

        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f; // 2D, always audible
        }

        if (channelSource == null)
        {
            channelSource = gameObject.AddComponent<AudioSource>();
            channelSource.playOnAwake = false;
            channelSource.loop = true;       // this one will loop
            channelSource.spatialBlend = 0f;
        }

        // Cache boss reference
        _boss = FindObjectOfType<BossHealth>();
    }

    void Update()
    {
        // Only control the local player
        if (_avatar != null && !_avatar.IsMe)
            return;

        if (_animator == null)
            return;

        HandleRoll();

        if (_playerControl != null && _playerControl.IsRolling)
            return;

        HandleAimAndMeteor();

        if (_isAiming || _isChanneling)
            return;

        if (attackAction == null)
            return;

        var attack = attackAction.action;

        if (attack.WasPressedThisFrame())
        {
            TryTapFireball();
            var c = GetComponent<CookingPhaseManager>();
            if (c != null && c.CurrentMiniGame != null)
            {
                c.CurrentMiniGame.OnPlayerPrimaryAction();
                return; 
            }
        }
    }

    // ---------------- ROLL ----------------
    void HandleRoll()
    {
        if (_playerControl == null)
            return;

        if (_isChanneling || _isAiming)
            return;

        if (_playerControl.IsRolling)
            return;

        if (!Input.GetKeyDown(KeyCode.LeftShift))
            return;

        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        Vector3 localDir = new Vector3(h, 0f, v);

        if (localDir.sqrMagnitude < 0.01f)
            localDir = Vector3.forward;

        Vector3 worldDir = transform.TransformDirection(localDir.normalized);

        string rollState;
        if (Mathf.Abs(h) > Mathf.Abs(v))
        {
            rollState = h > 0f ? stateRollRight : stateRollLeft;
        }
        else
        {
            rollState = v < 0f ? stateRollBackward : stateRollForward;
        }

        _playerControl.StartRoll(worldDir, rollLockDuration);
        _animator.CrossFade(rollState, 0.05f, 0);
    }

    // ---------------- TAP SHOT ----------------
    void TryTapFireball()
    {
        float now = Time.time;
        if (now - _lastAttack01Time < attack01Cooldown)
            return;

        _lastAttack01Time = now;

        _animator.CrossFade(stateAttack01, 0.05f, 0);
        _animator.SetBool(_hashInCombat, true);

        // Spawn visual projectile if assigned
        if (smallFireballPrefab != null && firePoint != null && playerCamera != null)
        {
            Transform cam = (_overrideAttackCamera != null) ? _overrideAttackCamera : playerCamera.transform;
            Vector3 aimDirection = cam.forward.normalized;

            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);
            Instantiate(smallFireballPrefab, firePoint.position, aimRotation);
        }

        // Hitscan damage application to the boss (host-authoritative via BossNetSync)
        ApplyTapFireballDamage();

        if (_playerControl != null)
            _playerControl.EnterCombatFromAttack();

        PlayLeftClickSfx();
    }

    void ApplyTapFireballDamage()
    {
        // Refresh boss reference if needed
        if (_boss == null)
            _boss = FindObjectOfType<BossHealth>();
        if (_boss == null || playerCamera == null)
            return;

        // Ray forward from camera
        Transform cam = (_overrideAttackCamera != null) ? _overrideAttackCamera : playerCamera.transform;

        Ray ray = new Ray(cam.position, cam.forward);
        Vector3 impactPoint = cam.position + cam.forward * tapFireballRange;


        // Try precise raycast against boss colliders first
        var bossColliders = _boss.GetComponentsInChildren<Collider>();
        bool hitBoss = false;
        foreach (var col in bossColliders)
        {
            if (col == null || !col.enabled) continue;

            if (col.Raycast(ray, out RaycastHit hitInfo, tapFireballRange))
            {
                impactPoint = hitInfo.point;
                hitBoss = true;
                break;
            }
        }

        // If ray didn’t hit a boss collider, validate proximity using a sphere near the expected impact
        if (!hitBoss)
        {
            foreach (var col in bossColliders)
            {
                if (col == null || !col.enabled) continue;

                Vector3 closest = col.ClosestPoint(impactPoint);
                float dist = Vector3.Distance(closest, impactPoint);
                if (dist <= bossHitRadius)
                {
                    impactPoint = closest;
                    hitBoss = true;
                    break;
                }
            }
        }

        if (!hitBoss)
            return;

        // Route damage through BossNetSync (host applies and replicates); fallback to local damage if missing
        var net = _boss.GetComponent<BossNetSync>();
        if (net != null)
        {
            net.RequestDamage(tapFireballDamage);
        }
        else
        {
            _boss.Damage(tapFireballDamage);
        }

        // Keep combat active
        _playerControl?.RegisterCombatAction();
    }

    // ---------------- CHANNEL ----------------
    void HandleAimAndMeteor()
    {
        if (_playerControl == null || playerCamera == null)
            return;

        bool rmbDown = Input.GetMouseButtonDown(1);
        bool rmbHeld = Input.GetMouseButton(1);
        bool rmbUp = Input.GetMouseButtonUp(1);

        if (rmbDown && !_isAiming && !_isChanneling)
        {
            _isAiming = true;
            BeginMeteorAim();
            _playerControl.EnterAimMode();

            if (meteorIndicatorPrefab != null)
            {
                _currentIndicator = Instantiate(
                    meteorIndicatorPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }
        }

        if (_isAiming)
        {
            UpdateMeteorAimIndicator();

            if (!rmbHeld)
                rmbUp = true;
        }

        if (rmbUp && _isAiming)
        {
            // Spawn meteor AOE visual; damage can be added similarly via BossHealth if needed
            if (_currentIndicator != null && meteorAoePrefab != null)
            {
                Instantiate(
                    meteorAoePrefab,
                    _currentIndicator.transform.position,
                    _currentIndicator.transform.rotation
                );
            }

            if (_currentIndicator != null)
                Destroy(_currentIndicator);

            _playerControl.ExitAimMode();
            EndMeteorAim();
            _isAiming = false;

            _playerControl?.EnterCombatFromAttack();
            _playerControl?.RegisterCombatAction();
            PlayMeteorReleaseSfx();
        }
    }

    void BeginMeteorAim()
    {
        _isChanneling = true;

        if (_playerControl != null)
            _playerControl.canMove = false;

        _animator.SetBool(_hashIsChanneling, true);
        _animator.SetBool(_hashInCombat, true);

        _animator.CrossFade(stateAttack02Start, 0.05f, 0);

        StartChannelSfx();
    }

    void EndMeteorAim()
    {
        _isChanneling = false;

        if (_playerControl != null)
            _playerControl.canMove = true;

        _animator.SetBool(_hashIsChanneling, false);

        if (!string.IsNullOrEmpty(stateBattleIdle))
            _animator.CrossFade(stateBattleIdle, 0.1f, 0);

        StopChannelSfx();
    }

    void UpdateMeteorAimIndicator()
    {
        if (_currentIndicator == null)
            return;

        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, groundMask))
        {
            _currentIndicator.SetActive(true);
            _currentIndicator.transform.position = hit.point;
            _currentIndicator.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }
        else
        {
            _currentIndicator.SetActive(false);
        }
    }

    // AUDIO METHODS
    void PlayLeftClickSfx()
    {
        if (oneShotSource != null && leftClickClip != null)
            oneShotSource.PlayOneShot(leftClickClip, oneShotVolume);
    }

    void PlayMeteorReleaseSfx()
    {
        if (oneShotSource != null && meteorReleaseClip != null)
            oneShotSource.PlayOneShot(meteorReleaseClip, oneShotVolume);
    }

    void StartChannelSfx()
    {
        if (channelSource == null || channelLoopClip == null)
            return;

        if (!channelSource.isPlaying)
        {
            channelSource.clip = channelLoopClip;
            channelSource.volume = channelVolume;
            channelSource.loop = true;
            channelSource.Play();
        }
    }

    void StopChannelSfx()
    {
        if (channelSource != null && channelSource.isPlaying)
            channelSource.Stop();
    }
}
