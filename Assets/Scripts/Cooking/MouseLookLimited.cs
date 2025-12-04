using UnityEngine;

public class MouseLookLimited : MonoBehaviour
{
    [Header("Sensitivity")]
    public float sensitivity = 2.0f;

    [Header("Limits")]
    public float maxYaw = 45f;     // left-right rotation limit
    public float maxPitch = 30f;   // up-down rotation limit

    private float yaw = 0f;
    private float pitch = 0f;

    private bool active = false;

    public void EnableLook()
    {
        active = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Reset rotation
        yaw = 0f;
        pitch = 0f;
        transform.localRotation = Quaternion.identity;
    }

    public void DisableLook()
    {
        active = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        if (!active)
            return;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        yaw += mouseX;
        pitch -= mouseY;

        yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
