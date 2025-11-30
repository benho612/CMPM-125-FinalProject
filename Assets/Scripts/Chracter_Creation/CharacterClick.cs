using UnityEngine;

public class CharacterClick : MonoBehaviour
{
    public int characterId;
    public CharacterSelectionNetwork network;

    private void OnMouseDown()
    {
        network.OnCharacterClicked(characterId);
    }
}
