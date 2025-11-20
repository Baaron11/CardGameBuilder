using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardGameBuilder
{
    /// <summary>
    /// UI controller for the Controller scene (player's phone/remote controller).
    /// Allows joining via IP, claiming seats, and reordering private hand.
    /// </summary>
    public class ControllerUI : MonoBehaviour
    {
        [Header("Connection Controls")]
        [SerializeField] private TMP_InputField hostIPInputField;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private TextMeshProUGUI connectionStatusText;

        [Header("Seat Controls")]
        [SerializeField] private TMP_InputField seatInputField;
        [SerializeField] private Button claimSeatButton;
        [SerializeField] private Button leaveSeatButton;
        [SerializeField] private TextMeshProUGUI seatStatusText;

        [Header("Hand Reorder Controls")]
        [SerializeField] private TMP_InputField fromIndexInputField;
        [SerializeField] private TMP_InputField toIndexInputField;
        [SerializeField] private Button reorderButton;

        [Header("Hand Display")]
        [SerializeField] private TextMeshProUGUI handDisplayText;
        [SerializeField] private Transform handCardsContainer;
        [SerializeField] private GameObject cardPrefab; // Optional: for visual card display

        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI infoText;

        private List<int> myHand = new List<int>();
        private bool isConnected = false;
        private bool hasSeat = false;

        private void Start()
        {
            // Wire up connection buttons
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinButtonClicked);
                joinButton.interactable = true;
            }

            if (leaveButton != null)
            {
                leaveButton.onClick.AddListener(OnLeaveButtonClicked);
                leaveButton.interactable = false;
            }

            // Wire up seat buttons
            if (claimSeatButton != null)
            {
                claimSeatButton.onClick.AddListener(OnClaimSeatButtonClicked);
                claimSeatButton.interactable = false;
            }

            if (leaveSeatButton != null)
            {
                leaveSeatButton.onClick.AddListener(OnLeaveSeatButtonClicked);
                leaveSeatButton.interactable = false;
            }

            // Wire up reorder button
            if (reorderButton != null)
            {
                reorderButton.onClick.AddListener(OnReorderButtonClicked);
                reorderButton.interactable = false;
            }

            // Set default IP
            if (hostIPInputField != null)
            {
                hostIPInputField.text = "127.0.0.1";
            }

            UpdateConnectionStatus("Not connected");
            UpdateSeatStatus("No seat");
            UpdateHandDisplay();
        }

        private void OnEnable()
        {
            // Subscribe to game manager events
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPrivateHandChanged += HandlePrivateHandChanged;
                NetworkGameManager.Instance.OnErrorMessage += HandleErrorMessage;
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from game manager events
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.OnPrivateHandChanged -= HandlePrivateHandChanged;
                NetworkGameManager.Instance.OnErrorMessage -= HandleErrorMessage;
            }
        }

        private void Update()
        {
            // Dynamically subscribe if game manager spawns after UI
            if (NetworkGameManager.Instance != null && isConnected)
            {
                if (!IsSubscribed())
                {
                    NetworkGameManager.Instance.OnPrivateHandChanged += HandlePrivateHandChanged;
                    NetworkGameManager.Instance.OnErrorMessage += HandleErrorMessage;
                }
            }

            // Update connection state
            if (NetworkManager.Singleton != null)
            {
                bool nowConnected = NetworkManager.Singleton.IsClient && NetworkManager.Singleton.IsConnectedClient;
                if (nowConnected != isConnected)
                {
                    isConnected = nowConnected;
                    OnConnectionStateChanged();
                }
            }
        }

        private bool IsSubscribed()
        {
            if (NetworkGameManager.Instance == null) return false;
            return true; // Simplified check
        }

        #region Connection Handlers

        /// <summary>
        /// Join the server as a client when Join button is clicked.
        /// </summary>
        private void OnJoinButtonClicked()
        {
            if (NetworkManager.Singleton == null)
            {
                LogError("NetworkManager not found in scene!");
                return;
            }

            string hostIP = hostIPInputField != null ? hostIPInputField.text : "127.0.0.1";

            if (string.IsNullOrWhiteSpace(hostIP))
            {
                LogError("Please enter a valid host IP address");
                return;
            }

            // Set the connection address on Unity Transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = hostIP;
                transport.ConnectionData.Port = 7777; // Default port
                LogInfo($"Connecting to {hostIP}:7777...");
            }

            bool success = NetworkManager.Singleton.StartClient();

            if (success)
            {
                joinButton.interactable = false;
                leaveButton.interactable = true;
                UpdateConnectionStatus("Connecting...");
            }
            else
            {
                LogError("Failed to start client");
            }
        }

        /// <summary>
        /// Leave the server when Leave button is clicked.
        /// </summary>
        private void OnLeaveButtonClicked()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.Shutdown();

            joinButton.interactable = true;
            leaveButton.interactable = false;
            claimSeatButton.interactable = false;
            leaveSeatButton.interactable = false;
            reorderButton.interactable = false;

            UpdateConnectionStatus("Not connected");
            UpdateSeatStatus("No seat");
            myHand.Clear();
            UpdateHandDisplay();
            hasSeat = false;

            LogInfo("Disconnected from server");
        }

        private void OnConnectionStateChanged()
        {
            if (isConnected)
            {
                UpdateConnectionStatus("Connected");
                claimSeatButton.interactable = true;
                LogInfo("Successfully connected to server");
            }
            else
            {
                UpdateConnectionStatus("Disconnected");
                claimSeatButton.interactable = false;
                leaveSeatButton.interactable = false;
                reorderButton.interactable = false;
                joinButton.interactable = true;
                leaveButton.interactable = false;
                hasSeat = false;
            }
        }

        #endregion

        #region Seat Handlers

        /// <summary>
        /// Claim a seat when Claim Seat button is clicked.
        /// </summary>
        private void OnClaimSeatButtonClicked()
        {
            if (NetworkGameManager.Instance == null)
            {
                LogError("NetworkGameManager not found!");
                return;
            }

            if (!int.TryParse(seatInputField.text, out int seatIndex))
            {
                LogError("Please enter a valid seat number (0-3)");
                return;
            }

            if (seatIndex < 0 || seatIndex > 3)
            {
                LogError("Seat number must be between 0 and 3");
                return;
            }

            LogInfo($"Requesting to claim seat {seatIndex}...");
            NetworkGameManager.Instance.ClaimSeatServerRpc(seatIndex);
        }

        /// <summary>
        /// Leave current seat when Leave Seat button is clicked.
        /// </summary>
        private void OnLeaveSeatButtonClicked()
        {
            if (NetworkGameManager.Instance == null)
            {
                LogError("NetworkGameManager not found!");
                return;
            }

            LogInfo("Leaving seat...");
            NetworkGameManager.Instance.LeaveSeatServerRpc();

            leaveSeatButton.interactable = false;
            reorderButton.interactable = false;
            claimSeatButton.interactable = true;
            hasSeat = false;
            UpdateSeatStatus("No seat");
        }

        #endregion

        #region Hand Reorder Handlers

        /// <summary>
        /// Reorder hand when Reorder button is clicked.
        /// </summary>
        private void OnReorderButtonClicked()
        {
            if (NetworkGameManager.Instance == null)
            {
                LogError("NetworkGameManager not found!");
                return;
            }

            if (!int.TryParse(fromIndexInputField.text, out int fromIndex))
            {
                LogError("Please enter a valid 'from' index");
                return;
            }

            if (!int.TryParse(toIndexInputField.text, out int toIndex))
            {
                LogError("Please enter a valid 'to' index");
                return;
            }

            if (fromIndex < 0 || fromIndex >= myHand.Count)
            {
                LogError($"'From' index must be between 0 and {myHand.Count - 1}");
                return;
            }

            if (toIndex < 0 || toIndex >= myHand.Count)
            {
                LogError($"'To' index must be between 0 and {myHand.Count - 1}");
                return;
            }

            LogInfo($"Reordering hand: {fromIndex} -> {toIndex}");
            NetworkGameManager.Instance.ReorderHandServerRpc(fromIndex, toIndex);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle private hand updates from NetworkGameManager.
        /// </summary>
        private void HandlePrivateHandChanged(List<int> hand)
        {
            myHand = new List<int>(hand);
            UpdateHandDisplay();

            if (hand.Count > 0 && !hasSeat)
            {
                hasSeat = true;
                leaveSeatButton.interactable = true;
                reorderButton.interactable = true;
                claimSeatButton.interactable = false;
                UpdateSeatStatus("Seated");
                LogInfo($"Received hand with {hand.Count} cards");
            }
            else if (hand.Count == 0 && hasSeat)
            {
                hasSeat = false;
                leaveSeatButton.interactable = false;
                reorderButton.interactable = false;
                claimSeatButton.interactable = true;
                UpdateSeatStatus("No seat");
                LogInfo("Hand cleared");
            }
        }

        /// <summary>
        /// Handle error messages from NetworkGameManager.
        /// </summary>
        private void HandleErrorMessage(string message)
        {
            LogError(message);
        }

        #endregion

        #region UI Updates

        private void UpdateConnectionStatus(string status)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = $"Connection: {status}";
            }
        }

        private void UpdateSeatStatus(string status)
        {
            if (seatStatusText != null)
            {
                seatStatusText.text = $"Seat: {status}";
            }
        }

        private void UpdateHandDisplay()
        {
            if (handDisplayText != null)
            {
                if (myHand.Count == 0)
                {
                    handDisplayText.text = "Hand: (empty)";
                }
                else
                {
                    string handStr = "Hand:\n";
                    for (int i = 0; i < myHand.Count; i++)
                    {
                        string cardDisplay = NetworkGameManager.GetCardDisplay(myHand[i]);
                        handStr += $"[{i}] {cardDisplay}  ";
                        if ((i + 1) % 4 == 0) handStr += "\n"; // Line break every 4 cards
                    }
                    handDisplayText.text = handStr;
                }
            }

            // Optional: Spawn card prefabs in container
            if (handCardsContainer != null && cardPrefab != null)
            {
                // Clear existing cards
                foreach (Transform child in handCardsContainer)
                {
                    Destroy(child.gameObject);
                }

                // Spawn new cards
                foreach (int cardId in myHand)
                {
                    GameObject cardObj = Instantiate(cardPrefab, handCardsContainer);
                    var cardText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (cardText != null)
                    {
                        cardText.text = NetworkGameManager.GetCardDisplay(cardId);
                    }
                }
            }
        }

        private void LogInfo(string message)
        {
            Debug.Log($"[ControllerUI] {message}");
            if (infoText != null)
            {
                infoText.text = $"[INFO] {message}";
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[ControllerUI] {message}");
            if (infoText != null)
            {
                infoText.text = $"<color=red>[ERROR] {message}</color>";
            }
        }

        #endregion
    }
}
