using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using CardGameBuilder.Core;
using CardGameBuilder.Net;
using CardGameBuilder.Persistence;

namespace CardGameBuilder.UI
{
    /// <summary>
    /// Polished HUD for Board/Host display with room info, player list, scores, and controls
    /// </summary>
    public class BoardHUD : MonoBehaviour
    {
        [Header("Room Info")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI roomCodeText;
        [SerializeField] private TextMeshProUGUI lanIpText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button copyIpButton;

        [Header("Game State")]
        [SerializeField] private TextMeshProUGUI gameStateText;
        [SerializeField] private TextMeshProUGUI currentTurnText;
        [SerializeField] private TextMeshProUGUI roundNumberText;

        [Header("Player List")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerEntryPrefab;

        [Header("Game Controls - Host Only")]
        [SerializeField] private GameObject hostControlsPanel;
        [SerializeField] private TMP_Dropdown gameTypeDropdown;
        [SerializeField] private TMP_InputField seedInput;
        [SerializeField] private TMP_InputField winTargetInput;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button saveMatchButton;
        [SerializeField] private Button resumeMatchButton;
        [SerializeField] private Button endGameButton;

        [Header("Bot Controls")]
        [SerializeField] private Button addBotButton;
        [SerializeField] private Button removeBotButton;
        [SerializeField] private TextMeshProUGUI botCountText;

        [Header("Event Log")]
        [SerializeField] private ScrollRect eventLogScrollRect;
        [SerializeField] private TextMeshProUGUI eventLogText;
        [SerializeField] private int maxEventLogLines = 20;

        [Header("Toast")]
        [SerializeField] private Toast toastComponent;

        private CardGameManager gameManager;
        private SessionManager sessionManager;
        private List<GameObject> playerEntries = new List<GameObject>();
        private List<string> eventLog = new List<string>();

        void Start()
        {
            gameManager = CardGameManager.Instance;
            sessionManager = SessionManager.Instance;

            // Setup button listeners
            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClick);
            if (saveMatchButton != null)
                saveMatchButton.onClick.AddListener(OnSaveMatchClick);
            if (resumeMatchButton != null)
                resumeMatchButton.onClick.AddListener(OnResumeMatchClick);
            if (endGameButton != null)
                endGameButton.onClick.AddListener(OnEndGameClick);
            if (addBotButton != null)
                addBotButton.onClick.AddListener(OnAddBotClick);
            if (removeBotButton != null)
                removeBotButton.onClick.AddListener(OnRemoveBotClick);
            if (copyCodeButton != null)
                copyCodeButton.onClick.AddListener(OnCopyCodeClick);
            if (copyIpButton != null)
                copyIpButton.onClick.AddListener(OnCopyIpClick);

            // Set toast manager
            if (toastComponent != null)
                ToastManager.Instance.SetActiveToast(toastComponent);

            // Initial update
            UpdateRoomInfo();
        }

        void Update()
        {
            if (gameManager == null || sessionManager == null)
                return;

            UpdateGameState();
            UpdatePlayerList();
            UpdateControls();
        }

        #region UI Updates

        void UpdateRoomInfo()
        {
            if (roomNameText != null)
                roomNameText.text = $"Room: {sessionManager.GetRoomName()}";

            if (sessionManager.GetRoomCode() == "")
            {
                sessionManager.GenerateRoomCode();
            }

            if (roomCodeText != null)
                roomCodeText.text = $"Code: {sessionManager.GetRoomCode()}";

            if (lanIpText != null)
                lanIpText.text = $"IP: {sessionManager.GetLanIpAddress()}";
        }

        void UpdateGameState()
        {
            if (gameStateText != null)
            {
                string stateStr = gameManager.CurrentGameState.ToString();
                string gameTypeStr = gameManager.CurrentGameType.ToString();
                gameStateText.text = $"{gameTypeStr} | {stateStr}";
            }

            if (roundNumberText != null)
            {
                roundNumberText.text = $"Round: {gameManager.RoundNumber}";
            }

            if (currentTurnText != null)
            {
                int activeSeat = gameManager.ActiveSeatIndex;
                if (activeSeat >= 0)
                {
                    var seat = gameManager.GetSeat(activeSeat);
                    if (seat != null && seat.IsActive)
                    {
                        currentTurnText.text = $"Turn: {seat.PlayerName}";
                        currentTurnText.color = Color.yellow;
                    }
                    else
                    {
                        currentTurnText.text = "Turn: --";
                        currentTurnText.color = Color.white;
                    }
                }
                else
                {
                    currentTurnText.text = "Turn: --";
                    currentTurnText.color = Color.white;
                }
            }
        }

        void UpdatePlayerList()
        {
            if (playerListContainer == null || playerEntryPrefab == null)
                return;

            var sessions = sessionManager.GetAllSessions();

            // Clear old entries if count changed
            if (playerEntries.Count != sessions.Count)
            {
                foreach (var entry in playerEntries)
                {
                    if (entry != null)
                        Destroy(entry);
                }
                playerEntries.Clear();

                // Create new entries
                foreach (var session in sessions)
                {
                    GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
                    playerEntries.Add(entry);
                }
            }

            // Update entries
            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var entry = playerEntries[i];

                if (entry == null)
                    continue;

                // Find text components
                var nameText = entry.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                var scoreText = entry.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
                var statusText = entry.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
                var turnIndicator = entry.transform.Find("TurnIndicator")?.gameObject;

                // Update name
                if (nameText != null)
                {
                    string botTag = session.isBot ? " [BOT]" : "";
                    nameText.text = $"Seat {session.seatIndex + 1}: {session.displayName}{botTag}";
                }

                // Update score
                var seat = gameManager.GetSeat(session.seatIndex);
                if (scoreText != null && seat != null)
                {
                    scoreText.text = $"Score: {seat.Score}";
                }

                // Update ready status
                if (statusText != null)
                {
                    statusText.text = session.isReady ? "READY" : "Not Ready";
                    statusText.color = session.isReady ? Color.green : Color.gray;
                }

                // Show turn indicator
                if (turnIndicator != null)
                {
                    bool isCurrentTurn = (gameManager.ActiveSeatIndex == session.seatIndex);
                    turnIndicator.SetActive(isCurrentTurn);
                }
            }
        }

