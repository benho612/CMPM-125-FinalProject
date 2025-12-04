using UnityEngine;
using System.Collections.Generic;

public class IngredientSpawner : MonoBehaviour
{
    [Header("Ingredient Prefabs")]
    public GameObject[] ingredientPrefabs;

    [Header("Spawn Settings")]
    public float spawnIntervalMin = 0.25f;
    public float spawnIntervalMax = 0.5f;

    public float spawnRadius = 4f;       // Area around the player where ingredients appear
    public float forwardForce = 12f;     // Average force toward the camera
    public float chaosAngle = 25f;       // How much the direction can deviate
    public float upwardForce = 4f;       // Push upward a bit
    public float lifetime = 3f;

    private bool spawning = false;
    private Transform targetCamera;       // Player FPS camera
    private List<GameObject> activeIngredients = new List<GameObject>();
    private IngredientChopController ownerController;


    public void Begin(Transform cam, IngredientChopController owner)
    {
        targetCamera = cam;
        ownerController = owner;
        spawning = true;
        StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        spawning = false;
    }

    public void CleanupIngredients()
    {
        foreach (var obj in activeIngredients)
        {
            if (obj != null)
                Destroy(obj);
        }
        activeIngredients.Clear();
    }

    private IEnumerator<WaitForSeconds> SpawnLoop()
    {
        while (spawning)
        {
            SpawnOneIngredient();

            float interval = Random.Range(spawnIntervalMin, spawnIntervalMax);
            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnOneIngredient()
    {
        if (ingredientPrefabs.Length == 0 || targetCamera == null)
            return;

        GameObject prefab = ingredientPrefabs[Random.Range(0, ingredientPrefabs.Length)];

        // RANDOMIZE where it spawns around the player
        Vector3 randomOffset = 
            Random.insideUnitSphere * spawnRadius;

        randomOffset.y = Mathf.Abs(randomOffset.y); // keep it above ground

        Vector3 spawnPos = targetCamera.position + targetCamera.transform.TransformDirection(new Vector3(randomOffset.x, randomOffset.y, 6f));

        GameObject ingredient = Instantiate(prefab, spawnPos, Quaternion.identity);
        activeIngredients.Add(ingredient);

        Rigidbody rb = ingredient.GetComponent<Rigidbody>();
        if (rb == null)
            rb = ingredient.AddComponent<Rigidbody>();

        rb.useGravity = true;

        var slice = ingredient.GetComponent<IngredientSliceable>();
        if (slice != null)
            slice.SetOwner(ownerController);



        // BASE direction toward camera
        Vector3 dir = (targetCamera.position - spawnPos).normalized;

        // Add chaos by rotating the direction randomly
        dir = Quaternion.AngleAxis(Random.Range(-chaosAngle, chaosAngle), Vector3.up) * dir;
        dir = Quaternion.AngleAxis(Random.Range(-chaosAngle, chaosAngle), Vector3.right) * dir;

        // Add forward + upward force
        Vector3 force = 
            dir * forwardForce + 
            Vector3.up * upwardForce;

        rb.AddForce(force, ForceMode.Impulse);

        // Auto destroy
        Destroy(ingredient, lifetime);
    }
}
