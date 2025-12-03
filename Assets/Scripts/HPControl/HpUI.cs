using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HpUI : MonoBehaviour
{
    [SerializeField] private RectTransform _hpFillRect;
    [SerializeField] private RectTransform _hpEffectRectPrefab;

    private Image _hpFillImage;
    private Animator _animator;
    private float _maxWidth;
    private float _preWidth;

    private float _hp;
    private float _maxHp;
    private bool _isDead;

    private PlayerDamageReceiver _receiver;
    private bool _subscribed;

    private void Awake()
    {
        _hpFillImage = _hpFillRect.GetComponent<Image>();
        _animator = GetComponent<Animator>();

        // Default until we bind to the player
        _maxHp = 100f;
        _hp = _maxHp;
    }

    private void Start()
    {
        _maxWidth = _hpFillRect.sizeDelta.x;
        _preWidth = _maxWidth;
    }

    private void OnEnable()
    {
        if (!_subscribed)
            StartCoroutine(FindAndBindLocalPlayer());
    }

    private void OnDisable()
    {
        if (_receiver != null && _subscribed)
        {
            _receiver.OnHealthChanged -= HandleHealthChanged;
            _receiver.OnDeath -= HandleDeath;
            _subscribed = false;
        }
    }

    private IEnumerator FindAndBindLocalPlayer()
    {
        var wait = new WaitForSeconds(0.25f);
        while (_receiver == null)
        {
            // Prefer local player's receiver if Alteruna is present
            var receivers = FindObjectsOfType<PlayerDamageReceiver>(true);
            PlayerDamageReceiver candidate = null;

            foreach (var r in receivers)
            {
                var avatar = r.GetComponent<Alteruna.Avatar>();
                if (avatar != null && avatar.IsMe)
                {
                    candidate = r;
                    break;
                }
            }

            if (candidate == null && receivers.Length == 1)
                candidate = receivers[0]; // single-player fallback

            if (candidate == null)
            {
                yield return wait;
                continue;
            }

            _receiver = candidate;
        }

        _receiver.OnHealthChanged += HandleHealthChanged;
        _receiver.OnDeath += HandleDeath;
        _subscribed = true;

        // Initialize UI to current values
        HandleHealthChanged(_receiver.hp, _receiver.maxHp);
    }

    // Called whenever HP changes
    private void HandleHealthChanged(float current, float max)
    {
        _maxHp = max;
        float prevHp = _hp;
        _hp = Mathf.Clamp(current, 0f, _maxHp);

        if (_isDead && _hp > 0f) _isDead = false;

        float fillAmount = _maxHp > 0f ? _hp / _maxHp : 0f;
        float newWidth = _maxWidth * fillAmount;
        float deltaWidth = Mathf.Max(0f, _preWidth - newWidth);

        // Resize bar
        _hpFillRect.sizeDelta = new Vector2(newWidth, _hpFillRect.sizeDelta.y);
        _preWidth = newWidth;

        // Color from green->red
        _hpFillImage.color = Color.Lerp(Color.red, Color.green, fillAmount);

        // Spawn trailing effect for lost HP
        if (deltaWidth > 0.01f)
        {
            Vector3 rightEdgeLocalPosition = new Vector3(_hpFillRect.sizeDelta.x, 0, 0);
            Vector3 rightEdgeWorldPosition = _hpFillRect.TransformPoint(rightEdgeLocalPosition);
            RectTransform effectRect = Instantiate(_hpEffectRectPrefab, transform);
            effectRect.position = rightEdgeWorldPosition;

            effectRect.sizeDelta = new Vector2(deltaWidth, _hpFillRect.sizeDelta.y);
            effectRect.gameObject.SetActive(true);

            var img = effectRect.GetComponentInChildren<Image>();
            if (img != null) img.color = _hpFillImage.color;

            if (_animator != null)
                _animator.Play("HealthBarInjured", -1, 0f);
        }
    }

    private void HandleDeath()
    {
        _isDead = true;
        // Optional: animate empty bar, show death UI, etc.
    }
}