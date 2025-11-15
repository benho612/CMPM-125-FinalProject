using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerSlow : MonoBehaviour
{
    public bool IsSlowed => _active > 0;
    int _active;
    Coroutine _co;
    MovementSpeedLink _link;

    void Awake() { _link = GetComponent<MovementSpeedLink>(); }

    public void ApplySlow(float multiplier, float duration)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(SlowRoutine(Mathf.Clamp(multiplier, 0.1f, 1f), Mathf.Max(0.1f, duration)));
    }

    IEnumerator SlowRoutine(float mult, float dur)
    {
        _active++;
        if (_link) _link.MultiplySpeed(mult);
        float t = dur;
        while (t > 0f) { t -= Time.deltaTime; yield return null; }
        _active--;
        if (_active <= 0)
        {
            _active = 0;
            if (_link) _link.RestoreSpeed();
        }
        _co = null;
    }
}
