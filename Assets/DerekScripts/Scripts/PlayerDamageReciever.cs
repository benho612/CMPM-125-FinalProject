using System;
using UnityEngine;
using Alteruna;

/// <summary>
/// Handles health + hit reactions for a player avatar.
/// Works with multiple character models / animators.
/// </summary>
public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Health")]
    public float maxHp = 100f;
    public float hp = 100f;
    public Action OnDeath;
    public Action<float, float> OnHealthChanged; // (current, max)

    [Header("Blocking / Defence")]
    [Tooltip("Damage multiplier while defending (0.3 = 30% of incoming).")]
    [Range(0f, 1f)]
    public float defendedDamageMultiplier = 0.3f;

    [Tooltip("Animator trigger for a normal hit reaction.")]
    public string normalHitTrigger = "Normal_Hit";

    [Tooltip("Animator trigger for a blocked / defend hit reaction.")]
    public string defendHitTrigger = "Defend_Hit";

    [Header("Debug")]
    [Tooltip("Enable detailed damage/health logs.")]
    public bool verbose = false;

    private Animator _animator;
    private PlayerControl _playerControl;
    private WarriorCombatController _warriorCombat;

    private int _normalHitHash;
    private int _defendHitHash;

    private bool _dead;
    private float _respawnTimer;

    // --------------------------------------------------------
    void Awake()
    {
        _animator = FindActiveAnimator();
        _warriorCombat = GetComponent<WarriorCombatController>();
        _playerControl = GetComponent<PlayerControl>();

        if (_animator != null)
        {
            _normalHitHash = Animator.StringToHash(normalHitTrigger);
            _defendHitHash = Animator.StringToHash(defendHitTrigger);
        }

        if (maxHp <= 0f) maxHp = 100f;
        hp = maxHp;

        OnHealthChanged?.Invoke(hp, maxHp);
        if (verbose) Debug.Log($"[PlayerDamageReceiver] Awake: hp={hp:0}/{maxHp:0} on '{name}'.");
    }

    void Update()
    {
        // If character model was swapped, re-find active animator
        if (_animator == null || !_animator.gameObject.activeInHierarchy)
        {
            _animator = FindActiveAnimator();
            if (_animator != null)
            {
                _normalHitHash = Animator.StringToHash(normalHitTrigger);
                _defendHitHash = Animator.StringToHash(defendHitTrigger);
            }
        }

        if (_dead)
        {
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
                Respawn();
        }
    }

    /// <summary>
    /// Apply damage that can be blocked (e.g., fireballs).
    /// </summary>
    public void ApplyDamage(float amount)
    {
        if (_dead) return;

        bool isDefendingNow = (_warriorCombat != null && _warriorCombat.enabled) && _warriorCombat.IsDefending;

        float finalDamage = Mathf.Max(0f, amount);
        if (isDefendingNow)
        {
            finalDamage *= defendedDamageMultiplier;
            if (_animator != null)
            {
                _animator.ResetTrigger(_normalHitHash);
                _animator.SetTrigger(_defendHitHash);
            }
            if (verbose) Debug.Log($"[PlayerDamageReceiver] Blocked: {amount:0.##} -> {finalDamage:0.##} on '{name}'.");
        }
        else
        {
            if (_animator != null)
            {
                _animator.ResetTrigger(_defendHitHash);
                _animator.SetTrigger(_normalHitHash);
            }
            if (verbose) Debug.Log($"[PlayerDamageReceiver] Damage: {amount:0.##} on '{name}'.");
        }

        ApplyDamageInternal(finalDamage);
    }

    /// <summary>
    /// Apply damage that ignores blocking (e.g., vents). Always full amount.
    /// </summary>
    public void ApplyUnblockableDamage(float amount)
    {
        if (_dead) return;

        float finalDamage = Mathf.Max(0f, amount);

        if (_animator != null)
        {
            _animator.ResetTrigger(_defendHitHash);
            _animator.SetTrigger(_normalHitHash);
        }

        if (verbose) Debug.Log($"[PlayerDamageReceiver] Unblockable: {amount:0.##} on '{name}'.");
        ApplyDamageInternal(finalDamage);
    }

    // ---------------- Methods called by PlayerHealthSync (network RPC target) ----------------

    public void RPC_ApplyDamage(float amount)
    {
        if (_dead) return;

        bool isDefendingNow = (_warriorCombat != null && _warriorCombat.enabled) && _warriorCombat.IsDefending;

        float finalDamage = Mathf.Max(0f, amount);
        if (isDefendingNow)
        {
            finalDamage *= defendedDamageMultiplier;
            if (_animator != null)
            {
                _animator.ResetTrigger(_normalHitHash);
                _animator.SetTrigger(_defendHitHash);
            }
            if (verbose) Debug.Log($"[PlayerDamageReceiver] RPC Blocked: {amount:0.##} -> {finalDamage:0.##}.");
        }
        else
        {
            if (_animator != null)
            {
                _animator.ResetTrigger(_defendHitHash);
                _animator.SetTrigger(_normalHitHash);
            }
            if (verbose) Debug.Log($"[PlayerDamageReceiver] RPC Damage: {amount:0.##}.");
        }

        ApplyDamageInternal(finalDamage);
    }

    public void RPC_ApplyUnblockableDamage(float amount)
    {
        if (_dead) return;

        float finalDamage = Mathf.Max(0f, amount);

        if (_animator != null)
        {
            _animator.ResetTrigger(_defendHitHash);
            _animator.SetTrigger(_normalHitHash);
        }

        if (verbose) Debug.Log($"[PlayerDamageReceiver] RPC Unblockable: {amount:0.##}.");
        ApplyDamageInternal(finalDamage);
    }

    public void RPC_SetHealth(float newHp, float newMaxHp)
    {
        maxHp = Mathf.Max(1f, newMaxHp);
        float before = hp;
        hp = Mathf.Clamp(newHp, 0f, maxHp);

        OnHealthChanged?.Invoke(hp, maxHp);
        if (verbose) Debug.Log($"[PlayerDamageReceiver] RPC_SetHealth: {before:0.##} -> {hp:0.##}/{maxHp:0.##}.");

        if (hp <= 0f && !_dead)
        {
            _dead = true;
            OnDeath?.Invoke();
            _respawnTimer = 10f;
            Debug.Log($"[PlayerDamageReceiver] '{name}' died (RPC).");
        }
    }

    // --------------------------------------------------------
    private void ApplyDamageInternal(float finalDamage)
    {
        if (finalDamage <= 0f) return;

        float before = hp;
        hp = Mathf.Clamp(hp - finalDamage, 0f, maxHp);
        OnHealthChanged?.Invoke(hp, maxHp);

        if (verbose) Debug.Log($"[PlayerDamageReceiver] HP: {before:0.##} -> {hp:0.##} (−{before - hp:0.##}).");

        if (hp <= 0f)
        {
            _dead = true;
            OnDeath?.Invoke();
            _respawnTimer = 10f;
            Debug.Log($"[PlayerDamageReceiver] '{name}' died.");
        }
    }

    private void Respawn()
    {
        _dead = false;
        hp = maxHp;
        OnHealthChanged?.Invoke(hp, maxHp);
        Debug.Log($"[PlayerDamageReceiver] Respawn '{name}': hp={hp:0}/{maxHp:0}.");
    }

    private Animator FindActiveAnimator()
    {
        var animators = GetComponentsInChildren<Animator>(true);
        foreach (var a in animators)
        {
            if (a != null && a.gameObject.activeInHierarchy)
                return a;
        }

        return animators.Length > 0 ? animators[0] : null;
    }
}