using Alteruna;
using UnityEngine;

[RequireComponent(typeof(Alteruna.Avatar))]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Base setup")]
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    [SerializeField] private float cameraYOffset = 0.4f;

    private Alteruna.Avatar _avatar;
    private CharacterController _cc;
    private Camera _cam;

    private Vector3 _move = Vector3.zero;
    private float _rotX;
    public bool canMove = true;

    void Awake()
    {
        _avatar = GetComponent<Alteruna.Avatar>();
        _cc = GetComponent<CharacterController>();

        // If you accidentally have a Rigidbody on this prefab, stop it on remotes.
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            bool local = _avatar != null && _avatar.IsMe;
            rb.isKinematic = !local;
            rb.useGravity = local;
        }

        // Optional but safest: remote clones don't need a CC collider at all.
        // If you want remotes to never collide/push locally, uncomment:
        // if (_avatar != null && !_avatar.IsMe) _cc.enabled = false;
    }

    void Start()
    {
        if (_avatar == null || !_avatar.IsMe) return;

        // Attach camera only on the local avatar
        _cam = Camera.main;
        if (_cam != null)
        {
            _cam.transform.SetParent(transform, worldPositionStays: false);
            _cam.transform.localPosition = new Vector3(0f, cameraYOffset, 0f);
            _cam.transform.localRotation = Quaternion.identity;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (_avatar == null || !_avatar.IsMe)
            return;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;

        float speed = isRunning ? runningSpeed : walkingSpeed;
        float vx = canMove ? speed * Input.GetAxis("Vertical") : 0f;
        float vz = canMove ? speed * Input.GetAxis("Horizontal") : 0f;

        float y = _move.y;
        _move = fwd * vx + right * vz;

        if (_cc.isGrounded)
        {
            if (canMove && Input.GetButton("Jump")) y = jumpSpeed;
        }
        else
        {
            y -= gravity * Time.deltaTime;
        }
        _move.y = y;

        _cc.Move(_move * Time.deltaTime);

        if (canMove && _cam != null)
        {
            _rotX += -Input.GetAxis("Mouse Y") * lookSpeed;
            _rotX = Mathf.Clamp(_rotX, -lookXLimit, lookXLimit);
            _cam.transform.localRotation = Quaternion.Euler(_rotX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }
}
