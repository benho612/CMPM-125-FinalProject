using Alteruna;
using UnityEngine;

/// <summary>
/// Chooses which character model (child prefab) to show based on a networked characterId,
/// and enables the AudioListener only on the local player's camera.
/// </summary>
public class PlayerCharacterVisual : AttributesSync
{
    [Header("References")]
    public Alteruna.Avatar avatar;                   // Alteruna Avatar on this GameObject
    public GameObject[] characterModels;    // index must match selection IDs
    public Camera playerCamera;             // optional: will auto-find if left null

    [SynchronizableField]
    public int characterId = -1;            // synced value

    private int _lastAppliedId = int.MinValue;

    private void Awake()
    {
        if (avatar == null)
            avatar = GetComponent<Alteruna.Avatar>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        if (avatar == null)
        {
            Debug.LogError("[PCV] Avatar component missing!", this);
            return;
        }

        // ----- AUDIO LISTENER: local only -----
        if (playerCamera != null)
        {
            var listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = avatar.IsMe;
        }

        if (avatar.IsMe)
        {
            int chosen = CharacterDatabase.SelectedCharacterID;
            if (chosen < 0)
            {
                // Fallback so you at least see *some* model
                Debug.LogWarning("[PCV] LOCAL has SelectedCharacterID = -1, defaulting to 0.", this);
                chosen = 0;
            }

            characterId = chosen;
            Debug.Log($"[PCV] LOCAL sets characterId = {characterId}", this);
            Commit();   // sync to others
        }

        ApplyVisual();
    }

    private void Update()
    {
        if (characterId != _lastAppliedId)
        {
            ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        _lastAppliedId = characterId;

        if (characterModels == null || characterModels.Length == 0)
        {
            Debug.LogWarning("[PCV] No character models assigned.", this);
            return;
        }

        // If for some reason we never got a valid id, just do nothing (all off)
        if (characterId < 0 || characterId >= characterModels.Length)
        {
            Debug.Log($"[PCV] characterId {characterId} is out of range on {(avatar.IsMe ? "LOCAL" : "REMOTE")} avatar.", this);
            // Optional: default to 0 instead of hiding everything
            // characterId = 0;
            // _lastAppliedId = characterId;
            // (and then enable index 0)
        }

        Debug.Log($"[PCV] Applying visual id={characterId} (avatar.IsMe={avatar.IsMe})", this);

        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] == null) continue;
            characterModels[i].SetActive(i == characterId);
        }
    }
}
