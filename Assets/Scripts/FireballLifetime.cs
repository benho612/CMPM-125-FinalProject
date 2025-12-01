using UnityEngine;

/// <summary>
/// Simple lifetime component: destroys the fireball after 'lifeTime' seconds.
/// </summary>
public class FireballLifetime : MonoBehaviour
{
    [Tooltip("Seconds before this projectile is destroyed.")]
    public float lifeTime = 3f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}
