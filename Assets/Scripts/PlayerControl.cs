using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControl : MonoBehaviour
{
    [Header("Base setup")]
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    [Header("Third-person camera")]
    public float cameraDistance = 4.0f;      // how far behind the player
    public float cameraHeight = 2.0f;        // how high above the player
    public float cameraSmooth = 10.0f;       // how smoothly the camera follows

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0f;                    // vertical camera angle

    [HideInInspector]
    public bool canMove = true;

    [SerializeField]
    private float cameraYOffset = 1.0f;      // where the camera looks (chest/head height)
    private Camera playerCamera;

    private Alteruna.Avatar _avatar;

    void Start()
    {
        _avatar = GetComponent<Alteruna.Avatar>();

        // Only control the local player
        if (!_avatar.IsMe)
            return;

        characterController = GetComponent<CharacterController>();

        // Use the camera on this avatar (child) instead of Camera.main
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            Debug.LogError("PlayerControl: No camera found on avatar.", this);
            return;
        }

        // Optional: start behind the player
        Vector3 startPos = transform.position
                           - transform.forward * cameraDistance
                           + Vector3.up * cameraHeight;
        playerCamera.transform.position = startPos;
        playerCamera.transform.LookAt(transform.position + Vector3.up * cameraYOffset);

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!_avatar.IsMe)
            return;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // ---- MOVEMENT (same as before) ----
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        float curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpSpeed;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        // ---- THIRD-PERSON CAMERA ROTATION & FOLLOW ----
        if (canMove && playerCamera != null)
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            // Rotate the player horizontally with the mouse
            transform.rotation *= Quaternion.Euler(0f, mouseX, 0f);

            // Vertical rotation only affects the camera
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);

            // Desired camera rotation and position
            Quaternion camRot = Quaternion.Euler(rotationX, transform.eulerAngles.y, 0f);

            Vector3 desiredPos =
                transform.position
                - camRot * Vector3.forward * cameraDistance
                + Vector3.up * cameraHeight;

            // Smooth follow
            playerCamera.transform.position =
                Vector3.Lerp(playerCamera.transform.position, desiredPos, cameraSmooth * Time.deltaTime);

            // Look at the player (slightly above the origin)
            Vector3 lookTarget = transform.position + Vector3.up * cameraYOffset;
            playerCamera.transform.rotation = Quaternion.Lerp(
                playerCamera.transform.rotation,
                Quaternion.LookRotation(lookTarget - playerCamera.transform.position),
                cameraSmooth * Time.deltaTime
            );
        }
    }
}
