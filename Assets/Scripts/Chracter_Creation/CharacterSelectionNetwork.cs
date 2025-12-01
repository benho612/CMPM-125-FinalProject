using System.Collections.Generic;
using Alteruna;
using UnityEngine;

/// <summary>
/// Handles 2-player character selection with Alteruna.
/// Uses Animator triggers: Play, Zoom_0, Zoom_1, Zoom_2, Return.
/// </summary>
public class CharacterSelectionNetwork : AttributesSync
{
    [Header("Characters")]
    [Tooltip("Colliders on each character that the player can click. Index = characterId.")]
    public Collider[] characterColliders;   // index = characterId

    [Header("Camera & UI")]
    public Animator cameraAnimator;         // animator on Main Camera
    public GameObject confirmPanel;         // "Are you sure?" panel
    public GameObject waitingText;          // "Waiting..." text
    public GameObject readyButton;          // host Ready button
    public GameObject notInRoomPopup;       // popup saying "You are not in a room"

    [Header("Multiplayer")]
    public Multiplayer multiplayerRef;      // Alteruna Multiplayer
    public string gameSceneName = "KitchenScene";

    // For each character: -1 = free, otherwise user index that owns it
    private int[] _characterOwner;

    // user indices that have CONFIRMED a character (i.e., pressed YES)
    private readonly HashSet<int> _confirmedUsers = new HashSet<int>();

    // Local state
    private int _localSelectedCharacter = -1;
    private int _localUserIndex = -1;
    private bool _localLocked = false;

    private Room _room; // current room


    // ----------------------------------------------------------------
    // Lifecycle
    // ----------------------------------------------------------------

    private void Awake()
    {
        if (multiplayerRef == null)
            multiplayerRef = Object.FindFirstObjectByType<Multiplayer>();

        if (multiplayerRef != null)
        {
            multiplayerRef.OnRoomJoined.AddListener(OnRoomJoined);
            multiplayerRef.OnRoomLeft.AddListener(OnRoomLeft);
        }

        _characterOwner = new int[characterColliders.Length];
        for (int i = 0; i < _characterOwner.Length; i++)
            _characterOwner[i] = -1;
    }

    private void Start()
    {
        SetCharacterColliders(false); // cannot click before we are in a room

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (waitingText != null) waitingText.SetActive(false);
        if (readyButton != null) readyButton.SetActive(false);
        if (notInRoomPopup != null) notInRoomPopup.SetActive(false);
    }

    protected new void OnDestroy()
    {
        if (multiplayerRef != null)
        {
            multiplayerRef.OnRoomJoined.RemoveListener(OnRoomJoined);
            multiplayerRef.OnRoomLeft.RemoveListener(OnRoomLeft);
        }
    }

    // ----------------------------------------------------------------
    // Room events
    // ----------------------------------------------------------------

    private void OnRoomJoined(Multiplayer mp, Room room, User joinedUser)
    {
        _room = room;

        // Local user index is always taken from Multiplayer
        _localUserIndex = mp.GetUser().Index;
        Debug.Log($"[Selection] OnRoomJoined, localUserIndex={_localUserIndex}", this);

        // Only clear state when THIS client is the one that just joined
        if (joinedUser.Index == _localUserIndex)
        {
            _confirmedUsers.Clear();
            ResetOwnership();
            _localLocked = false;

            EnableSelectionPhase();
        }
    }

