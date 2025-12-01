// Assets/_Net/SceneDirector.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using Alteruna;

public class SceneDirector : Synchronizable
{
    public static SceneDirector Instance { get; private set; }

    [Header("Refs")]
    public Multiplayer multiplayer;

    [Header("Offline / Fallback")]
    [Tooltip("If true, treat the local player as host when not connected to a room (or while booting).")]
    public bool playOfflineIfNoRoom = true;

    [Header("Synced State (host writes)")]
    [SerializeField] private int _sceneBuildIndex = 1; // 1 = Hub
    [SerializeField] private bool _boss1Cleared;
    [SerializeField] private bool _boss2Cleared;
    [SerializeField] private int _seed;

    // ---------- Host detection ----------
    // Adjust 'Me.IsHost' below if your Alteruna version uses 'IsServer', 'IsRoomOwner', or 'IsMaster'.
    public bool IsHost =>
        (playOfflineIfNoRoom && (multiplayer == null || !multiplayer.InRoom || multiplayer.Me == null))
        || (multiplayer != null && multiplayer.InRoom && multiplayer.Me != null && multiplayer.Me.IsHost);
    // If your SDK uses a different flag, swap '.IsHost' for '.IsServer' / '.IsRoomOwner' / '.IsMaster'.

    public int SceneBuildIndex => _sceneBuildIndex;
    public bool Boss1Cleared => _boss1Cleared;
    public bool Boss2Cleared => _boss2Cleared;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (!multiplayer) multiplayer = FindObjectOfType<Multiplayer>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ----------------- Host API -----------------
    public void HostStartInHub()
    {
        if (!IsHost) return;
        _sceneBuildIndex = 1; // Hub
        Commit();
        LoadSceneLocal(_sceneBuildIndex);
    }

    public void HostSendToBoss1()
    {
        if (!IsHost) return;
        _sceneBuildIndex = 2; Commit(); LoadSceneLocal(_sceneBuildIndex);
    }

    public void HostSendToBoss2()
    {
        if (!IsHost) return;
        _sceneBuildIndex = 3; Commit(); LoadSceneLocal(_sceneBuildIndex);
    }

    public void HostReturnToHubAfterBoss(int bossId)
    {
        if (!IsHost) return;
        if (bossId == 1) _boss1Cleared = true;
        if (bossId == 2) _boss2Cleared = true;
        _sceneBuildIndex = 1;
        Commit();
        LoadSceneLocal(_sceneBuildIndex);
    }

    // ----------------- Sync plumbing -----------------
    public override void AssembleData(Writer w, byte LOD)
    {
        w.Write(_sceneBuildIndex);
        w.Write(_boss1Cleared);
        w.Write(_boss2Cleared);
        w.Write(_seed);
    }

    public override void DisassembleData(Reader r, byte LOD)
    {
        int newScene = r.ReadInt();
        _boss1Cleared = r.ReadBool();
        _boss2Cleared = r.ReadBool();
        _seed = r.ReadInt();

        if (SceneManager.GetActiveScene().buildIndex != newScene)
            LoadSceneLocal(newScene);

        _sceneBuildIndex = newScene;
    }

    void LoadSceneLocal(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
    }

    //void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    //{
    //    var hubUi = FindObjectOfType<HubLootUI>(includeInactive: true);
    //    if (hubUi) hubUi.Refresh(this);
    //}
}
