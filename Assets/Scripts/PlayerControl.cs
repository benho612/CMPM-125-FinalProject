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
    [SerializeField] private float wizardHoldThreshold = 0.25f; // seconds before it becomes a channel
    private bool wizardIsChanneling = false;
    private float wizardPressTime = 0f;
    private Coroutine wizardHoldRoutine = null;

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

        if (attackStyle == AttackStyle.Wizard && wizardIsChanneling)
        {
            baseSpeed = 0f;   // you can still rotate, but no walking/running
        }

        float curSpeedZ = canMove ? baseSpeed * inputVec.y : 0f;
        float curSpeedX = canMove ? baseSpeed * inputVec.x : 0f;

        float verticalVelocity = moveDirection.y;
        moveDirection = (forward * curSpeedZ) + (right * curSpeedX);

        // ---- Jump & Double Jump ----
        if (characterController.isGrounded)
            jumpCount = 0;
        if (attackStyle == AttackStyle.Wizard && wizardIsChanneling)
        {
            // Skip all jump handling while channeling
        }
        else if (canMove && Input.GetButtonDown("Jump"))
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

        // --- ATTACK INPUT ---
        // We handle "press" for both Warrior and Wizard here
        if (animator != null && attackAction != null && attackAction.action.WasPerformedThisFrame())
        {
            // If defending, ignore attacks
            if (isDefending)
                return;

            // Global cooldown between presses
            if (Time.time - lastAttackTime < attackCooldown)
                return;

            lastAttackTime = Time.time;

            if (attackStyle == AttackStyle.Warrior)
            {
                HandleWarriorCombo();
                EnterCombat();
            }
            else if (attackStyle == AttackStyle.Wizard)
            {
                WizardAttackPressed();
                EnterCombat();
            }
        }

        // Wizard also needs to know when the button is released
        if (attackStyle == AttackStyle.Wizard && attackAction != null)
        {
            if (attackAction.action.WasReleasedThisFrame())
            {
                WizardAttackReleased();
            }
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
        PlayAttackAnimation(comboStep); // This still plays Attack01..04
    }

    // ================== WIZARD ATTACK (hold to charge) ==================
    private void WizardAttackPressed()
    {
        wizardPressTime = Time.time;

        // First, do the quick instant attack (small damage)
        // This can be interrupted later if we start channeling.
        if (animator != null)
        {
            animator.CrossFade("Attack01", 0.05f, 0);
        }

        // Start a small delay to detect if this becomes a hold
        if (wizardHoldRoutine != null)
            StopCoroutine(wizardHoldRoutine);

        wizardHoldRoutine = StartCoroutine(WizardHoldCheck());
    }

    private IEnumerator WizardHoldCheck()
    {
        float pressStamp = wizardPressTime;

        // Wait for the threshold
        yield return new WaitForSeconds(wizardHoldThreshold);

        // If button is still held, and this is still the same press, start channel
        if (attackAction != null &&
            attackAction.action.IsPressed() &&
            Mathf.Approximately(pressStamp, wizardPressTime) &&
            !wizardIsChanneling)
        {
            StartWizardChannel();
        }
    }

    private void StartWizardChannel()
    {
        wizardIsChanneling = true;

        if (animator != null)
        {
            animator.SetBool("IsChanneling", true);               // Drives Attack02Start / Maintain transitions
            animator.CrossFade("Attack02Start", 0.05f, 0);        // Play the start animation
        }
    }

    // Called when LMB is released
    private void WizardAttackReleased()
    {
        // Stop any pending hold detection
        if (wizardHoldRoutine != null)
        {
            StopCoroutine(wizardHoldRoutine);
            wizardHoldRoutine = null;
        }

        // If we were channeling, tell the animator to exit Attack02Maintain back to Battle_Idle
        if (wizardIsChanneling)
        {
            wizardIsChanneling = false;

            if (animator != null)
            {
                animator.SetBool("IsChanneling", false);
            }
        }

        // If we were NOT channeling, it was just a tap:
        // Attack01 already played on press, so nothing else to do here.
    }
    private void EnterCombat()
    {
        inCombat = true;
        lastCombatTime = Time.time;

        if (animator != null)
            animator.SetBool("InCombat", true);
    }
}
