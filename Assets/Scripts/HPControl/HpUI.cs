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
    private bool _layoutReady;

    private void Awake()
    {
        if (_hpFillRect == null)
            Debug.LogWarning("[HpUI] _hpFillRect is not assigned.");

        _hpFillImage = _hpFillRect != null ? _hpFillRect.GetComponent<Image>() : null;
        if (_hpFillImage == null)
            Debug.LogWarning("[HpUI] Could not find Image on _hpFillRect.");

        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogWarning("[HpUI] Animator not found on HpUI GameObject.");

        _maxHp = 100f;
        _hp = _maxHp;

        Debug.Log("[HpUI] Awake: initialized defaults hp=100/max=100. Waiting to bind PlayerDamageReceiver.");
    }

    private void Start()
    {
        StartCoroutine(WaitForLayoutThenInitWidth());
    }

    private IEnumerator WaitForLayoutThenInitWidth()
    {
        // Ensure UI layout system has run before reading sizes
        yield return null; // wait one frame
        Canvas.ForceUpdateCanvases();

        RecomputeMaxWidth();

        _layoutReady = true;
        Debug.Log($"[HpUI] Layout ready: _maxWidth={_maxWidth:0.##}, _preWidth={_preWidth:0.##}.");
    }

    private void RecomputeMaxWidth()
    {
        if (_hpFillRect == null) return;

        // Prefer the actual rendered rect width; sizeDelta can be 0 when driven by layout groups/anchors
        float rectWidth = _hpFillRect.rect.width;
        float sizeWidth = _hpFillRect.sizeDelta.x;

        _maxWidth = rectWidth > 0.01f ? rectWidth : sizeWidth;
        _preWidth = _maxWidth;

        Debug.Log($"[HpUI] RecomputeMaxWidth: rect.width={rectWidth:0.##}, sizeDelta.x={sizeWidth:0.##} -> _maxWidth={_maxWidth:0.##}.");
    }

    private void OnEnable()
    {
        if (!_subscribed)
        {
            Debug.Log("[HpUI] OnEnable: starting FindAndBindLocalPlayer coroutine.");
            StartCoroutine(FindAndBindLocalPlayer());
        }
    }

    private void OnDisable()
    {
        if (_receiver != null && _subscribed)
        {
            _receiver.OnHealthChanged -= HandleHealthChanged;
            _receiver.OnDeath -= HandleDeath;
            _subscribed = false;
            Debug.Log($"[HpUI] OnDisable: unsubscribed from '{_receiver.name}'.");
        }
    }

    private IEnumerator FindAndBindLocalPlayer()
    {
        var wait = new WaitForSeconds(0.25f);
        while (_receiver == null)
        {
            var receivers = FindObjectsOfType<PlayerDamageReceiver>(true);
            Debug.Log($"[HpUI] Polling receivers: found {receivers.Length} PlayerDamageReceiver(s).");

            PlayerDamageReceiver candidate = null;

            foreach (var r in receivers)
            {
                var avatar = r.GetComponent<Alteruna.Avatar>();
                if (avatar != null)
                {
                    Debug.Log($"[HpUI] Receiver '{r.name}' Avatar.IsMe={avatar.IsMe}.");
                    if (avatar.IsMe)
                    {
                        candidate = r;
                        break;
                    }
                }
                else
                {
                    Debug.Log($"[HpUI] Receiver '{r.name}' has no Alteruna.Avatar component.");
                }
            }

            if (candidate == null && receivers.Length == 1)
            {
                Debug.Log("[HpUI] Single-player fallback: using the only receiver found.");
                candidate = receivers[0];
            }

            if (candidate == null)
            {
                Debug.Log("[HpUI] No suitable receiver yet; retrying...");
                yield return wait;
                continue;
            }

            _receiver = candidate;
        }

        _receiver.OnHealthChanged += HandleHealthChanged;
        _receiver.OnDeath += HandleDeath;
        _subscribed = true;
        Debug.Log($"[HpUI] Subscribed to PlayerDamageReceiver '{(_receiver != null ? _receiver.name : "NULL")}'. Initializing UI.");

        if (_receiver != null)
            HandleHealthChanged(_receiver.hp, _receiver.maxHp);
        else
            Debug.LogWarning("[HpUI] _receiver became null before initialization.");
    }

    // Called whenever HP changes
    private void HandleHealthChanged(float current, float max)
    {
        Debug.Log($"[HpUI] HandleHealthChanged: current={current:0.##}, max={max:0.##}.");
        if (_hpFillRect == null || _hpFillImage == null)
        {
            Debug.LogWarning("[HpUI] HandleHealthChanged: UI references missing (_hpFillRect or _hpFillImage).");
            return;
        }

        // If layout not ready or width still zero, recompute and bail this frame to avoid black bar
        if (!_layoutReady || (_maxWidth <= 0.01f))
        {
            Debug.LogWarning("[HpUI] Layout not ready or _maxWidth=0. Recomputing and deferring update one frame.");
            RecomputeMaxWidth();
            return;
        }

        _maxHp = max;
        float prevHp = _hp;
        _hp = Mathf.Clamp(current, 0f, _maxHp);

        if (_isDead && _hp > 0f) _isDead = false;

        float fillAmount = _maxHp > 0f ? _hp / _maxHp : 0f;
        float newWidth = _maxWidth * fillAmount;
        float deltaWidth = Mathf.Max(0f, _preWidth - newWidth);

        _hpFillRect.sizeDelta = new Vector2(newWidth, _hpFillRect.sizeDelta.y);
        _preWidth = newWidth;

        _hpFillImage.color = Color.Lerp(Color.red, Color.green, fillAmount);

        Debug.Log($"[HpUI] Bar updated: fill={fillAmount:0.##}, width={newWidth:0.##} (max={_maxWidth:0.##}), delta={deltaWidth:0.##}, color={_hpFillImage.color}.");

        if (deltaWidth > 0.01f && _hpEffectRectPrefab != null)
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
            {
                _animator.Play("HealthBarInjured", -1, 0f);
            }
        }
        else if (_hpEffectRectPrefab == null)
        {
            Debug.LogWarning("[HpUI] _hpEffectRectPrefab is not assigned; no trailing effect spawned.");
        }
    }

    private void HandleDeath()
    {
        _isDead = true;
        Debug.Log("[HpUI] HandleDeath: player marked dead; UI remains for feedback.");
    }
}