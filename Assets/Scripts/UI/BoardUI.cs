using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGameBuilder.Core;
using CardGameBuilder.Modding;

namespace CardGameBuilder.UI
{
    /// <summary>
    /// Board UI - Displays game state and host controls.
    /// This is the "table" view showing all players, scores, and game events.
    ///
    /// Usage:
    /// - Attach to a Canvas GameObject in your scene
    /// - Assign UI element references in Inspector
    /// - Host uses this to start games and see overall state
    /// - All players see current turn, scores, and events here
    /// </summary>
    public class BoardUI : MonoBehaviour
    {
        #region UI References

        [Header("Host Controls")]
        [SerializeField] private GameObject hostPanel;
        [SerializeField] private TMP_Dropdown gameTypeDropdown;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TMP_InputField seedInputField;

        [Header("Game Info Display")]
        [SerializeField] private TextMeshProUGUI gameStateText;
        [SerializeField] private TextMeshProUGUI gameNameText;
        [SerializeField] private TextMeshProUGUI currentTurnText;
        [SerializeField] private TextMeshProUGUI roundNumberText;

        [Header("Player Display")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerInfoPrefab; // Prefab with: PlayerName, Score, HandCount texts

        [Header("Event Log")]
        [SerializeField] private TextMeshProUGUI eventLogText;
        [SerializeField] private ScrollRect eventLogScrollRect;
        [SerializeField] private int maxEventLines = 15;

        #endregion

        #region Private State

        private List<PlayerInfoUI> playerInfoUIElements = new List<PlayerInfoUI>();
        private List<string> eventLog = new List<string>();
        private CardGameManager gameManager;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Wait for CardGameManager to spawn
            Invoke(nameof(InitializeUI), 0.5f);
        }

        private void InitializeUI()
        {
            gameManager = CardGameManager.Instance;

            if (gameManager == null)
            {
                Debug.LogError("[BoardUI] CardGameManager not found! Make sure it exists in the scene.");
                return;
            }

            // Setup dropdown with game types
            SetupGameTypeDropdown();

            // Setup buttons
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
            }

            // Initially hide host panel (only show for host)
            if (hostPanel != null)
            {
                hostPanel.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost);
            }

            // Subscribe to network variable changes
            SubscribeToNetworkEvents();

