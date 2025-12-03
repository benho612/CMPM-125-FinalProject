using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    [Tooltip("Default shake duration if not overridden.")]
    public float defaultDuration = 0.2f;

    [Tooltip("Default shake magnitude if not overridden.")]
    public float defaultMagnitude = 0.25f;

    Vector3 _originalPos;
    float _shakeEndTime = 0f;
    float _magnitude = 0f;

    void Awake()
    {
        Instance = this;
        _originalPos = transform.localPosition;
    }

    void Update()
    {
        if (Time.time < _shakeEndTime)
        {
            Vector3 offset = Random.insideUnitSphere * _magnitude;
            offset.z = 0f; // keep forward axis stable
            transform.localPosition = _originalPos + offset;
        }
        else
        {
            transform.localPosition = _originalPos;
        }
    }

    public void Shake(float duration, float magnitude)
    {
        _shakeEndTime = Time.time + duration;
        _magnitude = magnitude;
    }

    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }
}