        void UpdateControls()
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            bool isInGame = gameManager.CurrentGameState == GameState.InProgress;
            bool isWaiting = gameManager.CurrentGameState == GameState.Waiting;

            // Show/hide host controls
            if (hostControlsPanel != null)
                hostControlsPanel.SetActive(isHost);

            // Enable/disable buttons
            if (startGameButton != null)
                startGameButton.interactable = isHost && isWaiting;

            if (saveMatchButton != null)
                saveMatchButton.interactable = isHost && isInGame;

            if (resumeMatchButton != null)
                resumeMatchButton.interactable = isHost && isWaiting && MatchPersistence.Instance.HasAutoSave();

            if (endGameButton != null)
                endGameButton.interactable = isHost && isInGame;

            if (addBotButton != null)
                addBotButton.interactable = isHost && !isInGame;

            if (removeBotButton != null)
                removeBotButton.interactable = isHost && !isInGame;

            // Update bot count
            if (botCountText != null)
            {
                int botCount = sessionManager.GetAllSessions().Count(s => s.isBot);
                botCountText.text = $"Bots: {botCount}";
            }
        }

        #endregion

        #region Button Handlers

        void OnStartGameClick()
        {
            if (gameManager == null)
                return;

            // Parse settings
            GameType gameType = (GameType)(gameTypeDropdown?.value ?? 0);
            if (gameType == GameType.None)
                gameType = GameType.War;

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

            gameManager.StartGameServerRpc(gameType, seed, winTarget);
            LogEvent($"Starting {gameType}...");
        }

        void OnSaveMatchClick()
        {
            if (gameManager == null)
                return;

            var snapshot = gameManager.CreateSnapshot();
            if (snapshot != null)
            {
                bool success = MatchPersistence.Instance.SaveSnapshot(snapshot);
                if (success)
                {
                    LogEvent("Match saved successfully!");
                    ToastManager.Instance.ShowSuccess("Match saved!");
                }
                else
                {
                    LogEvent("Failed to save match");
                    ToastManager.Instance.ShowError("Save failed");
                }
            }
        }

        void OnResumeMatchClick()
        {
            if (gameManager == null)
                return;

            var snapshot = MatchPersistence.Instance.LoadAutoSave();
            if (snapshot != null)
            {
                bool success = gameManager.ApplySnapshot(snapshot);
                if (success)
                {
                    LogEvent($"Resumed {snapshot.gameType} from Round {snapshot.roundNumber}");
                    ToastManager.Instance.ShowSuccess("Match resumed!");
                }
                else
                {
                    LogEvent("Failed to resume match");
                    ToastManager.Instance.ShowError("Resume failed");
                }
            }
            else
            {
                LogEvent("No save file found");
                ToastManager.Instance.ShowWarning("No save found");
            }
        }

        void OnEndGameClick()
        {
            LogEvent("Game ended by host");
            ToastManager.Instance.ShowInfo("Game ended");
            // Optionally trigger end game logic
        }

        void OnAddBotClick()
        {
            if (sessionManager.AddBot())
            {
                LogEvent("Bot added to game");
                ToastManager.Instance.ShowInfo("Bot added");
            }
            else
            {
                LogEvent("Cannot add bot (room full or bots disabled)");
                ToastManager.Instance.ShowWarning("Cannot add bot");
            }
        }

        void OnRemoveBotClick()
        {
            // Remove last bot
            var sessions = sessionManager.GetAllSessions();
            var botSession = sessions.LastOrDefault(s => s.isBot);
            if (botSession != null)
            {
                sessionManager.RemoveBot(botSession.seatIndex);
                LogEvent($"Removed bot from seat {botSession.seatIndex + 1}");
                ToastManager.Instance.ShowInfo("Bot removed");
            }
            else
            {
                LogEvent("No bots to remove");
                ToastManager.Instance.ShowWarning("No bots");
            }
        }

        void OnCopyCodeClick()
        {
            string code = sessionManager.GetRoomCode();
            GUIUtility.systemCopyBuffer = code;
            ToastManager.Instance.ShowSuccess($"Copied: {code}");
        }

        void OnCopyIpClick()
        {
            string ip = sessionManager.GetLanIpAddress();
            GUIUtility.systemCopyBuffer = ip;
            ToastManager.Instance.ShowSuccess($"Copied: {ip}");
        }

        #endregion

        #region Event Log

        public void LogEvent(string message)
        {
            string timestampedMessage = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            eventLog.Add(timestampedMessage);

            // Limit log size
            if (eventLog.Count > maxEventLogLines)
            {
                eventLog.RemoveAt(0);
            }

            // Update UI
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
        }

        public void ClearEventLog()
        {
            eventLog.Clear();
            if (eventLogText != null)
                eventLogText.text = "";
        }

        #endregion

        #region Public API

        /// <summary>
        /// Set room name and update display
        /// </summary>
        public void SetRoomName(string name)
        {
            sessionManager.SetRoomName(name);
            UpdateRoomInfo();
        }

        /// <summary>
        /// Allow bots in this room
        /// </summary>
        public void SetAllowBots(bool allow)
        {
            sessionManager.SetAllowBots(allow);
        }

        #endregion
    }
}
