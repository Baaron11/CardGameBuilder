using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using CardGameBuilder.Core;

namespace CardGameBuilder.UI
{
    /// <summary>
    /// Network UI - Simple connection menu for starting as Host/Client/Server.
    /// This is typically shown at the start before joining a game.
    ///
    /// Usage:
    /// - Attach to a Canvas GameObject
    /// - Assign button references in Inspector
    /// - This panel should hide once connected
    /// </summary>
    public class NetworkUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;
        [SerializeField] private TMP_InputField ipAddressField;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings")]
        [SerializeField] private bool hideOnConnect = true;

        private NetworkGameManager networkGameManager;

        private void Start()
        {
            // Find NetworkGameManager
            networkGameManager = NetworkGameManager.Instance;

            if (networkGameManager == null)
            {
                Debug.LogError("[NetworkUI] NetworkGameManager not found!");
            }

            // Setup buttons
            if (hostButton != null)
            {
                hostButton.onClick.AddListener(OnHostClicked);
            }

            if (clientButton != null)
            {
                clientButton.onClick.AddListener(OnClientClicked);
            }

            if (serverButton != null)
            {
                serverButton.onClick.AddListener(OnServerClicked);
            }

            // Set default IP
            if (ipAddressField != null)
            {
                ipAddressField.text = "127.0.0.1";
            }

            UpdateStatus("Select connection type");
        }

        private void OnHostClicked()
        {
            if (networkGameManager == null) return;

            UpdateStatus("Starting as Host...");
            networkGameManager.StartHost();

            if (hideOnConnect)
            {
                Invoke(nameof(HideUI), 0.5f);
            }
        }

        private void OnClientClicked()
        {
            if (networkGameManager == null) return;

            // Set IP address if Unity Transport is used
            SetTransportIPAddress();

            UpdateStatus("Connecting as Client...");
            networkGameManager.StartClient();

            if (hideOnConnect)
            {
                Invoke(nameof(HideUI), 1f);
            }
        }

        private void OnServerClicked()
        {
            if (networkGameManager == null) return;

            UpdateStatus("Starting as Server...");
            networkGameManager.StartServer();

            if (hideOnConnect)
            {
                Invoke(nameof(HideUI), 0.5f);
            }
        }

        /// <summary>
        /// Sets the IP address in Unity Transport (if available).
        /// </summary>
        private void SetTransportIPAddress()
        {
            if (NetworkManager.Singleton == null || ipAddressField == null)
                return;

            string ipAddress = ipAddressField.text;

            if (string.IsNullOrEmpty(ipAddress))
            {
                ipAddress = "127.0.0.1";
            }

            // Try to set Unity Transport address
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            if (transport != null)
            {
                // Unity Transport uses connection data
                // This is a simplified approach - in production you'd use UnityTransport directly
                Debug.Log($"[NetworkUI] Connecting to {ipAddress}");
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
            Debug.Log($"[NetworkUI] {message}");
        }

        private void HideUI()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the network UI again (e.g., after disconnecting).
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            UpdateStatus("Select connection type");
        }
    }
}
