// RelayUI_TMP_Toast.cs
// Attach to a Canvas. Wire references in the Inspector.
// Requires: NetworkManager + UnityTransport in the scene.
// UI: Buttons (Host/Join), TMP_InputField (join code), TMP_Text (join code label),
//      and a "toast" area: RectTransform with CanvasGroup + TMP_Text for status.

using System.Collections;
using System.Threading.Tasks;
using TMPro;                       // TMP UI
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;              // Buttons

public class RelayUI_TMP_Toast : MonoBehaviour
{
    [Header("UI")]
    public Button hostButton;
    public Button joinButton;
    public TMP_InputField joinCodeInput;
    public TMP_Text joinCodeLabel;

    [Header("Status Toast (single message that fades)")]
    public CanvasGroup statusGroup;   // put this on the toast container
    public TMP_Text statusLabel;      // the text inside the toast
    [SerializeField] float fadeInTime = 0.15f;
    [SerializeField] float holdTime = 2.0f;
    [SerializeField] float fadeOutTime = 0.25f;

    [Header("UGS (optional)")]
    public string environmentName = ""; // leave empty to use default

    Coroutine toastRoutine;

    async void Awake()
    {
        if (hostButton) hostButton.onClick.AddListener(async () => await HostAsync());
        if (joinButton) joinButton.onClick.AddListener(async () => await JoinAsync(joinCodeInput ? joinCodeInput.text.Trim() : ""));

        // Hide toast on start
        if (statusGroup) { statusGroup.alpha = 0f; statusGroup.gameObject.SetActive(false); }
        Debug.Log("cloudProjectId: " + Application.cloudProjectId);
        await EnsureUgsReadyAsync();
    }

    // ---------- UGS init/auth ----------
    async Task EnsureUgsReadyAsync()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                if (!string.IsNullOrWhiteSpace(environmentName))
                    await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName(environmentName));
                else
                    await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Toast($"Signed in. PlayerId: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (System.Exception e)
        {
            Toast("UGS init/auth failed: " + e.Message, true);
        }
    }

    // ---------- Helpers ----------
    UnityTransport GetTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { Toast("No NetworkManager in scene.", true); return null; }
        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null) { Toast("NetworkManager is missing UnityTransport.", true); return null; }
        return utp;
    }

    void ForceAssignTransport(UnityTransport utp)
    {
        // Bulletproof against “Start client failed” due to missing assignment
        NetworkManager.Singleton.NetworkConfig.NetworkTransport = utp;
    }

    // ---------- Host ----------
    public async Task HostAsync()
    {
        var utp = GetTransport(); if (!utp) return;
        await EnsureUgsReadyAsync();

        try
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(1);

            // Your UTP version expects hostConnectionData BEFORE isSecure. Host supplies null.
            utp.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                null,  // hostConnectionData
                true   // DTLS
            );

            ForceAssignTransport(utp);
            if (NetworkManager.Singleton.IsListening) return;

            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (joinCodeLabel) joinCodeLabel.text = $"Join Code: {code}";
            GUIUtility.systemCopyBuffer = code; // quick copy for your client build
            Toast("Relay host ready");

            if (!NetworkManager.Singleton.StartHost())
                Toast("StartHost failed.", true);
        }
        catch (System.Exception e)
        {
            Toast("Host failed: " + e.Message, true);
        }
    }

    // ---------- Join ----------
    public async Task JoinAsync(string code)
    {
        var utp = GetTransport(); if (!utp) return;

        code = (code ?? "").Trim();
        if (string.IsNullOrEmpty(code)) { Toast("Enter a join code.", true); return; }

        await EnsureUgsReadyAsync();

        try
        {
            var join = await RelayService.Instance.JoinAllocationAsync(code);

            utp.SetRelayServerData(
                join.RelayServer.IpV4,
                (ushort)join.RelayServer.Port,
                join.AllocationIdBytes,
                join.Key,
                join.ConnectionData,
                join.HostConnectionData, // client must provide this
                true
            );

            ForceAssignTransport(utp);
            if (NetworkManager.Singleton.IsListening) return;

            if (!NetworkManager.Singleton.StartClient())
                Toast("StartClient failed.", true);
            else
                Toast("Joining…");
        }
        catch (System.Exception e)
        {
            Toast("Join failed: " + e.Message, true);
        }
    }

    // ---------- Toast UI (single message, fade in/out) ----------
    void Toast(string message, bool isError = false)
    {
        if (!statusGroup || !statusLabel) { Debug.Log((isError ? "[ERROR] " : "[INFO] ") + message); return; }

        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message, isError));
    }

    IEnumerator ToastRoutine(string message, bool isError)
    {
        statusLabel.text = message;
        statusLabel.color = isError ? Color.red : Color.white;

        statusGroup.gameObject.SetActive(true);

        // fade in
        for (float t = 0f; t < fadeInTime; t += Time.unscaledDeltaTime)
        {
            statusGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInTime);
            yield return null;
        }
        statusGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(holdTime);

        // fade out
        for (float t = 0f; t < fadeOutTime; t += Time.unscaledDeltaTime)
        {
            statusGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOutTime);
            yield return null;
        }
        statusGroup.alpha = 0f;
        statusGroup.gameObject.SetActive(false);
        toastRoutine = null;
    }
}
