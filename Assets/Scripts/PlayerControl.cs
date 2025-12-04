using UnityEngine;

/// <summary>
/// Shared movement + camera controller for all characters.
/// Put this on the Avatar root. Wizard/Warrior controllers sit next to it.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerControl : MonoBehaviour
{
    [Header("Shared Combat Settings")]
    public float combatRange = 10f;
    public string enemyTag = "Enemy";
    public float combatExitDelay = 5f;

    bool _inCombat = false;
    float _lastCombatTime = -999f;

    public bool InCombat => _inCombat;

    [Header("Defend movement")]
    [Tooltip("Speed multiplier while defending (0.4 = 40% speed).")]
    [Range(0f, 1f)]
    public float defendSpeedMultiplier = 0.4f;
    private WarriorCombatController _warriorCombat;

    [Header("Movement")]
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;

    [Header("Camera")]
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;
    public float cameraDistance = 4.0f;
    public float cameraHeight = 2.0f;
    public float cameraSmooth = 10.0f;
    public float cameraYOffset = 1.0f;

    [Header("Jump VFX")]
    public GameObject doubleJumpVfxPrefab;
    public Transform doubleJumpSpawnPoint;

    public float doubleJumpVfxLifetime = 1.0f;

    [Header("Jump Audio")]
    public AudioSource jumpSource;
    public AudioClip jumpSound;
    public float jumpVolume = 1.0f;

    [Header("rolling")]
    bool _isRolling = false;
    float _rollEndTime = 0f;
    Vector3 _rollDirection;
    public float rollSpeed = 12f;
    public bool IsRolling => _isRolling;
    [Header("Roll Invulnerability")]
    [Tooltip("How long the player is invulnerable when a roll starts.")]
    public float rollInvulnDuration = 0.4f;

    [Tooltip("These colliders will be disabled while invulnerable (DO NOT include the CharacterController).")]
    public Collider[] invulnerableColliders;

    bool _invulnerableActive = false;
    float _invulnEndTime = 0f;

    [Header("Footstep Audio")]
    public AudioSource footstepSource;
    public AudioClip walkingLoop;
    public float walkPitch = 1.0f;
    public float runPitch = 1.25f;
    public float pitchLerpSpeed = 8f;

    [HideInInspector] public bool canMove = true;

    // Exposed so combat scripts can reuse them
    [HideInInspector] public Animator Animator;
    [HideInInspector] public CharacterController CharacterController;

    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }

    Alteruna.Avatar _avatar;

    bool _isWizard = false;

    Camera _playerCamera;
    Vector3 _moveDirection;
    float _rotationX;
    int _jumpCount = 0;

    [Header("Aim / First-Person Camera")]
    [Tooltip("Camera distance while aiming (close to 0 for FPS).")]
    public float aimCameraDistance = 0.1f;

    [Tooltip("Camera height while aiming (eye level).")]
    public float aimCameraHeight = 1.7f;

    [Tooltip("How far to offset the camera to the right when aiming.")]
    public float aimCameraSideOffset = 10f;

    float _defaultCameraDistance;
    float _defaultCameraHeight;
    float _defaultCameraYOffset;

    bool _isInAimMode = false;

    void Awake()
    {
        CharacterController = GetComponent<CharacterController>();
        _avatar = GetComponent<Alteruna.Avatar>();
    }

    // Call this whenever an attack is performed (warrior or wizard)
    public void EnterCombatFromAttack()
    {
        _inCombat = true;
        _lastCombatTime = Time.time;

        if (Animator != null)
            Animator.SetBool("InCombat", true);
    }

    void Start()
    {
        // Only local player controls movement / camera
        if (_avatar != null && !_avatar.IsMe)
        {
            enabled = false;
            return;
        }

        Animator = GetComponentInChildren<Animator>();
        _playerCamera = GetComponentInChildren<Camera>();
        _warriorCombat = GetComponent<WarriorCombatController>();


        _isWizard = (CharacterDatabase.SelectedCharacterID == 0);

        if (_playerCamera == null)
        {
            Debug.LogError("PlayerControl: No Camera found under Avatar.", this);
            return;
        }

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
        }
        footstepSource.playOnAwake = false;
        footstepSource.loop = true;          // footsteps loop
        footstepSource.spatialBlend = 0f;    // 0 = 2D so it's always audible

        if (_isWizard)
        {
            footstepSource.volume = 0.35f;
            footstepSource.pitch = 0.2f;

        }
            
        

        // --- JUMP AUDIO SOURCE ---
        if (jumpSource == null)
        {
            jumpSource = gameObject.AddComponent<AudioSource>();
        }
        jumpSource.playOnAwake = false;
        jumpSource.loop = false;             // jump is one-shot
        jumpSource.spatialBlend = 0f;        // also 2D for now

        // Initial camera placement
        _defaultCameraDistance = cameraDistance;
        _defaultCameraHeight = cameraHeight;
        _defaultCameraYOffset = cameraYOffset;

        Vector3 startPos =
            transform.position
            - transform.forward * cameraDistance
            + Vector3.up * cameraHeight;

        _playerCamera.transform.position = startPos;
        _playerCamera.transform.LookAt(transform.position + Vector3.up * cameraYOffset);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (_avatar != null && !_avatar.IsMe)
            return;

        if (CharacterController == null)
            return;


        UpdateCombatState();


        HandleMovement();
        HandleCamera();
        UpdateAnimatorLocomotion();
    }

    void HandleMovement()
    {
        // --- ROLL OVERRIDE ---
        if (_isRolling)
        {
            // end roll
            if (Time.time >= _rollEndTime)
            {
                _isRolling = false;
                canMove = true;
                _moveDirection = Vector3.zero;
            }
            else
            {
                float _verticalVelocity = _moveDirection.y;

                if (!CharacterController.isGrounded)
                    _verticalVelocity -= gravity * Time.deltaTime;

                _moveDirection = _rollDirection * rollSpeed;
                _moveDirection.y = _verticalVelocity;

                CharacterController.Move(_moveDirection * Time.deltaTime);
            }

            if (_invulnerableActive && Time.time >= _invulnEndTime)
            {
                SetRollInvulnerable(false);
            }

            // while rolling we skip normal movement
            if (_isRolling)
                return;
        }

        // Holding Left Shift = run
        bool runKey = !_isWizard && Input.GetKey(KeyCode.LeftShift);

        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        // Raw input
        float inputZ = canMove ? Input.GetAxis("Vertical") : 0f;  // W/S
        float inputX = canMove ? Input.GetAxis("Horizontal") : 0f;  // A/D

        // --- FIX INTERCARDINAL (DIAGONAL) SPEED ---
        // Build a 2D vector and normalize if needed so diagonals aren't faster
        Vector2 inputVec = new Vector2(inputX, inputZ);
        if (inputVec.sqrMagnitude > 1f)
            inputVec = inputVec.normalized;

        // Base speed (walk / run)

        float speed = runKey ? runningSpeed : walkingSpeed;

        // --- DEFEND SLOWDOWN (WARRIOR ONLY) ---
        // If we have a WarriorCombatController and it says we are defending,
        // multiply speed by defendSpeedMultiplier (e.g. 0.4 = 40% of normal)
        if (_warriorCombat != null && _warriorCombat.IsDefending)
        {
            speed *= defendSpeedMultiplier;
        }

        float curSpeedZ = speed * inputVec.y;
        float curSpeedX = speed * inputVec.x;

        float verticalVelocity = _moveDirection.y;

        // Combine into world-space move direction
        _moveDirection = forward * curSpeedZ + right * curSpeedX;

        // Jump / double jump
        if (CharacterController.isGrounded)
            _jumpCount = 0;

        if (canMove && Input.GetButtonDown("Jump"))
        {
            if (CharacterController.isGrounded)
            {
                _jumpCount = 1;
                verticalVelocity = jumpSpeed;
                Animator?.SetTrigger("Jump");
                PlayJumpSound();
            }
            else if (_jumpCount == 1)
            {
                _jumpCount = 2;
                verticalVelocity = jumpSpeed;
                Animator?.SetTrigger("DoubleJump");
                SpawnDoubleJumpVfx();
                PlayJumpSound();
            }
        }

        if (!CharacterController.isGrounded)
            verticalVelocity -= gravity * Time.deltaTime;

        _moveDirection.y = verticalVelocity;

        CharacterController.Move(_moveDirection * Time.deltaTime);
    }

    void HandleCamera()
    {
        if (_playerCamera == null)
            return;

        if (!canMove && !_isInAimMode)
            return;

        float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

        // Horizontal rotation affects the body
        transform.rotation *= Quaternion.Euler(0f, mouseX, 0f);

        // Vertical rotation is camera-only
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);

        Quaternion camRot = Quaternion.Euler(_rotationX, transform.eulerAngles.y, 0f);

        Vector3 shoulderOffset = Vector3.zero;
        if (_isInAimMode)
        {
            // offset to the player's right side in camera space
            shoulderOffset = camRot * Vector3.right * aimCameraSideOffset;
        }

        Vector3 desiredPos =
            transform.position
            - camRot * Vector3.forward * cameraDistance
            + shoulderOffset
            + Vector3.up * cameraHeight;

        _playerCamera.transform.position =
            Vector3.Lerp(_playerCamera.transform.position, desiredPos, cameraSmooth * Time.deltaTime);

        Vector3 lookTarget = transform.position + Vector3.up * cameraYOffset;
        _playerCamera.transform.rotation = Quaternion.Lerp(
            _playerCamera.transform.rotation,
            Quaternion.LookRotation(lookTarget - _playerCamera.transform.position),
            cameraSmooth * Time.deltaTime
        );
    }

    void UpdateAnimatorLocomotion()
    {
        if (Animator == null)
            return;

        Vector3 horizontalVel = CharacterController.velocity;
        horizontalVel.y = 0f;
        float speed = horizontalVel.magnitude;

        IsMoving = speed > 0.1f;
        bool runKey = !_isWizard && Input.GetKey(KeyCode.LeftShift);
        IsRunning = IsMoving && runKey;

        Animator.SetBool("IsMoving", IsMoving);
        Animator.SetBool("IsSprinting", IsRunning);
        Animator.SetBool("IsGrounded", CharacterController.isGrounded);
        Animator.SetBool("InCombat", _inCombat);

        UpdateFootstepAudio();
    }

    void UpdateFootstepAudio()
    {
        if (footstepSource == null || walkingLoop == null)
            return;

        // We want footsteps only when:
        // - moving horizontally
        // - grounded
        // - allowed to move
        // - not currently rolling (using your _isRolling flag)
        bool shouldPlay =
            IsMoving &&
            CharacterController.isGrounded &&
            canMove &&
            !_isRolling;   // this is your private roll bool

        if (shouldPlay)
        {
            if (!footstepSource.isPlaying)
            {
                footstepSource.clip = walkingLoop;
                footstepSource.pitch = walkPitch;
                footstepSource.Play();
            }

            // Blend pitch between walk and run
            float targetPitch = IsRunning ? runPitch : walkPitch;
            footstepSource.pitch = Mathf.Lerp(
                footstepSource.pitch,
                targetPitch,
                Time.deltaTime * pitchLerpSpeed
            );
        }
        else
        {
            if (footstepSource.isPlaying)
            {
                footstepSource.Stop();
            }
        }
    }

    void SpawnDoubleJumpVfx()
    {
        if (doubleJumpVfxPrefab == null) return;

        Vector3 pos = doubleJumpSpawnPoint != null
            ? doubleJumpSpawnPoint.position
            : transform.position;

        // You asked for rotation X = 90
        Quaternion rot = Quaternion.Euler(90f, 0f, 0f);

        var fx = Instantiate(doubleJumpVfxPrefab, pos, rot);
        fx.transform.SetParent(transform, worldPositionStays: true);
        Destroy(fx, doubleJumpVfxLifetime);
    }

    public void RegisterCombatAction()
    {
        _inCombat = true;
        _lastCombatTime = Time.time;
    }

    bool IsEnemyInRange()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (var e in enemies)
        {
            if (Vector3.Distance(transform.position, e.transform.position) <= combatRange)
                return true;
        }
        return false;
    }

    void UpdateCombatState()
    {
        bool enemyNear = IsEnemyInRange();

        if (enemyNear)
        {
            _inCombat = true;
            _lastCombatTime = Time.time;
        }
        // If no enemy is near, leave combat only after X seconds of no combat activity
        if (!enemyNear && _inCombat && Time.time - _lastCombatTime > combatExitDelay)
        {
            _inCombat = false;
        }

        if (Animator != null)
        {
            Animator.SetBool("InCombat", _inCombat);
        }

    }

    // Called by WizardCombatController to start rolling
    public void StartRoll(Vector3 worldDirection, float duration)
    {
        if (worldDirection.sqrMagnitude < 0.001f)
            worldDirection = transform.forward;

        _rollDirection = worldDirection.normalized;
        _isRolling = true;
        _rollEndTime = Time.time + duration;
        canMove = false;

        // start invulnerability window
        if (rollInvulnDuration > 0f)
        {
            SetRollInvulnerable(true);
            _invulnEndTime = Time.time + rollInvulnDuration;
        }
    }

    void PlayJumpSound()
    {
        if (jumpSource == null || jumpSound == null)
        {
            Debug.LogWarning("Jump SFX missing: jumpSource or jumpSound not assigned.");
            return;
        }

        jumpSource.PlayOneShot(jumpSound, jumpVolume);
    }

    void SetRollInvulnerable(bool value)
    {
        _invulnerableActive = value;

        if (invulnerableColliders == null)
            return;

        foreach (var col in invulnerableColliders)
        {
            if (col == null) continue;
            col.enabled = !value;  // disable while invulnerable
        }
    }

    public void EnterAimMode()
    {
        _isInAimMode = true;

        cameraDistance = aimCameraDistance;
        cameraHeight = aimCameraHeight;
        cameraYOffset = 1.7f;
    }

    public void ExitAimMode()
    {
        _isInAimMode = false;

        cameraDistance = _defaultCameraDistance;
        cameraHeight = _defaultCameraHeight;
        cameraYOffset = _defaultCameraYOffset;
    }
}
