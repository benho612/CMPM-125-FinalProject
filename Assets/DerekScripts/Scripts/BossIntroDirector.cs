// Assets/Bosses/_Shared/BossIntroDirector.cs
using UnityEngine;
using Alteruna;
using System.Collections;

public class BossIntroDirector : MonoBehaviour
{
    public Multiplayer multiplayer;
    public Transform boss;
    public Transform camRig;          // a separate Camera parent to orbit with
    public float duration = 5f;
    public float radius = 10f;
    public float height = 3f;
    public BossController bossController;

    void Start() { StartCoroutine(PlayIntro()); }

    IEnumerator PlayIntro()
    {
        // Freeze player input: simplest is to disable your player controller scripts for 5s.
        SetPlayersInput(false);

        var cam = Camera.main;
        if (cam && camRig) { cam.transform.SetParent(camRig, false); }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ang = Mathf.Lerp(0, 360f, t / duration);
            Vector3 pos = boss.position + Quaternion.Euler(0, ang, 0) * Vector3.forward * radius + Vector3.up * height;
            if (camRig) { camRig.position = pos; camRig.LookAt(boss.position + Vector3.up * 1.5f); }
            yield return null;
        }

        // Return camera to players (FPS)
        if (cam) { cam.transform.SetParent(null, true); }

        SetPlayersInput(true);

        // Start fight with a shared seed (host picks; clients mirror visuals)
        int seed = Random.Range(0, int.MaxValue);
        bossController.StartFight(seed); // host will run the loop; clients just show VFX
    }

    // inside BossIntroDirector
    void SetPlayersInput(bool enable)
    {
        foreach (var c in FindObjectsOfType<CharacterController>())
        {
            var ctrl = c.GetComponent<PlayerController>();
            if (ctrl) ctrl.enabled = enable;
        }
        // cursor lock optional
        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }
}

