using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using CardGameBuilder.Core;
using CardGameBuilder.Net;

namespace CardGameBuilder.UI
{
    /// <summary>
    /// Lobby UI for pre-game setup with seat selection, ready system, and host controls
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Lobby Info")]
        [SerializeField] private TextMeshProUGUI lobbyTitleText;
        [SerializeField] private TextMeshProUGUI playerCountText;

        [Header("Seat Selection")]
        [SerializeField] private Transform seatGridContainer;
        [SerializeField] private GameObject seatSlotPrefab;
        [SerializeField] private int maxSeats = 4;

        [Header("Ready System")]
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private TextMeshProUGUI readyStatusText;
        [SerializeField] private Button startGameButton;

        [Header("Host Controls")]
        [SerializeField] private GameObject hostControlsPanel;
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private Button setRoomNameButton;
        [SerializeField] private Toggle allowBotsToggle;
        [SerializeField] private Button addBotButton;
        [SerializeField] private TMP_Dropdown maxPlayersDropdown;

        [Header("Game Settings")]
        [SerializeField] private TMP_Dropdown gameTypeDropdown;
        [SerializeField] private TMP_InputField winTargetInput;
        [SerializeField] private TMP_InputField seedInput;

        [Header("Chat")]
        [SerializeField] private ScrollRect chatScrollRect;
        [SerializeField] private TextMeshProUGUI chatLogText;
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private Button sendChatButton;

        private SessionManager sessionManager;
        private CardGameManager gameManager;
        private List<GameObject> seatSlots = new List<GameObject>();
        private List<string> chatMessages = new List<string>();
        private int mySeatIndex = -1;

