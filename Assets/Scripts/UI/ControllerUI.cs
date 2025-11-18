using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGameBuilder.Core;

namespace CardGameBuilder.UI
{
    /// <summary>
    /// Controller UI - Individual player's hand and action controls.
    /// Each player sees their own cards and can perform game actions.
    ///
    /// Usage:
    /// - Attach to a Canvas GameObject (separate from BoardUI)
    /// - Assign UI element references in Inspector
    /// - Automatically shows/hides action buttons based on current game type
    /// - Disables inputs when it's not the player's turn
    /// </summary>
    public class ControllerUI : MonoBehaviour
    {
        #region UI References

        [Header("Player Hand Display")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardUIPrefab;
        [SerializeField] private TextMeshProUGUI handCountText;

        [Header("Player Info")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI scoreText;

        [Header("Action Buttons - War")]
        [SerializeField] private GameObject warButtonPanel;
        [SerializeField] private Button flipCardButton;

        [Header("Action Buttons - Go Fish")]
        [SerializeField] private GameObject goFishButtonPanel;
        [SerializeField] private TMP_Dropdown targetPlayerDropdown;
        [SerializeField] private TMP_Dropdown targetRankDropdown;
        [SerializeField] private Button askButton;
        [SerializeField] private Button drawButton;

        [Header("Action Buttons - Hearts")]
        [SerializeField] private GameObject heartsButtonPanel;
        [SerializeField] private Button playCardButton;

        [Header("General")]
        [SerializeField] private TextMeshProUGUI turnIndicatorText;
        [SerializeField] private Image turnIndicatorImage;
        [SerializeField] private Color myTurnColor = Color.green;
        [SerializeField] private Color notMyTurnColor = Color.gray;

        #endregion

        #region Private State

        private CardGameManager gameManager;
        private int mySeatIndex = -1;
        private List<Card> currentHand = new List<Card>();
        private List<CardUIElement> cardUIElements = new List<CardUIElement>();
        private Card selectedCard;
        private bool hasSelectedCard = false;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            // Wait for networking and game manager to initialize
            Invoke(nameof(InitializeUI), 0.5f);
        }

        private void InitializeUI()
        {
            gameManager = CardGameManager.Instance;

            if (gameManager == null)
            {
                Debug.LogError("[ControllerUI] CardGameManager not found!");
                return;
            }

            // Setup buttons
            SetupButtons();

            // Setup dropdowns
            SetupDropdowns();

            // Initially hide all action panels
            HideAllActionPanels();

            // Try to assign seat
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                ulong myClientId = NetworkManager.Singleton.LocalClientId;
                mySeatIndex = gameManager.GetSeatIndexForClient(myClientId);

                if (mySeatIndex >= 0)
                {
                    var seat = gameManager.GetSeat(mySeatIndex);
                    if (seat != null && playerNameText != null)
                    {
                        playerNameText.text = seat.PlayerName;
                    }
                }
            }

            UpdateStatusText("Waiting for game to start...");
        }

        private void Update()
        {
            if (gameManager == null) return;

            // Update turn indicator
            UpdateTurnIndicator();

            // Update action buttons based on game state
            UpdateActionButtons();

            // Update score display
            UpdateScoreDisplay();
        }

        #endregion

        #region UI Setup

        private void SetupButtons()
        {
            // War buttons
            if (flipCardButton != null)
            {
                flipCardButton.onClick.AddListener(OnFlipCardClicked);
            }

            // Go Fish buttons
            if (askButton != null)
            {
                askButton.onClick.AddListener(OnAskClicked);
            }

            if (drawButton != null)
            {
                drawButton.onClick.AddListener(OnDrawClicked);
            }

            // Hearts buttons
            if (playCardButton != null)
            {
                playCardButton.onClick.AddListener(OnPlayCardClicked);
            }
        }

        private void SetupDropdowns()
        {
            // Setup rank dropdown for Go Fish
            if (targetRankDropdown != null)
            {
                targetRankDropdown.ClearOptions();
                List<string> ranks = new List<string>
                {
                    "Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King"
                };
                targetRankDropdown.AddOptions(ranks);
            }
        }

        #endregion

        #region UI Updates

        /// <summary>
        /// Updates the turn indicator showing if it's this player's turn.
        /// </summary>
        private void UpdateTurnIndicator()
        {
            bool isMyTurn = gameManager.ActiveSeatIndex == mySeatIndex &&
                           gameManager.CurrentGameState == GameState.InProgress;

            if (turnIndicatorText != null)
            {
                turnIndicatorText.text = isMyTurn ? "YOUR TURN" : "Waiting...";
            }

            if (turnIndicatorImage != null)
            {
                turnIndicatorImage.color = isMyTurn ? myTurnColor : notMyTurnColor;
            }
        }

