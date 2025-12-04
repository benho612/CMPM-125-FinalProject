using UnityEngine;
using UnityEngine.UI;

public class FinalRamenCounterUI : MonoBehaviour
{
    public TMPro.TextMeshProUGUI counterText;

    void Start()
    {
        UpdateCounter(0);
    }

    public void UpdateCounter(int value)
    {
        counterText.text = $"Final Product Material: {value}/2";
    }
}
