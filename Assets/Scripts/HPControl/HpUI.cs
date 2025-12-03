using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

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

    private void Awake()
    {
        _maxHp = 100f;
        _hp = _maxHp;

        _hpFillImage = _hpFillRect.GetComponent<Image>();

        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        _maxWidth = _hpFillRect.sizeDelta.x;
        _preWidth = _maxWidth;
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _hp -= 10f;
            UpdateHp();
        }
    }

    private void UpdateHp()
    {
        if(_isDead)
        {
            return;
        }

        _hp = Mathf.Clamp(_hp, 0, _maxHp);

        if (_hp == 0)
        {
            _isDead = true;
        }


        float fillAmount = _hp / _maxHp;
        float newWidth = _maxWidth * fillAmount;
        float deltaWidth = _preWidth - newWidth;

        _hpFillRect.sizeDelta = new Vector2(newWidth, _hpFillRect.sizeDelta.y);
        _preWidth = newWidth;

        _hpFillImage.color = Color.Lerp(Color.red, Color.green, fillAmount);

        Vector3 rightEdgeLocalPosition = new Vector3(_hpFillRect.sizeDelta.x, 0, 0);
        Vector3 rightEdgeWorldPosition = _hpFillRect.TransformPoint(rightEdgeLocalPosition);
        RectTransform effectRect = Instantiate(_hpEffectRectPrefab, transform);
        effectRect.position = rightEdgeWorldPosition;

        effectRect.sizeDelta = new Vector2(deltaWidth, _hpFillRect.sizeDelta.y);
        effectRect.gameObject.SetActive(true);

        effectRect.GetComponentInChildren<Image>().color = _hpFillImage.color;

        _animator.Play("HealthBarInjured", -1, 0f);


    }



}
