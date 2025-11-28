using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Alteruna;

/// <summary>
/// Local player controller: movement, jump + double jump, combat stance,
/// combo attacks, defending, third-person camera, and animation parameters.
/// Works even if different character models / animators are toggled on/off
/// (wizard / male / female) because it always finds the active child Animator.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerControl : MonoBehaviour
{
    [Header("Base setup")]
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    [Header("Combat")]
    public float combatRange = 10f;
    public string enemyTag = "Enemy";
    public float combatExitDelay = 5f;

    [Tooltip("Time window between clicks to continue the combo.")]
    public float comboResetTime = 0.9f;

    [Tooltip("Cooldown between individual attacks.")]
    [SerializeField] private float attackCooldown = 0.3f;
    private float lastAttackTime = 0f;

    [Header("Wizard Attack (charge)")]
    [SerializeField] private float wizardLightThreshold = 0.25f;   // < 0.25s = light
    [SerializeField] private float wizardHeavyThreshold = 0.75f;   // 0.25¡V0.75 = medium, > 0.75 = heavy
    [SerializeField] private float wizardMaxChargeTime = 1.5f;

    private bool wizardIsCharging = false;
    private float wizardChargeTime = 0f;

    [Header("Input Actions (New Input System)")]
    public InputActionReference attackAction;
    public InputActionReference defendAction;
    public InputActionReference moveAction;

    [Header("Defend movement")]
    [Tooltip("Speed multiplier while defending (0.4 = 40% speed).")]
    [Range(0f, 1f)]
    public float defendSpeedMultiplier = 0.4f;

    [Header("Third-person camera")]
    public float cameraDistance = 10.0f;
    public float cameraHeight = 5.0f;
    public float cameraSmooth = 5.0f;
    public float cameraYOffset = 4.0f;

    // ---- internal state ----
    private CharacterController characterController;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0f;
    private int jumpCount = 0;

    private bool inCombat = false;
    private float lastCombatTime = -999f;
    private int comboStep = 0; // 0 = none, 1..4 = which attack
    private float lastAttackClickTime = -999f;

    // Defend state & movement
    private bool isDefending = false;
    private float defendMoveX = 0f;
    private float defendMoveZ = 0f;

    [HideInInspector]
    public bool canMove = true;

    public enum CharacterType
    {
        Warrior,
        Wizard
    }

    private enum AttackStyle
    {
        Warrior,
        Wizard
    }
    private AttackStyle attackStyle;
    [Header("Character")]

    [SerializeField] private CharacterType characterType = CharacterType.Warrior;
    private Camera playerCamera;
    private Animator animator;
    private Alteruna.Avatar _avatar; // Alteruna avatar

    // Expose defending state for other scripts (e.g. PlayerDamageReceiver)
    public bool IsDefending => isDefending;


    // --------------------------------------------------------
    void Start()
    {
        _avatar = GetComponent<Alteruna.Avatar>();

        // Only let the local owner control this instance
        if (_avatar != null && !_avatar.IsMe)
        {
            // Remote avatars: do not run input/camera logic.
            return;
        }

        int id = CharacterDatabase.SelectedCharacterID;

        if (id == 0)
            attackStyle = AttackStyle.Wizard;
        else
            attackStyle = AttackStyle.Warrior;

        Debug.Log("Attack style = " + attackStyle);

        characterController = GetComponent<CharacterController>();
        animator = FindActiveAnimator();
        playerCamera = GetComponentInChildren<Camera>();

        if (attackAction != null)
            attackAction.action.Enable();
        if (defendAction != null)
            defendAction.action.Enable();
        if (moveAction != null)
            moveAction.action.Enable();

        if (playerCamera == null)
        {
            Debug.LogError("PlayerControl: No camera found on avatar.", this);
            return;
        }

        // Initial camera placement
        Vector3 startPos = transform.position
                           - transform.forward * cameraDistance
                           + Vector3.up * cameraHeight;
        playerCamera.transform.position = startPos;
        playerCamera.transform.LookAt(transform.position + Vector3.up * cameraYOffset);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // --------------------------------------------------------
    void Update()
    {
        // Only local avatar should run this.
        if (_avatar != null && !_avatar.IsMe)
            return;

        // If model changed (wizard / male / female), re-find active animator
        if (animator == null || !animator.gameObject.activeInHierarchy)
        {
            animator = FindActiveAnimator();
        }

        bool isRunningKey = Input.GetKey(KeyCode.LeftShift);

        // ---- MOVEMENT (with normalized diagonal speed) ----
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float inputX = Input.GetAxisRaw("Horizontal"); // A/D
        float inputZ = Input.GetAxisRaw("Vertical");   // W/S

        Vector2 inputVec = new Vector2(inputX, inputZ);
        if (inputVec.sqrMagnitude > 1f)
            inputVec.Normalize();

        float baseSpeed = isRunningKey ? runningSpeed : walkingSpeed;

        // slower while defending
        if (isDefending)
            baseSpeed *= defendSpeedMultiplier;

        float curSpeedZ = canMove ? baseSpeed * inputVec.y : 0f;
        float curSpeedX = canMove ? baseSpeed * inputVec.x : 0f;

        float verticalVelocity = moveDirection.y;
        moveDirection = (forward * curSpeedZ) + (right * curSpeedX);

        // ---- Jump & Double Jump ----
        if (characterController.isGrounded)
            jumpCount = 0;

        if (canMove && Input.GetButtonDown("Jump"))
        {
            if (characterController.isGrounded)
            {
                jumpCount = 1;
                verticalVelocity = jumpSpeed;
                animator?.SetTrigger("Jump");
            }
            else if (jumpCount == 1)
            {
                jumpCount = 2;
                verticalVelocity = jumpSpeed;
                animator?.SetTrigger("DoubleJump");
            }
        }

        if (!characterController.isGrounded)
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        moveDirection.y = verticalVelocity;
        characterController.Move(moveDirection * Time.deltaTime);

        // ---- COMBAT STATE (enemy detection + timer) ----
        bool enemyNear = IsEnemyInRange();
        if (enemyNear)
        {
            inCombat = true;
            lastCombatTime = Time.time;
        }

        if (!enemyNear && inCombat && Time.time - lastCombatTime > combatExitDelay)
        {
            inCombat = false;
            comboStep = 0;
        }

        // --- COMBO ATTACK INPUT (Input System) ---
        if (animator != null && attackAction != null && attackAction.action.WasPerformedThisFrame())
        {
            if (isDefending)
                return;

            if (Time.time - lastAttackTime < attackCooldown)
                return;

            lastAttackTime = Time.time;

            // Choose attack system by character type
            if (attackStyle == AttackStyle.Warrior)
            {
                HandleWarriorCombo();
            }
            else if (attackStyle == AttackStyle.Wizard)
            {
                HandleWizardAttack();
            }

            inCombat = true;
            lastCombatTime = Time.time;
            animator.SetBool("InCombat", true);
        }

        // ---- DEFEND INPUT ----
        if (defendAction != null)
            isDefending = defendAction.action.IsPressed();
        else
            isDefending = Input.GetMouseButton(1); // fallback

        // Defend movement (for defend animations)
        if (moveAction != null)
        {
            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            defendMoveX = moveInput.x;
            defendMoveZ = moveInput.y;
        }
        else
        {
            defendMoveX = inputX;
            defendMoveZ = inputZ;
        }

        if (animator != null)
        {
            animator.SetBool("IsDefending", isDefending);
            animator.SetFloat("DefendMoveX", defendMoveX);
            animator.SetFloat("DefendMoveZ", defendMoveZ);
        }

        // ---- ANIMATION PARAMETERS (locomotion & combat) ----
        Vector3 horizontalVelocity = characterController.velocity;
        horizontalVelocity.y = 0f;
        float speed = horizontalVelocity.magnitude;

        bool isMoving = speed > 0.1f;
        bool isSprinting = isMoving && isRunningKey && !isDefending;

        if (animator != null)
        {
            animator.SetBool("IsMoving", isMoving);
            animator.SetBool("IsSprinting", isSprinting);
            animator.SetBool("IsGrounded", characterController.isGrounded);
            animator.SetBool("InCombat", inCombat);
        }

        // ---- CAMERA (third-person) ----
        if (canMove && playerCamera != null)
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            // Horizontal rotation (turn player)
            transform.rotation *= Quaternion.Euler(0f, mouseX, 0f);

            // Vertical camera pitch
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

            Quaternion camRot = Quaternion.Euler(rotationX, transform.eulerAngles.y, 0f);

            Vector3 desiredPos =
                transform.position
                - camRot * Vector3.forward * cameraDistance
                + Vector3.up * cameraHeight;

            playerCamera.transform.position =
                Vector3.Lerp(playerCamera.transform.position, desiredPos, cameraSmooth * Time.deltaTime);

            Vector3 lookTarget = transform.position + Vector3.up * cameraYOffset;
            playerCamera.transform.rotation = Quaternion.Lerp(
                playerCamera.transform.rotation,
                Quaternion.LookRotation(lookTarget - playerCamera.transform.position),
                cameraSmooth * Time.deltaTime
            );
        }
    }

    // --------------------------------------------------------
    private bool IsEnemyInRange()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (var e in enemies)
        {
            if (Vector3.Distance(transform.position, e.transform.position) <= combatRange)
                return true;
        }
        return false;
    }

    private void PlayAttackAnimation(int step)
    {
        if (animator == null) return;

        string stateName;
        switch (step)
        {
            case 1: stateName = "Attack01"; break;
            case 2: stateName = "Attack02"; break;
            case 3: stateName = "Attack03"; break;
            case 4: stateName = "Attack04"; break;
            default: stateName = "Attack01"; break;
        }

        animator.CrossFade(stateName, 0.05f, 0);
    }

    /// <summary>
    /// Finds the currently active child Animator (wizard / male / female).
    /// If none are active, returns the first animator it finds.
    /// </summary>
    private Animator FindActiveAnimator()
    {
        var animators = GetComponentsInChildren<Animator>(true);
        foreach (var a in animators)
        {
            if (a != null && a.gameObject.activeInHierarchy)
                return a;
        }

        return animators.Length > 0 ? animators[0] : null;
    }

    // ================== WARRIOR ATTACK (click combo) ==================
    private void HandleWarriorCombo()
    {
        float sinceLast = Time.time - lastAttackClickTime;

        if (sinceLast > comboResetTime)
            comboStep = 1;
        else
        {
            comboStep++;
            if (comboStep > 4)
                comboStep = 1;
        }

        lastAttackClickTime = Time.time;
        PlayAttackAnimation(comboStep);
    }

    // ================== WIZARD ATTACK (hold to charge) ==================
    private float chargeStart = 0f;

    private void HandleWizardAttack()
    {
        chargeStart = Time.time;
        animator.CrossFade("AttackCharge", 0.05f);

        StartCoroutine(ReleaseWizardSpell());
    }

    private IEnumerator ReleaseWizardSpell()
    {
        // Wait for button release
        while (attackAction.action.IsPressed())
            yield return null;

        float chargeTime = Time.time - chargeStart;

        if (chargeTime < 0.5f)
            animator.CrossFade("Attack01", 0.05f);     // weak spell
        else if (chargeTime < 1.5f)
            animator.CrossFade("Attack02", 0.05f);     // medium spell
        else
            animator.CrossFade("Attack03", 0.05f);     // full power spell
    }

}