            AddEventLog("Board UI initialized. Waiting for players...");
        }

        private void OnEnable()
        {
            // Re-subscribe when enabled
            if (gameManager != null)
            {
                SubscribeToNetworkEvents();
            }
        }

        private void OnDisable()
        {
            // Clean up subscriptions
            UnsubscribeFromNetworkEvents();
        }

        private void Update()
        {
            // Update UI every frame based on current network state
            UpdateGameStateDisplay();
            UpdatePlayerDisplay();
        }

        #endregion

        #region Network Event Subscription

        private void SubscribeToNetworkEvents()
        {
            if (gameManager == null) return;

            // Note: In a full implementation, you'd subscribe to NetworkVariable.OnValueChanged
            // For this example, we'll poll the values in Update()
        }

        private void UnsubscribeFromNetworkEvents()
        {
            // Unsubscribe from events if needed
        }

        #endregion

        #region UI Setup

        private void SetupGameTypeDropdown()
        {
            if (gameTypeDropdown == null) return;

            gameTypeDropdown.ClearOptions();

            List<string> options = new List<string>
            {
                "Select Game...",
                "War",
                "Go Fish",
                "Hearts"
            };

            gameTypeDropdown.AddOptions(options);
            gameTypeDropdown.value = 0;
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// Called when host clicks "Start Game" button.
        /// </summary>
        private void OnStartGameClicked()
        {
            if (gameManager == null || gameTypeDropdown == null)
            {
                Debug.LogWarning("[BoardUI] Cannot start game - missing references");
                return;
            }

            // Check if host
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                AddEventLog("Only the host can start games!");
                return;
            }

            // Get selected game type
            GameType selectedGame = gameTypeDropdown.value switch
            {
                1 => GameType.War,
                2 => GameType.GoFish,
                3 => GameType.Hearts,
                _ => GameType.None
            };

            if (selectedGame == GameType.None)
            {
                AddEventLog("Please select a game type!");
                return;
            }

            // Get seed (optional)
            int seed = -1;
            if (seedInputField != null && !string.IsNullOrEmpty(seedInputField.text))
            {
                int.TryParse(seedInputField.text, out seed);
            }

            // Start game via ServerRpc
            AddEventLog($"Starting {selectedGame}...");
            gameManager.StartGameServerRpc(selectedGame, seed);
        }

        #endregion

        #region UI Updates

        /// <summary>
        /// Updates the main game state display.
        /// </summary>
        private void UpdateGameStateDisplay()
        {
            if (gameManager == null) return;

            // Game state
            if (gameStateText != null)
            {
                gameStateText.text = $"State: {gameManager.CurrentGameState}";
            }

            // Game name (show custom game name if active)
            if (gameNameText != null)
            {
                if (gameManager.IsCustomGame && gameManager.ActiveCustomGame != null)
                {
                    gameNameText.text = $"Game: {gameManager.ActiveCustomGame.gameName}";
                    gameNameText.color = new Color(0.5f, 1f, 0.5f); // Light green for custom games
                }
                else if (gameManager.CurrentGameType != GameType.None)
                {
                    gameNameText.text = $"Game: {gameManager.CurrentGameType}";
                    gameNameText.color = Color.white;
                }
                else
                {
                    gameNameText.text = "Game: None";
                    gameNameText.color = Color.gray;
                }
            }

            // Current turn
            if (currentTurnText != null)
            {
                if (gameManager.ActiveSeatIndex >= 0 && gameManager.ActiveSeatIndex < gameManager.MaxPlayers)
                {
                    var seat = gameManager.GetSeat(gameManager.ActiveSeatIndex);
                    if (seat != null && seat.IsActive)
                    {
                        currentTurnText.text = $"Current Turn: {seat.PlayerName}";
                        currentTurnText.color = Color.yellow;
                    }
                    else
                    {
                        currentTurnText.text = "Current Turn: --";
                        currentTurnText.color = Color.gray;
                    }
                }
                else
                {
                    currentTurnText.text = "Waiting to start...";
                    currentTurnText.color = Color.gray;
                }
            }

            // Round number
            if (roundNumberText != null)
            {
                if (gameManager.CurrentGameState == GameState.InProgress ||
                    gameManager.CurrentGameState == GameState.RoundEnd)
                {
                    roundNumberText.text = $"Round: {gameManager.RoundNumber}";
                }
                else
                {
                    roundNumberText.text = "";
                }
            }
        }

        /// <summary>
        /// Updates the player list display with current scores and status.
        /// </summary>
        private void UpdatePlayerDisplay()
        {
            if (gameManager == null || playerListContainer == null) return;

            // Get active seats
            var activeSeats = gameManager.GetActiveSeats();

            // Create or update player info UI elements
            while (playerInfoUIElements.Count < activeSeats.Count)
            {
                GameObject newPlayerInfo = playerInfoPrefab != null
                    ? Instantiate(playerInfoPrefab, playerListContainer)
                    : new GameObject("PlayerInfo");

                PlayerInfoUI uiElement = newPlayerInfo.GetComponent<PlayerInfoUI>();
                if (uiElement == null)
                {
                    uiElement = newPlayerInfo.AddComponent<PlayerInfoUI>();
                }

                playerInfoUIElements.Add(uiElement);
            }

            // Update each player's info
            for (int i = 0; i < activeSeats.Count; i++)
            {
                var seat = activeSeats[i];
                if (i < playerInfoUIElements.Count)
                {
                    bool isCurrentTurn = seat.SeatIndex == gameManager.ActiveSeatIndex;
                    playerInfoUIElements[i].UpdateInfo(seat, isCurrentTurn);
                }
            }

            // Hide excess UI elements
            for (int i = activeSeats.Count; i < playerInfoUIElements.Count; i++)
            {
                playerInfoUIElements[i].gameObject.SetActive(false);
            }
        }

        #endregion

        #region Event Log

        /// <summary>
        /// Adds a message to the event log.
        /// Called when game events occur.
        /// </summary>
        public void AddEventLog(string message)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}";

            eventLog.Add(logEntry);

            // Keep only recent events
            if (eventLog.Count > maxEventLines)
            {
                eventLog.RemoveAt(0);
            }

            // Update display
            if (eventLogText != null)
            {
                eventLogText.text = string.Join("\n", eventLog);
            }

            // Auto-scroll to bottom
            if (eventLogScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                eventLogScrollRect.verticalNormalizedPosition = 0f;
            }

            Debug.Log($"[BoardUI] {logEntry}");
        }

        /// <summary>
        /// Called by CardGameManager via ClientRpc to display events.
        /// </summary>
        public void OnGameEvent(GameEvent gameEvent)
        {
            AddEventLog(gameEvent.Message);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows/hides host controls based on whether local player is host.
        /// </summary>
        public void SetHostMode(bool isHost)
        {
            if (hostPanel != null)
            {
                hostPanel.SetActive(isHost);
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper component for displaying individual player info in the player list.
    /// Attach this to your player info prefab with text elements for name, score, cards.
    /// </summary>
    public class PlayerInfoUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI playerNameText;
        public TextMeshProUGUI scoreText;
        public TextMeshProUGUI handCountText;
        public Image backgroundImage;

        [Header("Colors")]
        public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        public Color currentTurnColor = new Color(0.3f, 0.5f, 0.3f, 0.9f);

        /// <summary>
        /// Updates this UI element with current player info.
        /// </summary>
        public void UpdateInfo(PlayerSeat seat, bool isCurrentTurn)
        {
            gameObject.SetActive(true);

            if (playerNameText != null)
            {
                playerNameText.text = seat.PlayerName;
            }

            if (scoreText != null)
            {
                scoreText.text = $"Score: {seat.Score}";
            }

            if (handCountText != null)
            {
                handCountText.text = $"Cards: {seat.Hand.Count}";
            }

            if (backgroundImage != null)
            {
                backgroundImage.color = isCurrentTurn ? currentTurnColor : normalColor;
            }
        }
    }
}