    private void OnRoomLeft(Multiplayer mp)
    {
        Debug.Log("[Selection] OnRoomLeft ¡V clearing state", this);

        _room = null;
        _confirmedUsers.Clear();
        ResetOwnership();
        _localLocked = false;

        SetCharacterColliders(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (waitingText != null) waitingText.SetActive(false);
        if (readyButton != null) readyButton.SetActive(false);
    }

    private void ResetOwnership()
    {
        for (int i = 0; i < _characterOwner.Length; i++)
            _characterOwner[i] = -1;
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private bool IsInRoom()
    {
        return multiplayerRef != null && multiplayerRef.InRoom;
    }

    private void ShowNotInRoom()
    {
        if (notInRoomPopup != null)
            notInRoomPopup.SetActive(true);
        else
            Debug.LogWarning("Tried to show 'not in room' popup but none is assigned.", this);
    }

    private void SetCharacterColliders(bool enabled)
    {
        foreach (var col in characterColliders)
        {
            if (col != null)
                col.enabled = enabled && !_localLocked;
        }
    }

    /// <summary>Called when selection should start (after joining room / camera Play anim).</summary>
    public void EnableSelectionPhase()
    {
        _localLocked = false;
        Debug.Log($"[Selection] EnableSelectionPhase (local:{_localUserIndex})", this);
        SetCharacterColliders(true);
    }

    public void DisableSelectionPhase()
    {
        Debug.Log($"[Selection] DisableSelectionPhase (local:{_localUserIndex})", this);
        SetCharacterColliders(false);
    }

    // ----------------------------------------------------------------
    // Local input (click / confirm / ready)
    // ----------------------------------------------------------------

    public void OnCharacterClicked(int characterId)
    {
        if (!IsInRoom())
        {
            ShowNotInRoom();
            return;
        }

        if (_localLocked)
        {
            Debug.Log("[Selection] Click ignored: local is locked.", this);
            return;
        }

        if (characterId < 0 || characterId >= _characterOwner.Length)
            return;

        int owner = _characterOwner[characterId];
        if (owner != -1 && owner != _localUserIndex)
        {
            Debug.Log($"[Selection] Character {characterId} already taken by user {owner}.", this);
            return;
        }

        _localSelectedCharacter = characterId;
        Debug.Log($"[Selection] Local clicked character {characterId}", this);

        if (cameraAnimator != null)
            cameraAnimator.SetTrigger($"Zoom_{characterId}");

        if (confirmPanel != null)
            confirmPanel.SetActive(true);
    }

    public void OnConfirmYes()
    {
        if (!IsInRoom())
        {
            ShowNotInRoom();
            return;
        }

        if (_localSelectedCharacter < 0)
        {
            Debug.LogWarning("[Selection] ConfirmYes with no selected character.", this);
            return;
        }

        _localUserIndex = multiplayerRef.GetUser().Index;

        if (_localUserIndex < 0)
        {
            Debug.LogWarning("[Selection] ConfirmYes but localUserIndex not set.", this);
            return;
        }

        Debug.Log($"[Selection] ConfirmYes localUser={_localUserIndex} char={_localSelectedCharacter}", this);

        // remember which character THIS client picked (for loading next scene)
        CharacterDatabase.SelectedCharacterID = _localSelectedCharacter;
        Debug.Log($"[Selection] Setting SelectedCharacterID = {_localSelectedCharacter} for local user {_localUserIndex}", this);

        _localLocked = true;
        SetCharacterColliders(false);

        // Tell all clients (including this one) that this user locked this character
        BroadcastRemoteMethod(nameof(RpcSetCharacterOwner), _localUserIndex, _localSelectedCharacter);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (waitingText != null) waitingText.SetActive(true);
    }

    public void OnConfirmNo()
    {
        if (_localLocked)
        {
            Debug.Log("[Selection] ConfirmNo ignored: local already locked.", this);
            return;
        }

        Debug.Log("[Selection] ConfirmNo ¡V cancelling selection.", this);

        _localSelectedCharacter = -1;

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (waitingText != null) waitingText.SetActive(false);

        if (cameraAnimator != null)
            cameraAnimator.SetTrigger("Return");
    }

    public void OnReadyButtonPressed()
    {
        Debug.Log($"[Selection] Ready button clicked (local:{_localUserIndex})", this);

        if (!IsInRoom())
        {
            ShowNotInRoom();
            return;
        }

        if (!AllPlayersReady())
        {
            Debug.Log("[Selection] Ready ignored: not all players are ready.", this);
            return;
        }

        // Make sure we know our index
        if (_localUserIndex < 0 && Multiplayer != null)
            _localUserIndex = Multiplayer.GetUser().Index;

        bool isHost = _localUserIndex == 0;
        Debug.Log($"[Selection] isHost={isHost}", this);

        // Only host is allowed to start the game
        if (!isHost)
            return;

        // Host tells EVERYONE (including itself) to start the game
        BroadcastRemoteMethod(nameof(RpcStartGame));
    }

    // ----------------------------------------------------------------
    // Networked RPC (AttributesSync)
    // ----------------------------------------------------------------

    /// <summary>
    /// Called on ALL clients when some user locks in a character.
    /// </summary>
    [SynchronizableMethod]
    private void RpcSetCharacterOwner(int userIndex, int characterId)
    {
        if (characterId < 0 || characterId >= _characterOwner.Length)
            return;

        string before = string.Join(",", _confirmedUsers);
        Debug.Log($"[Selection] RpcSetCharacterOwner (local:{_localUserIndex}) " +
                  $"userIndex={userIndex} char={characterId} confirmedBefore=[{before}]", this);

        _characterOwner[characterId] = userIndex;
        _confirmedUsers.Add(userIndex);

        string after = string.Join(",", _confirmedUsers);
        Debug.Log($"[Selection] RpcSetCharacterOwner after update confirmed=[{after}]", this);

        // Disable characters that are taken by someone else
        for (int i = 0; i < characterColliders.Length; i++)
        {
            if (characterColliders[i] == null)
                continue;

            int owner = _characterOwner[i];
            if (owner != -1 && owner != _localUserIndex)
                characterColliders[i].enabled = false;
        }

        UpdateReadyButton();
    }

    [SynchronizableMethod]
    private void RpcStartGame()
    {
        Debug.Log("[Selection] RpcStartGame called, loading game scene.", this);

        // Hide UI / disable selection locally
        DisableSelectionPhase();

        if (multiplayerRef != null)
        {
            // Each client loads the scene on its own Multiplayer
            multiplayerRef.LoadScene(gameSceneName, true);
        }
        else
        {
            Debug.LogError("[Selection] RpcStartGame: multiplayerRef is null!", this);
        }
    }

    private bool AllPlayersReady()
    {
        if (_room == null)
        {
            Debug.Log("[Selection] AllPlayersReady: _room is null", this);
            return false;
        }

        int userCount = _room.Users.Count;        // how many users are in this room
        int readyCount = _confirmedUsers.Count;   // how many have confirmed their character

        bool allReady = userCount > 0 && readyCount >= userCount;

        Debug.Log($"[Selection] AllPlayersReady? users={userCount} ready={readyCount} -> {allReady}", this);

        return allReady;
    }

    private void UpdateReadyButton()
    {
        bool allReady = AllPlayersReady();

        // Only the host sees a Ready button
        bool isHost = (_localUserIndex == 0);

        if (readyButton != null)
            readyButton.SetActive(allReady && isHost);

        if (waitingText != null)
        {
            // Client ALWAYS waits
            if (!isHost)
            {
                waitingText.SetActive(true);
            }
            else
            {
                // Host waits until all are ready, then waiting text hides
                waitingText.SetActive(!allReady);
            }
        }
    }

}
