// Assets/Bosses/_Shared/PlayerStun.cs
using UnityEngine;

public class PlayerStun : MonoBehaviour
{
    public bool IsStunned { get; private set; }
    float timer;

    public void ApplyStun(float sec) { IsStunned = true; timer = sec; /* disable input here if needed */ }
    void Update()
    {
        if (IsStunned) { timer -= Time.deltaTime; if (timer <= 0f) { IsStunned = false; /* re-enable input */ } }
    }
}