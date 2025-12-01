// Assets/Bosses/_Shared/BossIntroDirector.cs
using UnityEngine;
using Alteruna;
using System.Collections;

public class BossIntroDirector : MonoBehaviour
{
    [Header("Auto-wired at runtime if left null")]
    public Multiplayer multiplayer;

    [Header("Scene refs")]
    public Transform boss;          // center of the orbit
    public Transform camRig;        // an empty that moves around the boss
    public BossController bossController;

    [Header("Cinematic")]
    public float duration = 5f;
    public float radius = 10f;
    public float height = 3f;

    Camera _cam;
    Transform _camOriginalParent;

    void Awake()
    {
        // Try to auto-wire Multiplayer if not set
        if (multiplayer == null)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            multiplayer = FindFirstObjectByType<Multiplayer>(FindObjectsInactive.Include);
#else
            multiplayer = FindObjectOfType<Multiplayer>();
#endif
        }

        if (bossController == null && boss != null)
            bossController = boss.GetComponent<BossController>();
    }

    void OnEnable() => StartCoroutine(PlayIntro());

    bool IsHost()
    {
        // Treat offline/singleplayer as host too
        if (multiplayer == null || !multiplayer.InRoom || multiplayer.GetUser() == null)
            return true;

        return multiplayer.GetUser().Index == 0; // 0 == host in Alteruna
    }

    IEnumerator PlayIntro()
    {
        // Freeze local input during the intro
        SetPlayersInput(false);

        _cam = Camera.main;
        if (_cam != null && camRig != null)
        {
            _camOriginalParent = _cam.transform.parent;       // remember where it was
            _cam.transform.SetParent(camRig, false);          // ride on the rig
        }

        float t = 0f;
        Vector3 bossLook = boss != null ? boss.position + Vector3.up * 1.5f : Vector3.zero;

        while (t < duration)
        {
            t += Time.deltaTime;

            if (camRig != null && boss != null)
            {
                float ang = Mathf.Lerp(0f, 360f, t / duration);
                Vector3 pos = boss.position
                            + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * radius
                            + Vector3.up * height;

                camRig.position = pos;
                camRig.LookAt(bossLook);
            }

            yield return null;
        }

        // Restore camera to its original parent (player rig)
        if (_cam != null)
        {
            _cam.transform.SetParent(_camOriginalParent, true);
        }

        // Unfreeze local input
        SetPlayersInput(true);

        // Host starts the fight; clients just watch the synced attacks
        if (IsHost() && bossController != null)
        {
            int seed = Random.Range(0, int.MaxValue);
            bossController.StartFight(seed);
        }
    }

    /// <summary>Enable/disable local PlayerController scripts; also toggle cursor.</summary>
    void SetPlayersInput(bool enable)
    {
        // Disable any PlayerController scripts in this scene (local machine only).
        // Your PlayerController already gates logic using Avatar.IsMe, so this is safe.
        var controllers = FindObjectsOfType<PlayerController>(includeInactive: true);
        foreach (var ctrl in controllers)
            ctrl.enabled = enable;

        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }
}
