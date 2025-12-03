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

    [Tooltip("Where fireballs spawn (tip of staff).")]
    public Transform firePoint;

    [Header("Tap vs Hold Settings")]
    public float tapMaxDuration = 0.20f;
    public float chargeThreshold = 0.60f;
    public float attack01Cooldown = 0.5f;

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
    }

    void Update()
    {
        // Only control the local player
        if (_avatar != null && !_avatar.IsMe)
            return;

        if (_animator == null)
            return;

        // 1) Handle roll first
        HandleRoll();

        // If rolling, skip everything else
        if (_playerControl != null && _playerControl.IsRolling)
            return;

        // 2) Handle RMB aim/meteor charge
        HandleAimAndMeteor();

        // If we are in aiming mode, don't allow normal attacks
        if (_isAiming || _isChanneling)
            return;

        // 3) Handle normal tap attack via LMB (InputAction)
        if (attackAction == null)
            return;

        var attack = attackAction.action;

        // --- press ---
        if (attack.WasPressedThisFrame())
        {
            TryTapFireball();
        }

    }


    // ---------------- ROLL ----------------
    void HandleRoll()
    {
        if (_playerControl == null)
            return;

        if (_isChanneling || _isAiming)
            return;

        // If currently rolling, PlayerControl will finish it
        if (_playerControl.IsRolling)
            return;

        // Shift triggers roll **only** for wizard (warrior still uses it for sprint from PlayerControl)
        if (!Input.GetKeyDown(KeyCode.LeftShift))
            return;

        // Decide direction based on input
        float h = Input.GetAxisRaw("Horizontal"); // A/D
        float v = Input.GetAxisRaw("Vertical");   // W/S

        // Local move direction (relative to character forward)
        Vector3 localDir = new Vector3(h, 0f, v);

        if (localDir.sqrMagnitude < 0.01f)
            localDir = Vector3.forward; // no input → roll forward

        // World-space direction
        Vector3 worldDir = transform.TransformDirection(localDir.normalized);

        // Choose animation state
        string rollState;
        if (Mathf.Abs(h) > Mathf.Abs(v))
        {
            rollState = h > 0f ? stateRollRight : stateRollLeft;
        }
        else
        {
            if (v < 0f)
                rollState = stateRollBackward;
            else
                rollState = stateRollForward;
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

        if (smallFireballPrefab != null && firePoint != null && playerCamera != null)
        {
            Vector3 aimDirection = playerCamera.transform.forward;
            aimDirection.Normalize();

            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            GameObject fb = Instantiate(
                smallFireballPrefab,
                firePoint.position,
                aimRotation
            );
        }

        if (_playerControl != null)
            _playerControl.EnterCombatFromAttack();
    }

    // ---------------- CHANNEL ----------------
    void HandleAimAndMeteor()
    {
        if (_playerControl == null || playerCamera == null)
            return;

        bool rmbDown = Input.GetMouseButtonDown(1);
        bool rmbHeld = Input.GetMouseButton(1);
        bool rmbUp = Input.GetMouseButtonUp(1);

        // enter aiming
        if (rmbDown && !_isAiming && !_isChanneling)
        {
            _isAiming = true;
            BeginMeteorAim();          // play Attack02 and lock movement
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

            // safety: if RMB is no longer held, treat as release
            if (!rmbHeld)
                rmbUp = true;
        }

        // release: cast the spell
        if (rmbUp && _isAiming)
        {
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
            EndMeteorAim();            // back to idle, unlock movement
            _isAiming = false;
        }
    }

    void BeginMeteorAim()
    {
        _isChanneling = true;

        if (_playerControl != null)
            _playerControl.canMove = false;   // no movement while aiming

        _animator.SetBool(_hashIsChanneling, true);
        _animator.SetBool(_hashInCombat, true);

        // start Attack02Start; your animator should go into Attack02Maintain while IsChanneling == true
        _animator.CrossFade(stateAttack02Start, 0.05f, 0);
    }

    void EndMeteorAim()
    {
        _isChanneling = false;

        if (_playerControl != null)
            _playerControl.canMove = true;

        _animator.SetBool(_hashIsChanneling, false);

        if (!string.IsNullOrEmpty(stateBattleIdle))
            _animator.CrossFade(stateBattleIdle, 0.1f, 0);
    }

    void UpdateMeteorAimIndicator()
    {
        if (_currentIndicator == null)
            return;

        // ray from center of screen
        Ray ray = playerCamera.ScreenPointToRay(
            new Vector3(Screen.width / 2f, Screen.height / 2f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, groundMask))
        {
            _currentIndicator.SetActive(true);
            _currentIndicator.transform.position = hit.point;

            // nicely align with ground normal
            _currentIndicator.transform.rotation = Quaternion.FromToRotation(
                Vector3.up, hit.normal
            );
        }
        else
        {
            _currentIndicator.SetActive(false);
        }
    }


}
