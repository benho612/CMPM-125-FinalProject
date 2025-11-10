// RelayUI_TMP_Toast_Stop.cs
// Attach to your Canvas (or MainPanel). Wire the fields in the Inspector.
// Requires: one (1) NetworkManager in scene with a UnityTransport component.

using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Core.Environments; // for SetEnvironmentName (optional)
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class RelayUI_TMP_Toast_Stop : MonoBehaviour
{
    [Header("UI")]
    public Button hostButton;
    public Button joinButton;
    public Button stopButton;            // ? new
    public TMP_InputField joinCodeInput;
    public TMP_Text joinCodeLabel;

    [Header("Status Toast")]
    public CanvasGroup statusGroup;
    public TMP_Text statusLabel;
    [SerializeField] float fadeInTime = 0.15f, holdTime = 2.0f, fadeOutTime = 0.25f;

    [Header("UGS (optional)")]
    [Tooltip("Leave blank to use default environment. If you set one (e.g., 'development'), host & client must match.")]
    public string environmentName = "";

    Coroutine toastRoutine;

    async void Awake()
    {
        if (hostButton) hostButton.onClick.AddListener(async () => await HostAsync());
        if (joinButton) joinButton.onClick.AddListener(async () => await JoinAsync(joinCodeInput ? joinCodeInput.text : ""));
        if (stopButton) stopButton.onClick.AddListener(StopSession);

        if (statusGroup) { statusGroup.alpha = 0f; statusGroup.gameObject.SetActive(false); }
        SetInteractable(canHost: true, canJoin: true, canStop: false);

        await EnsureUgsReadyAsync();

        DumpContext("Awake");
        DumpNetworkManager("Awake");
    }

    void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // ---------------- UGS init/auth ----------------
    async Task EnsureUgsReadyAsync()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                var opts = new InitializationOptions();
                if (!string.IsNullOrWhiteSpace(environmentName))
                    opts = opts.SetEnvironmentName(environmentName);

                await UnityServices.InitializeAsync(opts);
                Debug.Log($"[UGS] Initialized. cloudProjectId={Application.cloudProjectId} env={(string.IsNullOrWhiteSpace(environmentName) ? "(default)" : environmentName)}");
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[UGS] Signed in. PlayerId={AuthenticationService.Instance.PlayerId}");
                Toast("Signed in.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[UGS] Init/Auth failed: " + e);
            Toast("UGS init/auth failed: " + e.Message, true);
        }
    }

    // ---------------- Host / Join / Stop ----------------
    public async Task HostAsync()
    {
        var utp = GetTransportOrToast(); if (!utp) return;
        await EnsureUgsReadyAsync();

        try
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsListening) { Toast("Already in a session", true); return; }

            var allocation = await RelayService.Instance.CreateAllocationAsync(1);

            // Host: pass null for hostConnectionData, then isSecure
            utp.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                null,
                true
            );

            ForceAssignTransport(utp);

            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (joinCodeLabel) joinCodeLabel.text = $"Join Code: {code}";
            GUIUtility.systemCopyBuffer = code; // paste straight into your client build
            Toast("Hosting?join code copied");

            bool ok = nm.StartHost();
            Debug.Log($"[Host] StartHost result={ok}");
            if (!ok) { Toast("StartHost failed.", true); return; }

            // Prevent re-host (which would invalidate the code)
            SetInteractable(canHost: false, canJoin: true, canStop: true);
        }
        catch (System.Exception e)
        {
            Toast("Host failed: " + e.Message, true);
            SetInteractable(true, true, false);
        }
    }

    public async Task JoinAsync(string codeRaw)
    {
        var utp = GetTransportOrToast(); if (!utp) return;

        // Sanitize whitespace/newlines and case
        string code = Regex.Replace(codeRaw ?? "", @"\s+", "").ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) { Toast("Enter a join code.", true); return; }

        await EnsureUgsReadyAsync();

        try
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsListening) { Toast("Already in a session", true); return; }

            var join = await RelayService.Instance.JoinAllocationAsync(code);

            utp.SetRelayServerData(
                join.RelayServer.IpV4,
                (ushort)join.RelayServer.Port,
                join.AllocationIdBytes,
                join.Key,
                join.ConnectionData,
                join.HostConnectionData,
                true
            );

            ForceAssignTransport(utp);

            bool ok = nm.StartClient();
            Debug.Log($"[Join] StartClient result={ok}");
            if (!ok) { Toast("StartClient failed.", true); SetInteractable(true, true, false); return; }

            SetInteractable(canHost: false, canJoin: false, canStop: true);
            Toast("Joining?");
        }
        catch (System.Exception e)
        {
            // Most common: "Not Found: join code not found" (stale/invalid code)
            Toast("Join failed: " + e.Message, true);
            SetInteractable(true, true, false);
        }
    }

    public void StopSession()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            nm.Shutdown();
            Debug.Log("[NM] Shutdown called");
        }
        if (joinCodeLabel) joinCodeLabel.text = "Join Code:";
        SetInteractable(true, true, false);
        Toast("Session stopped");
    }

    // ---------------- Helpers ----------------
    UnityTransport GetTransportOrToast()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { Toast("No NetworkManager in scene.", true); Debug.LogError("[NM] Missing NetworkManager"); return null; }
        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null) { Toast("NetworkManager missing UnityTransport.", true); Debug.LogError("[NM] Missing UnityTransport"); return null; }
        return utp;
    }

    void ForceAssignTransport(UnityTransport utp)
    {
        var nm = NetworkManager.Singleton;
        nm.NetworkConfig.NetworkTransport = utp; // bulletproof against StartClient() failing
        Debug.Log($"[NM] Transport assigned: {nm.NetworkConfig.NetworkTransport?.GetType().Name ?? "NULL"}");
    }

    void SetInteractable(bool canHost, bool canJoin, bool canStop)
    {
        if (hostButton) hostButton.interactable = canHost;
        if (joinButton) joinButton.interactable = canJoin;
        if (stopButton) stopButton.interactable = canStop;
    }

    // ---------------- Diagnostics (lightweight) ----------------
    void DumpContext(string tag)
    {
        Debug.Log($"[CTX:{tag}] cloudProjectId={Application.cloudProjectId}, env={(string.IsNullOrWhiteSpace(environmentName) ? "(default)" : environmentName)}");
    }

    void DumpNetworkManager(string tag)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { Debug.Log($"[NM:{tag}] NetworkManager=NULL"); return; }

        var utp = nm.GetComponent<UnityTransport>();
        var transportAssigned = nm.NetworkConfig.NetworkTransport != null
            ? nm.NetworkConfig.NetworkTransport.GetType().Name
            : "NULL";

        var playerPrefab = nm.NetworkConfig.PlayerPrefab;
        bool hasNO = playerPrefab != null && playerPrefab.TryGetComponent<NetworkObject>(out _);

        Debug.Log($"[NM:{tag}] IsServer={nm.IsServer}, IsClient={nm.IsClient}, IsListening={nm.IsListening}");
        Debug.Log($"[NM:{tag}] UnityTransport comp exists: {(utp != null)}");
        Debug.Log($"[NM:{tag}] NetworkConfig.Transport assigned: {transportAssigned}");
        Debug.Log($"[NM:{tag}] PlayerPrefab set: {(playerPrefab != null)} (has NetworkObject: {hasNO})");
        Debug.Log($"[NM:{tag}] ConnectionApproval={nm.NetworkConfig.ConnectionApproval}");
        Debug.Log($"[NM:{tag}] Other NetworkManagers in scene: {FindObjectsOfType<NetworkManager>().Length}");
    }

    void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        Debug.Log($"[NM] Connected: clientId={clientId} (Local={nm.LocalClientId}) IsServer={nm.IsServer} IsClient={nm.IsClient}");
    }

    void OnClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        Debug.Log($"[NM] Disconnected: clientId={clientId}. Reason='{nm.DisconnectReason}'");
        Toast(clientId == nm.LocalClientId ? $"Disconnected: {nm.DisconnectReason}" : $"Client {clientId} disconnected");
        SetInteractable(true, true, false);
    }

    // ---------------- Toast UI ----------------
    void Toast(string message, bool isError = false)
    {
        if (!statusGroup || !statusLabel)
        {
            Debug.Log((isError ? "[ERROR] " : "[INFO] ") + message);
            return;
        }
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message, isError));
    }

    IEnumerator ToastRoutine(string message, bool isError)
    {
        statusLabel.text = message;
        statusLabel.color = isError ? Color.red : Color.white;

        statusGroup.gameObject.SetActive(true);
        for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
        { statusGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInTime); yield return null; }
        statusGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(holdTime);

        for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
        { statusGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOutTime); yield return null; }
        statusGroup.alpha = 0f;
        statusGroup.gameObject.SetActive(false);
        toastRoutine = null;
    }
}
