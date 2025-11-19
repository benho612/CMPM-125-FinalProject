using UnityEngine;
using UnityEngine.UI;

public class MenuFade : MonoBehaviour
{
    public CanvasGroup menuGroup;
    public float fadeDuration = 1.0f;

    public void FadeOutMenu()
    {
        StartCoroutine(FadeOut());
    }

    private System.Collections.IEnumerator FadeOut()
    {
        float t = 0f;
        float startAlpha = menuGroup.alpha;

        // disable button clicks
        menuGroup.interactable = false;
        menuGroup.blocksRaycasts = false;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            menuGroup.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeDuration);
            yield return null;
        }

        menuGroup.alpha = 0f;
    }
}
