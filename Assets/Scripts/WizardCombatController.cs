using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Wizard-only combat controller (fireball + charge spell).
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
    public GameObject chargedFireballPrefab; // charged shot

    [Tooltip("Where fireballs spawn (tip of staff).")]
    public Transform firePoint;

    [Header("Tap vs Hold Settings")]
    [Tooltip("Max hold time to count as a 'tap' (seconds).")]
    public float tapMaxDuration = 0.20f;

    [Tooltip("Time after which we consider the spell 'charged'.")]
    public float chargeThreshold = 0.60f;

    [Tooltip("Cooldown for the small tap fireball.")]
    public float attack01Cooldown = 0.5f;

    [Header("Animation state names")]
    public string stateAttack01 = "Attack01";
    public string stateAttack02Start = "Attack02Start";
    public string stateAttack02Maintain = "Attack02Maintain";
    public string stateBattleIdle = "Battle_Idle";

    [Header("Animator parameter names")]
    public string paramIsChanneling = "IsChanneling";
    public string paramInCombat = "InCombat";

    [Header("Attack Controller")]
    private Camera playerCamera;



    // ---- runtime ----
    private PlayerControl _playerControl;
    private Animator _animator;
    private Alteruna.Avatar _avatar;

    private bool _isHoldingAttack = false;
    private bool _isChanneling = false;
    private float _holdTime = 0f;
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
            {
                // Optional fallback if camera is on a deeper child
                playerCamera = transform.GetComponentInChildren<Camera>(true);
            }

            if (playerCamera == null)
            {
                Debug.LogWarning("WizardCombatController: No camera found in this avatar!");
            }
        }

    }

    void Update()
    {
        // Only control the local player
        if (_avatar != null && !_avatar.IsMe)
            return;

        if (attackAction == null || _animator == null)
            return;

        var attack = attackAction.action;

        // --- press ---
        if (attack.WasPressedThisFrame())
        {
            _isHoldingAttack = true;
            _holdTime = 0f;
        }

        // --- while held ---
        if (_isHoldingAttack)
        {
            _holdTime += Time.deltaTime;

            // when crossing charge threshold, start channeling
            if (!_isChanneling && _holdTime >= chargeThreshold)
                BeginChannel();
        }

        // --- release ---
        if (attack.WasReleasedThisFrame())
        {
            if (_isChanneling)
            {
                // release after charge �� fire big spell
                EndChannelAndFireCharged();
            }
            else
            {
                // short tap �� quick Attack01 (if not on cooldown)
                TryTapFireball();
                var c = GetComponent<CookingPhaseManager>();
                if (c != null && c.CurrentMiniGame != null)
                {
                    c.CurrentMiniGame.OnPlayerPrimaryAction();
                    return; 
                }

            }

            _isHoldingAttack = false;
            _holdTime = 0f;
        }
    }

    // ---------------- TAP SHOT ----------------

    void TryTapFireball()
    {
        float now = Time.time;
        if (now - _lastAttack01Time < attack01Cooldown)
            return; // still on cooldown

        _lastAttack01Time = now;

        // Play quick attack animation
        _animator.CrossFade(stateAttack01, 0.05f, 0);

        // mark in combat
        _animator.SetBool(_hashInCombat, true);

        // Spawn small fireball
        if (smallFireballPrefab != null && firePoint != null)
        {
            Vector3 aimDirection = playerCamera.transform.forward; 
            aimDirection.Normalize();

            // Spawn facing the aim direction
            Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

            GameObject fb = Instantiate(
                smallFireballPrefab,   // or chargedFireballPrefab
                firePoint.position,
                aimRotation
            );


            // make sure it dies after 3s (or whatever is on the prefab script)
            var life = fb.GetComponent<FireballLifetime>();
            if (life == null)
            {
                life = fb.AddComponent<FireballLifetime>();
            }
        }
        if (_playerControl != null)
            _playerControl.EnterCombatFromAttack();

        Debug.Log("TAP FIREBALL FIRED");

    }

    // ---------------- CHANNEL ----------------

    void BeginChannel()
    {
        _isChanneling = true;

        // lock movement while charging
        if (_playerControl != null)
            _playerControl.canMove = false;

        _animator.SetBool(_hashIsChanneling, true);
        _animator.SetBool(_hashInCombat, true);

        // start channel animation
        _animator.CrossFade(stateAttack02Start, 0.05f, 0);
        // Animator graph should transition Attack02Start -> Attack02Maintain
        // while IsChanneling == true.
    }

    void EndChannelAndFireCharged()
    {
        _isChanneling = false;

        if (_playerControl != null)
            _playerControl.canMove = true;

        _animator.SetBool(_hashIsChanneling, false);

        // optional: blend back to Battle_Idle
        if (!string.IsNullOrEmpty(stateBattleIdle))
            _animator.CrossFade(stateBattleIdle, 0.1f, 0);

        // Spawn charged fireball
        if (chargedFireballPrefab != null && firePoint != null)
        {
            GameObject fb = Instantiate(
                chargedFireballPrefab,
                firePoint.position,
                firePoint.rotation
            );

            var life = fb.GetComponent<FireballLifetime>();
            if (life == null)
            {
                life = fb.AddComponent<FireballLifetime>();
            }
        }

        if (_isChanneling)
        {
            _isChanneling = false;
            _animator.SetBool("IsChanneling", false);
        }
        Debug.Log("CHARGED FIREBALL FIRED");

    }
}
