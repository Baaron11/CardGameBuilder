using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using CardGameBuilder.Core;
using CardGameBuilder.Net;

namespace CardGameBuilder.Game
{
    /// <summary>
    /// Server-side bot controller that makes AI decisions for bot-controlled seats
    /// </summary>
    public class BotController : MonoBehaviour
    {
        private static BotController _instance;
        public static BotController Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("BotController");
                    _instance = go.AddComponent<BotController>();
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private float botThinkTime = 1.5f;        // Delay before bot acts (for realism)
        [SerializeField] private float botThinkVariance = 0.5f;    // Random variance in think time

        private CardGameManager _gameManager;
        private bool _isProcessing = false;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        void Start()
        {
            _gameManager = CardGameManager.Instance;
        }

        void Update()
        {
            // Only run on server
            if (!NetworkManager.Singleton.IsServer)
                return;

            // Only process if game is in progress and not already processing
            if (_gameManager == null || _isProcessing)
                return;

            if (_gameManager.State.Value != GameState.InProgress)
                return;

            // Check if current turn is a bot
            int currentSeat = _gameManager.TurnSeat.Value;
            if (SessionManager.Instance.IsSeatBot(currentSeat))
            {
                StartCoroutine(ProcessBotTurn(currentSeat));
            }
        }

        /// <summary>
        /// Process a bot's turn with realistic delay
        /// </summary>
        private IEnumerator ProcessBotTurn(int seatIndex)
        {
            _isProcessing = true;

            // Random think time to appear more human-like
            float thinkTime = botThinkTime + Random.Range(-botThinkVariance, botThinkVariance);
            yield return new WaitForSeconds(thinkTime);

            // Ensure it's still the bot's turn
            if (_gameManager.TurnSeat.Value != seatIndex)
            {
                _isProcessing = false;
                yield break;
            }

            // Make decision based on current game type
            PlayerAction action = DecideBotAction(seatIndex);

            if (action.Type != ActionType.None)
            {
                // Execute the action through the game manager
                _gameManager.ProcessPlayerAction(seatIndex, action);
            }
            else
            {
                Debug.LogWarning($"[BotController] Bot at seat {seatIndex} could not decide action");
            }

            _isProcessing = false;
        }

        /// <summary>
        /// Decide what action the bot should take based on game type
        /// </summary>
        private PlayerAction DecideBotAction(int seatIndex)
        {
            GameType gameType = _gameManager.ActiveGame.Value;

            switch (gameType)
            {
                case GameType.War:
                    return DecideWarAction(seatIndex);

                case GameType.GoFish:
                    return DecideGoFishAction(seatIndex);

                case GameType.Hearts:
                    return DecideHeartsAction(seatIndex);

                default:
                    Debug.LogWarning($"[BotController] No AI for game type: {gameType}");
                    return new PlayerAction { Type = ActionType.None };
            }
        }

        #region War AI

        /// <summary>
        /// War AI: Simply flip the top card
        /// </summary>
        private PlayerAction DecideWarAction(int seatIndex)
        {
            var seat = _gameManager.GetPlayerSeat(seatIndex);
            if (seat == null || seat.Hand.Count == 0)
                return new PlayerAction { Type = ActionType.None };

            // Flip the first card in hand
            Card cardToFlip = seat.Hand[0];
            return new PlayerAction
            {
                Type = ActionType.FlipCard,
                Card = cardToFlip
            };
        }

        #endregion

        #region Go Fish AI

        /// <summary>
        /// Go Fish AI: Simple heuristic - ask for ranks we already have
        /// </summary>
        private PlayerAction DecideGoFishAction(int seatIndex)
        {
            var seat = _gameManager.GetPlayerSeat(seatIndex);
            if (seat == null)
                return new PlayerAction { Type = ActionType.None };

            // If hand is empty, draw a card
            if (seat.Hand.Count == 0)
            {
                return new PlayerAction { Type = ActionType.Draw };
            }

            // Count cards by rank
            Dictionary<Rank, int> rankCounts = new Dictionary<Rank, int>();
            foreach (var card in seat.Hand)
            {
                if (!rankCounts.ContainsKey(card.Rank))
                    rankCounts[card.Rank] = 0;
                rankCounts[card.Rank]++;
            }

            // Find best rank to ask for (one we have multiple of, or any)
            Rank targetRank = Rank.Ace;
            int maxCount = 0;

            foreach (var kvp in rankCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    targetRank = kvp.Key;
                }
            }

            // Choose a random opponent who is still active
            int targetSeat = ChooseRandomOpponent(seatIndex);
            if (targetSeat == -1)
            {
                // No valid opponents, draw instead
                return new PlayerAction { Type = ActionType.Draw };
            }

