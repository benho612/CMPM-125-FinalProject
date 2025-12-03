using System;
using UnityEngine;

/// <summary>
/// Handles health + hit reactions for a player avatar.
/// Works with multiple character models / animators because it always uses
/// the currently active child Animator, and it reads defending state from
/// PlayerControl.IsDefending.
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

    private Animator _animator;
    private PlayerControl _playerControl;

    private int _normalHitHash;
    private int _defendHitHash;

    private bool _dead;
    private float _respawnTimer;

    private WarriorCombatController _warriorCombat;

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
        Debug.Log($"[PlayerDamageReceiver] Awake: hp={hp:0}/{maxHp:0} on '{name}'.");
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

        bool isDefendingNow = false;

        // Warriors
        if (_warriorCombat != null && _warriorCombat.enabled)
        {
            isDefendingNow = _warriorCombat.IsDefending;
        }

        float finalDamage = Mathf.Max(0f, amount);

        if (isDefendingNow)
        {
            finalDamage *= defendedDamageMultiplier;
            Debug.Log($"[PlayerDamageReceiver] Blocked damage on '{name}': incoming={amount:0.##} final={finalDamage:0.##} (mult={defendedDamageMultiplier:0.##}).");

            if (_animator != null)
            {
                _animator.ResetTrigger(_normalHitHash);
                _animator.SetTrigger(_defendHitHash);
            }
        }
        else
        {
            Debug.Log($"[PlayerDamageReceiver] Normal damage on '{name}': incoming={amount:0.##}.");
            if (_animator != null)
            {
                _animator.ResetTrigger(_defendHitHash);
                _animator.SetTrigger(_normalHitHash);
            }
        }

        float before = hp;
        hp = Mathf.Clamp(hp - finalDamage, 0f, maxHp);
        OnHealthChanged?.Invoke(hp, maxHp);
        Debug.Log($"[PlayerDamageReceiver] HP change on '{name}': {before:0.##} -> {hp:0.##} (−{before - hp:0.##}).");

        if (hp <= 0f)
        {
            _dead = true;
            OnDeath?.Invoke();
            _respawnTimer = 10f;
            Debug.Log($"[PlayerDamageReceiver] '{name}' died.");
        }
    }

    /// <summary>
    /// Apply damage that ignores blocking (e.g., vents). Always full amount.
    /// </summary>
    public void ApplyUnblockableDamage(float amount)
    {
        if (_dead) return;

        float finalDamage = Mathf.Max(0f, amount);
        Debug.Log($"[PlayerDamageReceiver] Unblockable damage on '{name}': incoming={amount:0.##}.");

        if (_animator != null)
        {
            _animator.ResetTrigger(_defendHitHash);
            _animator.SetTrigger(_normalHitHash);
        }

        float before = hp;
        hp = Mathf.Clamp(hp - finalDamage, 0f, maxHp);
        OnHealthChanged?.Invoke(hp, maxHp);
        Debug.Log($"[PlayerDamageReceiver] HP change on '{name}': {before:0.##} -> {hp:0.##} (−{before - hp:0.##}).");

        if (hp <= 0f)
        {
            _dead = true;
            OnDeath?.Invoke();
            _respawnTimer = 10f;
            Debug.Log($"[PlayerDamageReceiver] '{name}' died.");
        }
    }

    // --------------------------------------------------------
    private void Respawn()
    {
        _dead = false;
        hp = maxHp;
        OnHealthChanged?.Invoke(hp, maxHp);
        Debug.Log($"[PlayerDamageReceiver] Respawn '{name}': hp reset to {hp:0}/{maxHp:0}.");
    }

    /// <summary>
    /// Finds the currently active child Animator (wizard / male / female).
    /// </summary>
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