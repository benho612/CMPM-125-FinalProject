using Alteruna;
using UnityEngine;

/// <summary>
/// On the Avatar prefab, picks which visual model to show based on a networked characterId.
/// Local player sets it from CharacterDatabase.SelectedCharacterID; others receive it over the network.
/// </summary>
public class PlayerCharacterVisual : AttributesSync
{
    [Header("References")]
    public Alteruna.Avatar avatar;                   // Alteruna Avatar on this GameObject
    public GameObject[] characterModels;    // one child per character ID, same order as in selection

    [SynchronizableField]
    private int characterId = -1;

    private int _lastAppliedId = int.MinValue;

    private void Awake()
    {
        if (avatar == null)
            avatar = GetComponent<Alteruna.Avatar>();
    }

    private void Start()
    {
        if (avatar != null && avatar.IsMe) // this is *my* avatar
        {
            // Use the choice we saved in the selection scene
            characterId = CharacterDatabase.SelectedCharacterID;

            // Share it with everyone over the network
            Commit();
        }

        ApplyVisual(); // apply whatever value we currently have
    }

    private void Update()
    {
        // If the synced characterId changes (locally or from the network),
        // re-apply the correct model.
        if (characterId != _lastAppliedId)
        {
            _lastAppliedId = characterId;
            ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        if (characterModels == null || characterModels.Length == 0)
            return;

        for (int i = 0; i < characterModels.Length; i++)
        {
            if (characterModels[i] == null) continue;

            // show only the selected one
            characterModels[i].SetActive(i == characterId);
        }
    }
}
