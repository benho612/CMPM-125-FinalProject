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
    public Transform doubleJumpSpawnPoint;   // optional ¡V e.g. pelvis / feet

    public float doubleJumpVfxLifetime = 1.5f;

    [HideInInspector] public bool canMove = true;

    // Exposed so combat scripts can reuse them
    [HideInInspector] public Animator Animator;
    [HideInInspector] public CharacterController CharacterController;

    public bool IsMoving { get; private set; }
    public bool IsRunning { get; private set; }

    Alteruna.Avatar _avatar;

    Camera _playerCamera;
    Vector3 _moveDirection;
    float _rotationX;
    int _jumpCount = 0;

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

        if (_playerCamera == null)
        {
            Debug.LogError("PlayerControl: No Camera found under Avatar.", this);
            return;
        }

        // Initial camera placement
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
        // Holding Left Shift = run
        bool runKey = Input.GetKey(KeyCode.LeftShift);

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
            }
            else if (_jumpCount == 1)
            {
                _jumpCount = 2;
                verticalVelocity = jumpSpeed;
                Animator?.SetTrigger("DoubleJump");
                SpawnDoubleJumpVfx();
            }
        }

        if (!CharacterController.isGrounded)
            verticalVelocity -= gravity * Time.deltaTime;

        _moveDirection.y = verticalVelocity;

        CharacterController.Move(_moveDirection * Time.deltaTime);
    }

    void HandleCamera()
    {
        if (!canMove || _playerCamera == null)
            return;

        float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

        // Horizontal rotation affects the body
        transform.rotation *= Quaternion.Euler(0f, mouseX, 0f);

        // Vertical rotation is camera-only
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, -lookXLimit, lookXLimit);

        Quaternion camRot = Quaternion.Euler(_rotationX, transform.eulerAngles.y, 0f);

        Vector3 desiredPos =
            transform.position
            - camRot * Vector3.forward * cameraDistance
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
        bool runKey = Input.GetKey(KeyCode.LeftShift);
        IsRunning = IsMoving && runKey;

        Animator.SetBool("IsMoving", IsMoving);
        Animator.SetBool("IsSprinting", IsRunning);
        Animator.SetBool("IsGrounded", CharacterController.isGrounded);
        Animator.SetBool("InCombat", _inCombat);
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
}
