// Assets/Bosses/_Shared/BossFightAutoStart.cs
using System.Collections;
using UnityEngine;
using Alteruna;

[DefaultExecutionOrder(50)]
public class BossFightAutoStart : MonoBehaviour
{
    [Header("Auto-wired if left null")]
    public Multiplayer multiplayer;
    public BossController bossController;   // assign your EmberOvenWarden's BossController here

    [Header("Start conditions")]
    public bool waitForBothPlayers = true;
    public int expectedPlayers = 2;
    public float maxWaitForPlayers = 8f;   // safety timeout
    public float startDelay = 1f;          // small delay after everyone is in

    void Awake()
    {
        if (bossController == null)
            bossController = GetComponentInChildren<BossController>(true);

        if (multiplayer == null)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            multiplayer = FindFirstObjectByType<Multiplayer>(FindObjectsInactive.Include);
#else
            multiplayer = FindObjectOfType<Multiplayer>();
#endif
        }
    }

    void OnEnable() => StartCoroutine(AutoStart());

    IEnumerator AutoStart()
    {
        // 1) Wait for Multiplayer to exist (or treat as offline)
        while (multiplayer == null)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            multiplayer = FindFirstObjectByType<Multiplayer>(FindObjectsInactive.Include);
#else
            multiplayer = FindObjectOfType<Multiplayer>();
#endif
            yield return null;
        }

        // 2) Host check (treat offline as host)
        bool isHost = (multiplayer == null || !multiplayer.InRoom || multiplayer.GetUser() == null)
                      || (multiplayer.GetUser().Index == 0);
        if (!isHost)
            yield break; // clients do nothing; they’ll just see the synced attacks

        // 3) Optional: wait until both avatars are present in this scene
        if (waitForBothPlayers)
        {
            float t = 0f;
            while (t < maxWaitForPlayers)
            {
                var avatars = FindObjectsByType<Alteruna.Avatar>(FindObjectsSortMode.None);
                if (avatars != null && avatars.Length >= expectedPlayers)
                    break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        // 4) Small grace delay, then start
        yield return new WaitForSeconds(startDelay);

        if (bossController == null)
        {
            Debug.LogWarning("[BossAutoStart] No BossController found; cannot start fight.");
            yield break;
        }

        int seed = Random.Range(0, int.MaxValue);
        Debug.Log($"[BossAutoStart] Host starting fight with seed {seed}");
        bossController.StartFight(seed);   // your BossController should flip fightActive & begin its loop
    }
}
