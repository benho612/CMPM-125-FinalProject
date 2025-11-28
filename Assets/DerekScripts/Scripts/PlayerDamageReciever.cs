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

    // --------------------------------------------------------
    void Awake()
    {
        _animator = FindActiveAnimator();
        _playerControl = GetComponent<PlayerControl>();

        if (_animator != null)
        {
            _normalHitHash = Animator.StringToHash(normalHitTrigger);
            _defendHitHash = Animator.StringToHash(defendHitTrigger);
        }

        if (maxHp <= 0f) maxHp = 100f;
        hp = maxHp;
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
    /// Called by enemies (e.g., GroundDragon) when this player takes damage.
    /// </summary>
    public void ApplyDamage(float amount)
    {
        if (_dead) return;

        bool isDefendingNow = _playerControl != null && _playerControl.IsDefending;

        float finalDamage = amount;

        if (isDefendingNow)
        {
            // Blocked: reduced damage & defend hit animation
            finalDamage *= defendedDamageMultiplier;

            if (_animator != null)
            {
                _animator.ResetTrigger(_normalHitHash);
                _animator.SetTrigger(_defendHitHash);
            }
        }
        else
        {
            // Normal hit
            if (_animator != null)
            {
                _animator.ResetTrigger(_defendHitHash);
                _animator.SetTrigger(_normalHitHash);
            }
        }

        hp -= finalDamage;

        if (hp <= 0f)
        {
            _dead = true;
            OnDeath?.Invoke();
            _respawnTimer = 10f; // adjust as needed

            // TODO: trigger death animation / disable movement here if desired
        }
    }

    // --------------------------------------------------------
    private void Respawn()
    {
        _dead = false;
        hp = maxHp;

        // TODO: teleport to spawn, reset animations & state as needed
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