        /// <summary>
        /// Shows/hides action button panels based on current game type.
        /// </summary>
        private void UpdateActionButtons()
        {
            bool isMyTurn = gameManager.ActiveSeatIndex == mySeatIndex &&
                           gameManager.CurrentGameState == GameState.InProgress;

            // Hide all panels first
            HideAllActionPanels();

            // Show appropriate panel based on game type
            switch (gameManager.CurrentGameType)
            {
                case GameType.War:
                    if (warButtonPanel != null)
                    {
                        warButtonPanel.SetActive(true);
                        if (flipCardButton != null)
                        {
                            flipCardButton.interactable = isMyTurn && currentHand.Count > 0;
                        }
                    }
                    break;

                case GameType.GoFish:
                    if (goFishButtonPanel != null)
                    {
                        goFishButtonPanel.SetActive(true);
                        if (askButton != null)
                        {
                            askButton.interactable = isMyTurn && currentHand.Count > 0;
                        }
                        if (drawButton != null)
                        {
                            drawButton.interactable = isMyTurn;
                        }
                    }
                    UpdateTargetPlayerDropdown();
                    break;

                case GameType.Hearts:
                    if (heartsButtonPanel != null)
                    {
                        heartsButtonPanel.SetActive(true);
                        if (playCardButton != null)
                        {
                            playCardButton.interactable = isMyTurn && hasSelectedCard;
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Updates the target player dropdown for Go Fish.
        /// </summary>
        private void UpdateTargetPlayerDropdown()
        {
            if (targetPlayerDropdown == null || gameManager == null) return;

            targetPlayerDropdown.ClearOptions();

            List<string> playerOptions = new List<string>();
            var activeSeats = gameManager.GetActiveSeats();

            foreach (var seat in activeSeats)
            {
                if (seat.SeatIndex != mySeatIndex) // Don't include self
                {
                    playerOptions.Add($"{seat.PlayerName} (Seat {seat.SeatIndex})");
                }
            }

            if (playerOptions.Count > 0)
            {
                targetPlayerDropdown.AddOptions(playerOptions);
                targetPlayerDropdown.interactable = true;
            }
            else
            {
                targetPlayerDropdown.AddOptions(new List<string> { "No other players" });
                targetPlayerDropdown.interactable = false;
            }
        }

        /// <summary>
        /// Updates the score display.
        /// </summary>
        private void UpdateScoreDisplay()
        {
            if (scoreText == null || mySeatIndex < 0) return;

            var seat = gameManager.GetSeat(mySeatIndex);
            if (seat != null)
            {
                scoreText.text = $"Score: {seat.Score}";
            }
        }

        /// <summary>
        /// Updates the status text.
        /// </summary>
        private void UpdateStatusText(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
        }

        /// <summary>
        /// Hides all action button panels.
        /// </summary>
        private void HideAllActionPanels()
        {
            if (warButtonPanel != null) warButtonPanel.SetActive(false);
            if (goFishButtonPanel != null) goFishButtonPanel.SetActive(false);
            if (heartsButtonPanel != null) heartsButtonPanel.SetActive(false);
        }

        #endregion

        #region Hand Display

        /// <summary>
        /// Updates the displayed hand with new cards.
        /// Called when receiving hand update from server.
        /// </summary>
        public void UpdateHand(Card[] newHand)
        {
            currentHand.Clear();
            currentHand.AddRange(newHand);

            if (handCountText != null)
            {
                handCountText.text = $"Cards: {currentHand.Count}";
            }

            RefreshHandDisplay();
        }

        /// <summary>
        /// Refreshes the visual display of cards in hand.
        /// </summary>
        private void RefreshHandDisplay()
        {
            if (handContainer == null) return;

            // Clear existing card UI
            foreach (var cardUI in cardUIElements)
            {
                if (cardUI != null)
                {
                    Destroy(cardUI.gameObject);
                }
            }
            cardUIElements.Clear();

            // Create UI for each card
            for (int i = 0; i < currentHand.Count; i++)
            {
                Card card = currentHand[i];
                GameObject cardObj = cardUIPrefab != null
                    ? Instantiate(cardUIPrefab, handContainer)
                    : new GameObject($"Card_{i}");

                CardUIElement cardUI = cardObj.GetComponent<CardUIElement>();
                if (cardUI == null)
                {
                    cardUI = cardObj.AddComponent<CardUIElement>();
                }

                cardUI.SetCard(card);
                cardUI.SetClickCallback(() => OnCardClicked(card));
                cardUIElements.Add(cardUI);
            }
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// Called when a card in the hand is clicked.
        /// </summary>
        private void OnCardClicked(Card card)
        {
            selectedCard = card;
            hasSelectedCard = true;

            // Highlight selected card
            foreach (var cardUI in cardUIElements)
            {
                cardUI.SetHighlighted(cardUI.GetCard().Equals(card));
            }

            UpdateStatusText($"Selected: {card}");
            Debug.Log($"[ControllerUI] Selected card: {card}");
        }

        /// <summary>
        /// [War] Flip top card button clicked.
        /// </summary>
        private void OnFlipCardClicked()
        {
            if (gameManager == null || mySeatIndex < 0) return;

            PlayerAction action = new PlayerAction(ActionType.FlipCard);
            gameManager.PerformActionServerRpc(action);

            UpdateStatusText("Flipping card...");
        }

        /// <summary>
        /// [Go Fish] Ask button clicked.
        /// </summary>
        private void OnAskClicked()
        {
            if (gameManager == null || mySeatIndex < 0) return;

            // Get target player from dropdown
            if (targetPlayerDropdown == null || targetRankDropdown == null) return;

            var activeSeats = gameManager.GetActiveSeats();
            List<int> otherSeatIndices = new List<int>();

            foreach (var seat in activeSeats)
            {
                if (seat.SeatIndex != mySeatIndex)
                {
                    otherSeatIndices.Add(seat.SeatIndex);
                }
            }

            if (targetPlayerDropdown.value < 0 || targetPlayerDropdown.value >= otherSeatIndices.Count)
            {
                UpdateStatusText("Select a valid player!");
                return;
            }

            int targetSeat = otherSeatIndices[targetPlayerDropdown.value];

            // Get target rank (1-13 corresponding to Ace-King)
            Rank targetRank = (Rank)(targetRankDropdown.value + 1);

            PlayerAction action = new PlayerAction(ActionType.Ask, default, targetSeat, targetRank);
            gameManager.PerformActionServerRpc(action);

            UpdateStatusText($"Asking for {targetRank}s...");
        }

        /// <summary>
        /// [Go Fish] Draw button clicked.
        /// </summary>
        private void OnDrawClicked()
        {
            if (gameManager == null || mySeatIndex < 0) return;

            gameManager.DrawCardServerRpc();
            UpdateStatusText("Drawing card...");
        }

        /// <summary>
        /// [Hearts] Play selected card button clicked.
        /// </summary>
        private void OnPlayCardClicked()
        {
            if (gameManager == null || mySeatIndex < 0 || !hasSelectedCard) return;

            PlayerAction action = new PlayerAction(ActionType.Play, selectedCard);
            gameManager.PerformActionServerRpc(action);

            UpdateStatusText($"Playing {selectedCard}...");

            // Clear selection
            hasSelectedCard = false;
            selectedCard = default;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Sets the player's seat index.
        /// </summary>
        public void SetSeatIndex(int seatIndex)
        {
            mySeatIndex = seatIndex;

            if (mySeatIndex >= 0)
            {
                var seat = gameManager?.GetSeat(mySeatIndex);
                if (seat != null && playerNameText != null)
                {
                    playerNameText.text = seat.PlayerName;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Helper component for displaying a single card in the hand.
    /// Attach this to your card UI prefab with image/text for the card display.
    /// </summary>
    public class CardUIElement : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI cardText;
        public Image cardImage;
        public Button cardButton;

        [Header("Colors")]
        public Color normalColor = Color.white;
        public Color highlightedColor = Color.yellow;
        public Color heartColor = Color.red;
        public Color diamondColor = new Color(1f, 0.4f, 0.4f);
        public Color clubColor = Color.black;
        public Color spadeColor = Color.black;

        private Card card;
        private System.Action clickCallback;

        private void Awake()
        {
            if (cardButton == null)
            {
                cardButton = GetComponent<Button>();
            }

            if (cardButton == null)
            {
                cardButton = gameObject.AddComponent<Button>();
            }

            cardButton.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// Sets the card data and updates display.
        /// </summary>
        public void SetCard(Card newCard)
        {
            card = newCard;
            UpdateDisplay();
        }

        /// <summary>
        /// Sets the click callback for this card.
        /// </summary>
        public void SetClickCallback(System.Action callback)
        {
            clickCallback = callback;
        }

        /// <summary>
        /// Gets the card this UI represents.
        /// </summary>
        public Card GetCard()
        {
            return card;
        }

        /// <summary>
        /// Sets whether this card is highlighted (selected).
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (cardImage != null)
            {
                cardImage.color = highlighted ? highlightedColor : GetSuitColor();
            }
        }

        /// <summary>
        /// Updates the visual display of the card.
        /// </summary>
        private void UpdateDisplay()
        {
            if (cardText != null)
            {
                cardText.text = card.ToShortString();
            }

            if (cardImage != null)
            {
                cardImage.color = GetSuitColor();
            }
        }

        /// <summary>
        /// Gets the color for this card's suit.
        /// </summary>
        private Color GetSuitColor()
        {
            return card.Suit switch
            {
                Suit.Hearts => heartColor,
                Suit.Diamonds => diamondColor,
                Suit.Clubs => clubColor,
                Suit.Spades => spadeColor,
                _ => normalColor
            };
        }

        private void OnClicked()
        {
            clickCallback?.Invoke();
        }
    }
}
