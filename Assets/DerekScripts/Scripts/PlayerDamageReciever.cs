// Assets/Bosses/_Shared/PlayerDamageReceiver.cs
using UnityEngine;

public class PlayerDamageReceiver : MonoBehaviour
{
    public float hp = 100f;
    public System.Action OnDeath;
    float respawnTimer;
    bool dead;

    void Update()
    {
        if (dead)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f) Respawn();
        }
    }

    public void ApplyDamage(float amount)
    {
        if (dead) return;
        hp -= amount;
        if (hp <= 0f) { dead = true; OnDeath?.Invoke(); respawnTimer = 10f; /* disable movement / hide mesh etc. */ }
    }

    void Respawn()
    {
        dead = false; hp = 100f;
        // TODO: teleport to a spawn pad & grant 2s invuln
    }
}
