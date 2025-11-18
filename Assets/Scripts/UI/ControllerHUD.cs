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
    /// Polished HUD for player controller with profile, hand display, and action buttons
    /// </summary>
    public class ControllerHUD : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] private TMP_InputField displayNameInput;
        [SerializeField] private Button saveNameButton;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI playerInfoText;
        [SerializeField] private TextMeshProUGUI myScoreText;
        [SerializeField] private TextMeshProUGUI turnStatusText;
        [SerializeField] private TextMeshProUGUI handCountText;

        [Header("Connection")]
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private TMP_InputField serverIpInput;
        [SerializeField] private Button connectButton;
        [SerializeField] private Button disconnectButton;
        [SerializeField] private TextMeshProUGUI connectionStatusText;

        [Header("Hand Display")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardButtonPrefab;

        [Header("Action Buttons")]
        [SerializeField] private Button playCardButton;
        [SerializeField] private Button drawCardButton;
        [SerializeField] private Button flipCardButton;
        [SerializeField] private Button askButton;

        [Header("Go Fish Controls")]
        [SerializeField] private GameObject goFishPanel;
        [SerializeField] private TMP_Dropdown targetPlayerDropdown;
        [SerializeField] private TMP_Dropdown targetRankDropdown;

        [Header("Ready System")]
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private TextMeshProUGUI readyCountText;

        [Header("Toast")]
        [SerializeField] private Toast toastComponent;

        [Header("Reconnection")]
        [SerializeField] private GameObject reconnectionNotice;
        [SerializeField] private TextMeshProUGUI reconnectionText;

        private CardGameManager gameManager;
        private SessionManager sessionManager;
        private ProfileService profileService;
        private NetworkGameManager networkManager;

        private List<GameObject> handCardObjects = new List<GameObject>();
        private Card selectedCard;
        private int mySeatIndex = -1;
        private bool isReconnecting = false;

        void Start()
        {
            gameManager = CardGameManager.Instance;
            sessionManager = SessionManager.Instance;
            profileService = ProfileService.Instance;

            // Setup button listeners
            if (saveNameButton != null)
                saveNameButton.onClick.AddListener(OnSaveNameClick);
            if (connectButton != null)
                connectButton.onClick.AddListener(OnConnectClick);
            if (disconnectButton != null)
                disconnectButton.onClick.AddListener(OnDisconnectClick);
            if (playCardButton != null)
                playCardButton.onClick.AddListener(OnPlayCardClick);
            if (drawCardButton != null)
                drawCardButton.onClick.AddListener(OnDrawCardClick);
            if (flipCardButton != null)
                flipCardButton.onClick.AddListener(OnFlipCardClick);
            if (askButton != null)
                askButton.onClick.AddListener(OnAskClick);
            if (readyToggle != null)
                readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);

            // Load profile
            LoadProfile();

            // Set toast manager
            if (toastComponent != null)
                ToastManager.Instance.SetActiveToast(toastComponent);

            // Initial UI update
            UpdateConnectionPanel(true);
            if (reconnectionNotice != null)
                reconnectionNotice.SetActive(false);
        }

        void Update()
        {
            if (gameManager == null || sessionManager == null)
                return;

            UpdatePlayerInfo();
            UpdateActionButtons();
            UpdateReadySystem();

            // Check for reconnection
            CheckReconnectionStatus();
        }

        #region Profile Management

        void LoadProfile()
        {
            if (profileService == null)
                return;

            var profile = profileService.GetProfile();

            if (displayNameInput != null)
                displayNameInput.text = profile.displayName;

            UpdateStatsDisplay();
        }

        void UpdateStatsDisplay()
        {
            if (statsText != null && profileService != null)
            {
                statsText.text = profileService.GetStatsSummary();
            }
        }

        void OnSaveNameClick()
        {
            if (displayNameInput == null || string.IsNullOrWhiteSpace(displayNameInput.text))
                return;

            profileService.UpdateDisplayName(displayNameInput.text);
            ToastManager.Instance.ShowSuccess("Name saved!");
            UpdateStatsDisplay();
        }

        #endregion

        #region Connection

        void OnConnectClick()
        {
            if (networkManager == null)
                networkManager = NetworkGameManager.Instance;

            if (networkManager == null)
            {
                Debug.LogError("[ControllerHUD] NetworkGameManager not found");
                ToastManager.Instance.ShowError("Network manager missing");
                return;
            }

            string serverIp = serverIpInput?.text ?? "127.0.0.1";

            // Set transport IP
            var transport = NetworkManager.Singleton?.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
            {
                transport.ConnectionData.Address = serverIp;
            }

            // Start as client
            networkManager.StartClient();

            // Register with session manager
            var profile = profileService.GetProfile();
            StartCoroutine(WaitForConnectionAndRegister(profile.PlayerId, profile.displayName));

            UpdateConnectionPanel(false);
            ToastManager.Instance.ShowInfo($"Connecting to {serverIp}...");
        }

        System.Collections.IEnumerator WaitForConnectionAndRegister(System.Guid playerId, string displayName)
        {
            // Wait for connection
            float timeout = 5f;
            float elapsed = 0f;

            while (!NetworkManager.Singleton.IsConnectedClient && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (NetworkManager.Singleton.IsConnectedClient)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                mySeatIndex = sessionManager.RegisterPlayer(clientId, playerId, displayName);

                if (mySeatIndex >= 0)
                {
                    ToastManager.Instance.ShowSuccess($"Connected! Seat {mySeatIndex + 1}");
                }
                else
                {
                    ToastManager.Instance.ShowWarning("Connected but room full");
                }
            }
            else
            {
                ToastManager.Instance.ShowError("Connection timeout");
                UpdateConnectionPanel(true);
            }
        }

        void OnDisconnectClick()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
                ToastManager.Instance.ShowInfo("Disconnected");
            }

            mySeatIndex = -1;
            UpdateConnectionPanel(true);
        }

        void UpdateConnectionPanel(bool showConnectionUI)
        {
            if (connectionPanel != null)
                connectionPanel.SetActive(showConnectionUI);

            if (connectionStatusText != null)
            {
                if (NetworkManager.Singleton == null)
                {
                    connectionStatusText.text = "Not Connected";
                    connectionStatusText.color = Color.gray;
                }
                else if (NetworkManager.Singleton.IsConnectedClient)
                {
                    connectionStatusText.text = "Connected";
                    connectionStatusText.color = Color.green;
                }
                else
                {
                    connectionStatusText.text = "Disconnected";
                    connectionStatusText.color = Color.red;
                }
            }
        }

        #endregion

        #region Player Info

        void UpdatePlayerInfo()
        {
            // Get my seat
            if (mySeatIndex < 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                mySeatIndex = gameManager.GetSeatIndexForClient(NetworkManager.Singleton.LocalClientId);
            }

            if (mySeatIndex < 0)
            {
                if (playerInfoText != null)
                    playerInfoText.text = "Not seated";
                return;
            }

            var mySeat = gameManager.GetSeat(mySeatIndex);
            if (mySeat == null)
                return;

            // Update player info
            if (playerInfoText != null)
            {
                playerInfoText.text = $"{mySeat.PlayerName} | Seat {mySeatIndex + 1}";
            }

            // Update score
            if (myScoreText != null)
            {
                myScoreText.text = $"Score: {mySeat.Score}";
            }

            // Update turn status
            if (turnStatusText != null)
            {
                bool isMyTurn = (gameManager.ActiveSeatIndex == mySeatIndex);
                if (isMyTurn)
                {
                    turnStatusText.text = "YOUR TURN";
                    turnStatusText.color = Color.yellow;
                }
                else
                {
                    int activeSeat = gameManager.ActiveSeatIndex;
                    if (activeSeat >= 0)
                    {
                        var activeSeatObj = gameManager.GetSeat(activeSeat);
                        if (activeSeatObj != null)
                        {
                            turnStatusText.text = $"{activeSeatObj.PlayerName}'s Turn";
                            turnStatusText.color = Color.white;
                        }
                    }
                    else
                    {
                        turnStatusText.text = "Waiting...";
                        turnStatusText.color = Color.gray;
                    }
                }
            }

            // Update hand count
            if (handCountText != null)
            {
                handCountText.text = $"Hand: {mySeat.Hand.Count} cards";
            }

            // Update hand display
            UpdateHandDisplay(mySeat.Hand);
        }

        #endregion

        #region Hand Display

        void UpdateHandDisplay(List<Card> hand)
        {
            if (handContainer == null || cardButtonPrefab == null)
                return;

            // Clear old cards if count changed
            if (handCardObjects.Count != hand.Count)
            {
                foreach (var cardObj in handCardObjects)
                {
                    if (cardObj != null)
                        Destroy(cardObj);
                }
                handCardObjects.Clear();

                // Create new card buttons
                for (int i = 0; i < hand.Count; i++)
                {
                    GameObject cardObj = Instantiate(cardButtonPrefab, handContainer);
                    handCardObjects.Add(cardObj);
                }
            }

            // Update card buttons
            for (int i = 0; i < hand.Count; i++)
            {
                Card card = hand[i];
                GameObject cardObj = handCardObjects[i];

                if (cardObj == null)
                    continue;

                // Update card text
                var cardText = cardObj.GetComponentInChildren<TextMeshProUGUI>();
                if (cardText != null)
                {
                    cardText.text = card.ToShortString();
                }

                // Setup button click
                var button = cardObj.GetComponent<Button>();
                if (button != null)
                {
                    int index = i; // Capture for lambda
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnCardSelected(card, index));
                }

                // Highlight selected card
                var image = cardObj.GetComponent<Image>();
                if (image != null)
                {
                    image.color = (card.Equals(selectedCard)) ? Color.yellow : Color.white;
                }
            }
        }

        void OnCardSelected(Card card, int index)
        {
            selectedCard = card;
            Debug.Log($"[ControllerHUD] Selected card: {card.ToShortString()}");
        }

        #endregion

        #region Action Buttons

        void UpdateActionButtons()
        {
            bool isMyTurn = (mySeatIndex >= 0 && gameManager.ActiveSeatIndex == mySeatIndex);
            bool isInGame = (gameManager.CurrentGameState == GameState.InProgress);
            bool hasSelection = selectedCard.Suit != Suit.None;

            GameType currentGame = gameManager.CurrentGameType;

            // Show/hide game-specific panels
            if (goFishPanel != null)
                goFishPanel.SetActive(currentGame == GameType.GoFish && isMyTurn);

            // Enable/disable buttons based on game type and turn
            if (playCardButton != null)
            {
                bool canPlay = isMyTurn && isInGame && hasSelection &&
                              (currentGame == GameType.Hearts);
                playCardButton.interactable = canPlay;
            }

            if (drawCardButton != null)
            {
                bool canDraw = isMyTurn && isInGame &&
                              (currentGame == GameType.GoFish);
                drawCardButton.interactable = canDraw;
            }

            if (flipCardButton != null)
            {
                bool canFlip = isMyTurn && isInGame && hasSelection &&
                              (currentGame == GameType.War);
                flipCardButton.interactable = canFlip;
            }

            if (askButton != null)
            {
                bool canAsk = isMyTurn && isInGame &&
                             (currentGame == GameType.GoFish);
                askButton.interactable = canAsk;
            }
        }

        void OnPlayCardClick()
        {
            if (selectedCard.Suit == Suit.None)
            {
                ToastManager.Instance.ShowWarning("Select a card first");
                return;
            }

            PlayerAction action = new PlayerAction
            {
                Type = ActionType.Play,
                Card = selectedCard
            };

            gameManager.PerformActionServerRpc(action);
            selectedCard = default;
        }

        void OnDrawCardClick()
        {
            gameManager.DrawCardServerRpc();
        }

        void OnFlipCardClick()
        {
            if (selectedCard.Suit == Suit.None)
            {
                ToastManager.Instance.ShowWarning("Select a card first");
                return;
            }

            PlayerAction action = new PlayerAction
            {
                Type = ActionType.FlipCard,
                Card = selectedCard
            };

            gameManager.PerformActionServerRpc(action);
            selectedCard = default;
        }

        void OnAskClick()
        {
            if (targetPlayerDropdown == null || targetRankDropdown == null)
                return;

            int targetSeat = targetPlayerDropdown.value;
            Rank targetRank = (Rank)(targetRankDropdown.value + 1);

            PlayerAction action = new PlayerAction
            {
                Type = ActionType.Ask,
                TargetSeatIndex = targetSeat,
                TargetRank = targetRank
            };

            gameManager.PerformActionServerRpc(action);
        }

        #endregion

        #region Ready System

        void UpdateReadySystem()
        {
            if (readyCountText != null)
            {
                int totalPlayers = sessionManager.GetTotalPlayerCount();
                int readyPlayers = sessionManager.GetAllSessions().Count(s => s.isReady || s.isBot);
                readyCountText.text = $"Ready: {readyPlayers}/{totalPlayers}";
            }

            // Disable ready toggle during game
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

                string status = isReady ? "ready" : "not ready";
                ToastManager.Instance.ShowInfo($"You are {status}");
            }
        }

        #endregion

        #region Reconnection

        void CheckReconnectionStatus()
        {
            if (reconnectionNotice == null)
                return;

            // Check if we were disconnected and should show reconnection option
            if (!NetworkManager.Singleton.IsConnectedClient && mySeatIndex >= 0 && !isReconnecting)
            {
                reconnectionNotice.SetActive(true);
                if (reconnectionText != null)
                    reconnectionText.text = "Disconnected! Your seat is reserved. Click Connect to rejoin.";
            }
            else
            {
                reconnectionNotice.SetActive(false);
            }
        }

        #endregion
    }
}
