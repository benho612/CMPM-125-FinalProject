using UnityEngine;

public class SelectionCameraController : MonoBehaviour
{
    public Transform idleTarget;
    public float moveSpeed = 5f;
    public float rotateSpeed = 5f;

    private Transform _currentTarget;
    private bool _lockedOnCharacter;

    private void Start()
    {
        _currentTarget = idleTarget;
    }

    private void Update()
    {
        if (_currentTarget == null) return;

        transform.position = Vector3.Lerp(
            transform.position,
            _currentTarget.position,
            Time.deltaTime * moveSpeed
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            _currentTarget.rotation,
            Time.deltaTime * rotateSpeed
        );
    }

    public void FocusOnCharacter(Transform characterCamTarget)
    {
        _currentTarget = characterCamTarget;
        _lockedOnCharacter = true;
    }

    public void ReturnToIdle()
    {
        _currentTarget = idleTarget;
        _lockedOnCharacter = false;
    }

    public bool IsLockedOnCharacter => _lockedOnCharacter;
}