            return new PlayerAction
            {
                Type = ActionType.Ask,
                TargetRank = targetRank,
                TargetSeatIndex = targetSeat
            };
        }

        #endregion

        #region Hearts AI

        /// <summary>
        /// Hearts AI: Basic strategy - avoid points, follow suit
        /// </summary>
        private PlayerAction DecideHeartsAction(int seatIndex)
        {
            var seat = _gameManager.GetPlayerSeat(seatIndex);
            if (seat == null || seat.Hand.Count == 0)
                return new PlayerAction { Type = ActionType.None };

            Card? cardToPlay = null;

            // Get current trick's lead suit (if any)
            var currentTrick = _gameManager.GetCurrentTrick();
            Suit? leadSuit = currentTrick.Count > 0 ? (Suit?)currentTrick[0].Suit : null;

            if (leadSuit.HasValue)
            {
                // Must follow suit if possible
                var validCards = seat.Hand.Where(c => c.Suit == leadSuit.Value).ToList();

                if (validCards.Count > 0)
                {
                    // Follow suit - play lowest card to avoid taking trick
                    cardToPlay = validCards.OrderBy(c => c.Rank).First();
                }
                else
                {
                    // Can't follow suit - dump highest point card
                    cardToPlay = ChooseDiscardCard(seat.Hand);
                }
            }
            else
            {
                // Leading the trick
                cardToPlay = ChooseLeadCard(seat.Hand);
            }

            if (!cardToPlay.HasValue)
            {
                // Fallback: play first card
                cardToPlay = seat.Hand[0];
            }

            return new PlayerAction
            {
                Type = ActionType.Play,
                Card = cardToPlay.Value
            };
        }

        /// <summary>
        /// Choose best card to lead in Hearts (avoid hearts, play safe suits)
        /// </summary>
        private Card? ChooseLeadCard(List<Card> hand)
        {
            // Prefer non-hearts, low cards
            var nonHearts = hand.Where(c => c.Suit != Suit.Hearts).OrderBy(c => c.Rank).ToList();
            if (nonHearts.Count > 0)
                return nonHearts[0];

            // If only hearts, play lowest
            return hand.OrderBy(c => c.Rank).First();
        }

        /// <summary>
        /// Choose card to discard in Hearts (dump points)
        /// </summary>
        private Card? ChooseDiscardCard(List<Card> hand)
        {
            // Prioritize Queen of Spades (13 pts)
            var queenOfSpades = hand.FirstOrDefault(c => c.Suit == Suit.Spades && c.Rank == Rank.Queen);
            if (queenOfSpades.Suit != Suit.None)
                return queenOfSpades;

            // Then hearts (highest first)
            var hearts = hand.Where(c => c.Suit == Suit.Hearts).OrderByDescending(c => c.Rank).ToList();
            if (hearts.Count > 0)
                return hearts[0];

            // Otherwise highest card
            return hand.OrderByDescending(c => c.Rank).First();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Choose a random opponent seat that is active
        /// </summary>
        private int ChooseRandomOpponent(int mySeat)
        {
            List<int> validOpponents = new List<int>();

            for (int i = 0; i < _gameManager.GetMaxPlayers(); i++)
            {
                if (i == mySeat)
                    continue;

                var seat = _gameManager.GetPlayerSeat(i);
                if (seat != null && seat.IsActive && seat.Hand.Count > 0)
                {
                    validOpponents.Add(i);
                }
            }

            if (validOpponents.Count == 0)
                return -1;

            return validOpponents[Random.Range(0, validOpponents.Count)];
        }

        /// <summary>
        /// Enable/disable bot processing (useful for pausing)
        /// </summary>
        public void SetBotProcessing(bool enabled)
        {
            Debug.Log($"[BotController] Bot processing: {enabled}");
        }

        /// <summary>
        /// Set bot think time parameters
        /// </summary>
        public void SetBotThinkTime(float baseTime, float variance)
        {
            botThinkTime = Mathf.Max(0.1f, baseTime);
            botThinkVariance = Mathf.Max(0f, variance);
        }

        #endregion
    }

    // Extension methods for CardGameManager to support bot queries
    public static class CardGameManagerBotExtensions
    {
        /// <summary>
        /// Get the current trick cards (for Hearts AI)
        /// </summary>
        public static List<Card> GetCurrentTrick(this CardGameManager manager)
        {
            // This would need to be implemented in CardGameManager
            // For now, return empty list (will be added in CardGameManager update)
            return new List<Card>();
        }

        /// <summary>
        /// Get max players count
        /// </summary>
        public static int GetMaxPlayers(this CardGameManager manager)
        {
            return 4; // Default, will be configurable in updated CardGameManager
        }
    }
}
