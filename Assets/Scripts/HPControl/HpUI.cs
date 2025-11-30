using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class HpUI : MonoBehaviour
{

    [SerializeField]
    private RectTransform _hpFillRect;
    private Image _hpFillImage;
    private float _maxWidth;
    private float _hp;
    private float _maxHp;

    private void Awake()
    {
        _maxHp = 100f;
        _hp = _maxHp;

        _hpFillImage = _hpFillRect.GetComponent<Image>();
    }

    private void Start()
    {
        _maxWidth = _hpFillRect.sizeDelta.x;
        
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
        _hp = Mathf.Clamp(_hp, 0, _maxHp);
        float fillAmount = _hp / _maxHp;
        _hpFillRect.sizeDelta = new Vector2(_maxWidth * fillAmount, _hpFillRect.sizeDelta.y);

        _hpFillImage.color = Color.Lerp(Color.red, Color.green, fillAmount);
    }



}
