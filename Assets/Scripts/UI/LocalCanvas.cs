using UnityEngine;
using Alteruna;

public class LocalCanvas : MonoBehaviour
{
    private Alteruna.Avatar avatar;


    void Start()
    {
        avatar = FindObjectOfType<Alteruna.Avatar>();
        if (avatar == null)
        {
            Debug.LogWarning("No Avatar found in scene.");
            return;
        }

        if (!avatar.IsMe)
        {
            gameObject.SetActive(false);
        }
    }
}
