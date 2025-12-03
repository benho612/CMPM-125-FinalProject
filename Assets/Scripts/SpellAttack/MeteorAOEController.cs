using UnityEngine;
using System.Collections;

public class MeteorAOEController : MonoBehaviour
{
    [Header("Spawn Area")]
    [Tooltip("Radius of the AOE circle (match your VFX size).")]
    public float radius = 3f;

    [Header("Meteors")]
    [Tooltip("Physical meteor prefab with mesh + collider + MeteorProjectile.")]
    public GameObject meteorPrefab;

    [Tooltip("Number of meteors to spawn inside the circle.")]
    public int meteorCount = 5;

    [Tooltip("Height above the ground where meteors spawn.")]
    public float spawnHeight = 12f;

    [Tooltip("Delay before first meteor starts falling (seconds).")]
    public float firstImpactDelay = 0.5f;

    [Tooltip("Delay between each meteor spawn (seconds).")]
    public float betweenMeteorDelay = 0.1f;

    [Header("Lifetime")]
    [Tooltip("Lifetime of this AOE object (destroy after meteors are done).")]
    public float aoeLifetime = 5f;

    [Header("Audio")]
    public AudioSource aoeSource;     // optional, can be auto-created
    public AudioClip aoeLoopClip;

    public float aoeVolume = 1f;
    void Start()
    {
        StartCoroutine(SpawnMeteorsRoutine());
        SetupAndPlayAudio();
        Destroy(gameObject, aoeLifetime);
    }

    IEnumerator SpawnMeteorsRoutine()
    {
        if (meteorPrefab == null)
        {
            Debug.LogWarning("MeteorAOEController: meteorPrefab is not assigned.", this);
            yield break;
        }

        // small telegraph delay so players see the circle before meteors fall
        yield return new WaitForSeconds(firstImpactDelay);

        for (int i = 0; i < meteorCount; i++)
        {
            // pick a random point inside the circle on XZ plane
            Vector2 random = Random.insideUnitCircle * radius;
            Vector3 groundPos = transform.position + new Vector3(random.x, 0f, random.y);
            Vector3 spawnPos = groundPos + Vector3.up * spawnHeight;

            // spawn meteor looking downwards and tell it where to land
            GameObject meteor = Instantiate(
                meteorPrefab,
                spawnPos,
                Quaternion.LookRotation(Vector3.down, Vector3.forward)
            );

            MeteorProjectile proj = meteor.GetComponent<MeteorProjectile>();
            if (proj != null)
            {
                proj.SetTarget(groundPos);
            }

            yield return new WaitForSeconds(betweenMeteorDelay);
        }
    }

    void SetupAndPlayAudio()
    {
        if (aoeLoopClip == null)
            return;

        if (aoeSource == null)
        {
            aoeSource = gameObject.AddComponent<AudioSource>();
        }

        aoeSource.clip = aoeLoopClip;
        aoeSource.volume = aoeVolume;
        aoeSource.loop = true;
        aoeSource.playOnAwake = false;
        aoeSource.spatialBlend = 0f; // or 1f if you want 3D localized sound

        aoeSource.Play();
    }
}
