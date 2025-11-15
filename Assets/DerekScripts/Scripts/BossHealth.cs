// Assets/Bosses/_Shared/BossHealth.cs
using UnityEngine;

public class BossHealth : MonoBehaviour
{
    [SerializeField] float maxHp = 1000f;
    [SerializeField] BossNetSync net;
    public float HP { get; private set; }
    public System.Action OnDeath;

    void Awake() { HP = maxHp; if (!net) net = GetComponent<BossNetSync>(); }

    public void Damage(float amount)
    {
        if (HP <= 0f) return;
        HP = Mathf.Max(0f, HP - amount);
        net?.BroadcastHealth(HP / maxHp);
        if (HP <= 0f) OnDeath?.Invoke();
    }

    // Optional helpers
    public void SetMax(float newMax) { maxHp = newMax; HP = maxHp; net?.BroadcastHealth(1f); }
}
