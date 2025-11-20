using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardGameBuilder
{
    /// <summary>
    /// UI controller for the Board scene (host/shared table view).
    /// Displays Host/Stop buttons and subscribes to public game state.
    /// </summary>
    public class BoardUI : MonoBehaviour
    {
        [Header("Network Controls")]
        [SerializeField] private Button hostButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Game Display")]
        [SerializeField] private TextMeshProUGUI[] seatTexts; // 4 seats
        [SerializeField] private TextMeshProUGUI deckCountText;
        [SerializeField] private TextMeshProUGUI discardCountText;
        [SerializeField] private TextMeshProUGUI topDiscardCardText;

        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI infoText;

        private void Start()
        {
            // Wire up buttons
            if (hostButton != null)
            {
                hostButton.onClick.AddListener(OnHostButtonClicked);
                hostButton.interactable = true;
            }

            if (stopButton != null)
            {
                stopButton.onClick.AddListener(OnStopButtonClicked);
                stopButton.interactable = false;
            }

            UpdateStatusText("Not connected");
            ClearGameDisplay();
        }

        private void OnEnable()
        {
            // Subscribe to game manager events
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPublicStateChanged += HandlePublicStateChanged;
                NetworkGameManager.Instance.OnErrorMessage += HandleErrorMessage;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from game manager events
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPublicStateChanged -= HandlePublicStateChanged;
                NetworkGameManager.Instance.OnErrorMessage -= HandleErrorMessage;
            }
        }

        private void Update()
        {
            // Dynamically subscribe if game manager spawns after UI
            if (NetworkGameManager.Instance != null)
            {
                if (!IsSubscribed())
                {
                    NetworkGameManager.Instance.OnPublicStateChanged += HandlePublicStateChanged;
                    NetworkGameManager.Instance.OnErrorMessage += HandleErrorMessage;
                }
            }
        }

        private bool IsSubscribed()
        {
            if (NetworkGameManager.Instance == null) return false;
            // Simple check - in production you'd track this more carefully
            return true;
        }

        #region Button Handlers

        /// <summary>
        /// Start the server/host when Host button is clicked.
        /// </summary>
        private void OnHostButtonClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                LogError("NetworkManager not found in scene!");
                return;
            }

            bool success = NetworkManager.Singleton.StartHost();

            if (success)
            {
                UpdateStatusText("Hosting...");
                hostButton.interactable = false;
                stopButton.interactable = true;

                // Display local IP for clients to connect
                string localIP = GetLocalIPAddress();
                LogInfo($"Server started. Clients should connect to: {localIP}");
            }
            else
            {
                LogError("Failed to start host");
            }
        }

        /// <summary>
        /// Stop the server/host when Stop button is clicked.
        /// </summary>
        private void OnStopButtonClicked()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.Shutdown();

            UpdateStatusText("Not connected");
            hostButton.interactable = true;
            stopButton.interactable = false;

            ClearGameDisplay();
            LogInfo("Server stopped");
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle public game state updates from NetworkGameManager.
        /// </summary>
        private void HandlePublicStateChanged(PublicGameState state)
        {
            // Update seat displays
            if (seatTexts != null && seatTexts.Length >= state.seatOccupied.Length)
            {
                for (int i = 0; i < state.seatOccupied.Length; i++)
                {
                    if (seatTexts[i] != null)
                    {
                        string status = state.seatOccupied[i] ? "OCCUPIED" : "EMPTY";
                        string playerName = state.seatPlayerNames[i];
                        seatTexts[i].text = $"Seat {i}: {status}\n{playerName}";
                        seatTexts[i].color = state.seatOccupied[i] ? Color.green : Color.gray;
                    }
                }
            }

            // Update deck count
            if (deckCountText != null)
            {
                deckCountText.text = $"Deck: {state.deckCount} cards";
            }

            // Update discard pile
            if (discardCountText != null)
            {
                discardCountText.text = $"Discard: {state.discardCount} cards";
            }

            // Update top discard card
            if (topDiscardCardText != null)
            {
                if (state.topDiscardCard >= 0)
                {
                    string cardDisplay = NetworkGameManager.GetCardDisplay(state.topDiscardCard);
                    topDiscardCardText.text = $"Top Card: {cardDisplay}";
                }
                else
                {
                    topDiscardCardText.text = "Top Card: None";
                }
            }

            UpdateStatusText($"Connected - {state.deckCount} cards in deck");
        }

        /// <summary>
        /// Handle error messages from NetworkGameManager.
        /// </summary>
        private void HandleErrorMessage(string message)
        {
            LogError(message);
        }

        #endregion

        #region Helper Methods

        private void UpdateStatusText(string status)
        {
            if (statusText != null)
            {
                statusText.text = $"Status: {status}";
            }
        }

        private void ClearGameDisplay()
        {
            if (seatTexts != null)
            {
                foreach (var seatText in seatTexts)
                {
                    if (seatText != null)
                    {
                        seatText.text = "Seat: EMPTY";
                        seatText.color = Color.gray;
                    }
                }
            }

            if (deckCountText != null) deckCountText.text = "Deck: 0 cards";
            if (discardCountText != null) discardCountText.text = "Discard: 0 cards";
            if (topDiscardCardText != null) topDiscardCardText.text = "Top Card: None";
        }

        private void LogInfo(string message)
        {
            Debug.Log($"[BoardUI] {message}");
            if (infoText != null)
            {
                infoText.text = $"[INFO] {message}";
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[BoardUI] {message}");
            if (infoText != null)
            {
                infoText.text = $"<color=red>[ERROR] {message}</color>";
            }
        }

        /// <summary>
        /// Get the local IP address for LAN connections.
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        #endregion
    }
}