        void Start()
        {
            sessionManager = SessionManager.Instance;
            gameManager = CardGameManager.Instance;

            // Setup listeners
            if (readyToggle != null)
                readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);
            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClick);
            if (setRoomNameButton != null)
                setRoomNameButton.onClick.AddListener(OnSetRoomNameClick);
            if (addBotButton != null)
                addBotButton.onClick.AddListener(OnAddBotClick);
            if (allowBotsToggle != null)
                allowBotsToggle.onValueChanged.AddListener(OnAllowBotsChanged);
            if (sendChatButton != null)
                sendChatButton.onClick.AddListener(OnSendChatClick);
            if (maxPlayersDropdown != null)
                maxPlayersDropdown.onValueChanged.AddListener(OnMaxPlayersChanged);

            // Initialize seats
            CreateSeatSlots();

            // Session manager events
            sessionManager.OnPlayerSeated += OnPlayerSeated;
            sessionManager.OnPlayerLeft += OnPlayerLeft;
            sessionManager.OnPlayerReadyChanged += OnPlayerReadyChanged;
            sessionManager.OnAllPlayersReady += OnAllPlayersReady;
        }

        void OnDestroy()
        {
            // Unsubscribe from events
            if (sessionManager != null)
            {
                sessionManager.OnPlayerSeated -= OnPlayerSeated;
                sessionManager.OnPlayerLeft -= OnPlayerLeft;
                sessionManager.OnPlayerReadyChanged -= OnPlayerReadyChanged;
                sessionManager.OnAllPlayersReady -= OnAllPlayersReady;
            }
        }

        void Update()
        {
            UpdateLobbyInfo();
            UpdateSeatSlots();
            UpdateHostControls();
            UpdateReadySystem();
        }

        #region Seat Management

        void CreateSeatSlots()
        {
            if (seatGridContainer == null || seatSlotPrefab == null)
                return;

            // Clear existing
            foreach (var slot in seatSlots)
            {
                if (slot != null)
                    Destroy(slot);
            }
            seatSlots.Clear();

            // Create seat slots
            for (int i = 0; i < maxSeats; i++)
            {
                GameObject slotObj = Instantiate(seatSlotPrefab, seatGridContainer);
                seatSlots.Add(slotObj);

                // Setup seat button
                var button = slotObj.GetComponent<Button>();
                if (button != null)
                {
                    int seatIndex = i; // Capture for lambda
                    button.onClick.AddListener(() => OnSeatClicked(seatIndex));
                }
            }
        }

        void UpdateSeatSlots()
        {
            var sessions = sessionManager.GetAllSessions();

            for (int i = 0; i < seatSlots.Count; i++)
            {
                GameObject slotObj = seatSlots[i];
                if (slotObj == null)
                    continue;

                // Find session for this seat
                var session = sessions.FirstOrDefault(s => s.seatIndex == i);

                // Update slot display
                var nameText = slotObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                var statusText = slotObj.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
                var readyIndicator = slotObj.transform.Find("ReadyIndicator")?.gameObject;
                var button = slotObj.GetComponent<Button>();

                if (session != null)
                {
                    // Seat occupied
                    if (nameText != null)
                    {
                        string botTag = session.isBot ? " [BOT]" : "";
                        nameText.text = $"{session.displayName}{botTag}";
                    }

                    if (statusText != null)
                    {
                        statusText.text = session.isReady ? "READY" : "Not Ready";
                        statusText.color = session.isReady ? Color.green : Color.gray;
                    }

                    if (readyIndicator != null)
                        readyIndicator.SetActive(session.isReady);

                    if (button != null)
                        button.interactable = false; // Can't click occupied seats
                }
                else
                {
                    // Seat empty
                    if (nameText != null)
                        nameText.text = $"Seat {i + 1}\n[Empty]";

                    if (statusText != null)
                        statusText.text = "Open";

                    if (readyIndicator != null)
                        readyIndicator.SetActive(false);

                    if (button != null)
                    {
                        // Can only claim seat if not already seated
                        button.interactable = (mySeatIndex < 0);
                    }
                }

                // Highlight my seat
                var image = slotObj.GetComponent<Image>();
                if (image != null)
                {
                    image.color = (i == mySeatIndex) ? Color.cyan : Color.white;
                }
            }
        }

        void OnSeatClicked(int seatIndex)
        {
            if (mySeatIndex >= 0)
            {
                Debug.Log("[LobbyUI] Already seated");
                return;
            }

            // Check if seat is available
            var session = sessionManager.GetSessionBySeat(seatIndex);
            if (session != null)
            {
                Debug.Log("[LobbyUI] Seat occupied");
                return;
            }

            // Claim seat (this would need network support)
            mySeatIndex = seatIndex;
            Debug.Log($"[LobbyUI] Claimed seat {seatIndex}");

            // In real implementation, send ServerRpc to claim seat
        }

        #endregion

        #region Lobby Info

        void UpdateLobbyInfo()
        {
            if (lobbyTitleText != null)
            {
                lobbyTitleText.text = $"Lobby: {sessionManager.GetRoomName()}";
            }

            if (playerCountText != null)
            {
                int connected = sessionManager.GetConnectedPlayerCount();
                int total = sessionManager.GetTotalPlayerCount();
                int max = sessionManager.GetMaxPlayers();
                playerCountText.text = $"Players: {total}/{max} (Connected: {connected})";
            }
        }

        #endregion

        #region Ready System

        void UpdateReadySystem()
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            int readyCount = sessionManager.GetAllSessions().Count(s => s.isReady || s.isBot);
            int totalCount = sessionManager.GetTotalPlayerCount();

            if (readyStatusText != null)
            {
                readyStatusText.text = $"Ready: {readyCount}/{totalCount}";
            }

            // Enable start button only if host and conditions met
            if (startGameButton != null)
            {
                bool canStart = isHost &&
                               totalCount >= 2 &&
                               sessionManager.AreAllPlayersReady();
                startGameButton.interactable = canStart;
            }

            // Disable ready toggle if in game
            if (readyToggle != null)
            {
                readyToggle.interactable = (gameManager.CurrentGameState == GameState.Waiting);
            }
        }

        void OnReadyToggleChanged(bool isReady)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                sessionManager.SetPlayerReadyByClientId(clientId, isReady);
            }
        }

        void OnAllPlayersReady()
        {
            Debug.Log("[LobbyUI] All players ready!");
            AddChatMessage("System", "All players ready! Host can start the game.");
        }

        #endregion

        #region Host Controls

        void UpdateHostControls()
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            if (hostControlsPanel != null)
                hostControlsPanel.SetActive(isHost);

            if (isHost)
            {
                // Update room name field
                if (roomNameInput != null && string.IsNullOrEmpty(roomNameInput.text))
                {
                    roomNameInput.text = sessionManager.GetRoomName();
                }

                // Update allow bots toggle
                if (allowBotsToggle != null)
                {
                    allowBotsToggle.isOn = sessionManager.GetAllowBots();
                }
            }
        }

        void OnSetRoomNameClick()
        {
            if (roomNameInput != null && !string.IsNullOrWhiteSpace(roomNameInput.text))
            {
                sessionManager.SetRoomName(roomNameInput.text);
                AddChatMessage("System", $"Room name changed to: {roomNameInput.text}");
            }
        }

        void OnAllowBotsChanged(bool allow)
        {
            sessionManager.SetAllowBots(allow);
            AddChatMessage("System", allow ? "Bots enabled" : "Bots disabled");
        }

        void OnAddBotClick()
        {
            if (sessionManager.AddBot())
            {
                AddChatMessage("System", "Bot added to lobby");
            }
            else
            {
                AddChatMessage("System", "Cannot add bot (room full or bots disabled)");
            }
        }

        void OnMaxPlayersChanged(int index)
        {
            int maxPlayers = index + 2; // Dropdown: 0=2 players, 1=3 players, etc.
            sessionManager.SetMaxPlayers(maxPlayers);
            AddChatMessage("System", $"Max players set to: {maxPlayers}");
        }

        void OnStartGameClick()
        {
            if (gameManager == null)
                return;

            // Get settings
            GameType gameType = GameType.War;
            if (gameTypeDropdown != null)
            {
                gameType = (GameType)(gameTypeDropdown.value);
                if (gameType == GameType.None)
                    gameType = GameType.War;
            }

            int seed = -1;
            if (seedInput != null && !string.IsNullOrEmpty(seedInput.text))
            {
                int.TryParse(seedInput.text, out seed);
            }

            int winTarget = -1;
            if (winTargetInput != null && !string.IsNullOrEmpty(winTargetInput.text))
            {
                int.TryParse(winTargetInput.text, out winTarget);
            }

            // Start game
            gameManager.StartGameServerRpc(gameType, seed, winTarget);
            AddChatMessage("System", $"Starting {gameType}!");

            // Hide lobby UI (or switch to game view)
            gameObject.SetActive(false);
        }

        #endregion

        #region Chat

        void OnSendChatClick()
        {
            if (chatInputField == null || string.IsNullOrWhiteSpace(chatInputField.text))
                return;

            string message = chatInputField.text;
            string senderName = "Player"; // Get from profile

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                var session = sessionManager.GetSessionByClientId(clientId);
                if (session != null)
                {
                    senderName = session.displayName;
                }
            }

            AddChatMessage(senderName, message);
            chatInputField.text = "";

            // In real implementation, broadcast via ServerRpc
        }

        void AddChatMessage(string sender, string message)
        {
            string formattedMessage = $"[{System.DateTime.Now:HH:mm}] {sender}: {message}";
            chatMessages.Add(formattedMessage);

            // Limit chat history
            if (chatMessages.Count > 50)
            {
                chatMessages.RemoveAt(0);
            }

            // Update UI
            if (chatLogText != null)
            {
                chatLogText.text = string.Join("\n", chatMessages);
            }

            // Auto-scroll to bottom
            if (chatScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                chatScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        #endregion

        #region Session Events

        void OnPlayerSeated(System.Guid playerId, int seatIndex)
        {
            var session = sessionManager.GetSessionByPlayerId(playerId);
            if (session != null)
            {
                string botTag = session.isBot ? " (Bot)" : "";
                AddChatMessage("System", $"{session.displayName}{botTag} joined at seat {seatIndex + 1}");
            }
        }

        void OnPlayerLeft(System.Guid playerId, int seatIndex)
        {
            AddChatMessage("System", $"Player left seat {seatIndex + 1}");
        }

        void OnPlayerReadyChanged(System.Guid playerId, bool isReady)
        {
            var session = sessionManager.GetSessionByPlayerId(playerId);
            if (session != null)
            {
                string status = isReady ? "ready" : "not ready";
                AddChatMessage("System", $"{session.displayName} is {status}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Show the lobby UI
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            sessionManager.ResetAllReady();
        }

        /// <summary>
        /// Hide the lobby UI
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}
