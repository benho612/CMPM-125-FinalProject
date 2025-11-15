using UnityEngine;
using System.Reflection;

/// Attach this to the Player and point it at your FPS controller script.
/// Set the field or property name that controls speed (e.g., "speed", "moveSpeed").
public class MovementSpeedLink : MonoBehaviour
{
    public Component target;          // your FPS controller script
    public string speedMemberName = "moveSpeed";
    public float fallbackBaseSpeed = 6f;

    float _baseSpeed;
    bool _hasBase;
    MemberInfo _member;

    void Awake()
    {
        if (!target) return;
        var type = target.GetType();
        _member = type.GetField(speedMemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               ?? (MemberInfo)type.GetProperty(speedMemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        _baseSpeed = ReadSpeed();
        _hasBase = true;
    }

    float ReadSpeed()
    {
        if (!target) return fallbackBaseSpeed;
        if (_member is FieldInfo f) return f.GetValue(target) is float v ? v : fallbackBaseSpeed;
        if (_member is PropertyInfo p) return p.GetValue(target) is float v ? v : fallbackBaseSpeed;
        return fallbackBaseSpeed;
    }

    void WriteSpeed(float v)
    {
        if (!target) return;
        if (_member is FieldInfo f) f.SetValue(target, v);
        else if (_member is PropertyInfo p && p.CanWrite) p.SetValue(target, v);
    }

    public void MultiplySpeed(float factor)
    {
        float baseSpd = _hasBase ? _baseSpeed : ReadSpeed();
        WriteSpeed(baseSpd * Mathf.Clamp(factor, 0.1f, 2f));
    }

    public void RestoreSpeed()
    {
        if (_hasBase) WriteSpeed(_baseSpeed);
    }
}
