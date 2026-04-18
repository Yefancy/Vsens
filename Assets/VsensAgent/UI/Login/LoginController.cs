using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VsensAgent.Network;

public class LoginController : MonoBehaviour
{
    public GameObject loginPanel;
    public GameObject workspacePanel;
    public WsClient wsClient; 
    public TMP_InputField serverInput;
    public TMP_InputField usernameInput;
    public TMP_InputField usInput;
    public Button loginButton;
    public TMP_Text loginButtonText;
    public TMP_Text statusText;

    private bool isConnecting => wsClient?.IsConnecting ?? false;
    
    void OnEnable()
    {
        loginButton.onClick.AddListener(OnLoginButtonClicked);
        SetLoginStateIdle();
    }

    void OnDisable()
    {
        loginButton.onClick.RemoveListener(OnLoginButtonClicked);
    }

    private void OnLoginButtonClicked()
    {
        if (isConnecting)
            return;

        SetLoginStateConnecting();

        wsClient?.ConnectToServer(
            serverInput != null ? serverInput.text : null,
            usernameInput != null ? usernameInput.text : null,
            usInput != null ? usInput.text : null,
            onConnected: OnConnected,
            onFailed: OnConnectFailed
        );
    }

    private void OnConnected()
    {
        if (statusText != null)
            statusText.text = "Connected";

        if (loginPanel != null)
            loginPanel.SetActive(false);

        if (workspacePanel != null)
            workspacePanel.SetActive(true);
    }

    private void OnConnectFailed(string error)
    {
        Debug.LogError("[Login] Connect failed: " + error);
        SetLoginStateIdle(error);
    }

    private void SetLoginStateConnecting()
    {
        if (loginButton != null)
            loginButton.interactable = false;

        if (serverInput != null)
            serverInput.interactable = false;

        if (usernameInput != null)
            usernameInput.interactable = false;

        if (usInput != null)
            usInput.interactable = false;

        if (loginButtonText != null)
            loginButtonText.text = "Connecting...";

        if (statusText != null)
            statusText.text = "Connecting...";

        if (loginPanel != null)
            loginPanel.SetActive(true);

        if (workspacePanel != null)
            workspacePanel.SetActive(false);
    }

    private void SetLoginStateIdle(string errorMessage = null)
    {
        if (loginButton != null)
            loginButton.interactable = true;

        if (serverInput != null)
            serverInput.interactable = true;

        if (usernameInput != null)
            usernameInput.interactable = true;

        if (usInput != null)
            usInput.interactable = true;

        if (loginButtonText != null)
            loginButtonText.text = "Login";

        if (statusText != null)
            statusText.text = string.IsNullOrEmpty(errorMessage) ? "" : $"Connect failed: {errorMessage}";

        if (loginPanel != null)
            loginPanel.SetActive(true);

        if (workspacePanel != null)
            workspacePanel.SetActive(false);
    }
}
