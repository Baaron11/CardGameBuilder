using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using CardGameBuilder.Games;

namespace CardGameBuilder.Core
{
    /// <summary>
    /// Server-authoritative card game manager.
    /// Handles deck, hands, turns, and game flow for all game types.
    ///
    /// Network Architecture:
    /// - All game logic executes on the server/host
    /// - Clients send action requests via [ServerRpc]
    /// - Server validates and broadcasts updates via [ClientRpc]
    /// - Uses NetworkVariable for synchronized state
    /// </summary>
    public class CardGameManager : NetworkBehaviour
    {
        #region Singleton

        public static CardGameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #endregion

        #region Configuration

        [Header("Game Settings")]
        [SerializeField] private int maxPlayers = 4;
        [SerializeField] private int minPlayers = 2;

        #endregion

        #region Network Variables

        // Synchronized game state
        private NetworkVariable<GameState> gameState = new NetworkVariable<GameState>(
            GameState.Waiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<GameType> currentGameType = new NetworkVariable<GameType>(
            GameType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<int> activeSeatIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<int> roundNumber = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        #endregion

        #region Server-Side State

        // Server-only game state (not synchronized directly)
        private Deck deck;
        private List<PlayerSeat> seats;
        private int shuffleSeed;
        private IGameRules currentGameRules;

        // Game-specific state
        private List<Card> centerPile;      // For War
        private Card leadCard;              // For Hearts trick-taking
        private List<Card> currentTrick;    // For Hearts

        #endregion

        #region Public Properties

        public GameState CurrentGameState => gameState.Value;
        public GameType CurrentGameType => currentGameType.Value;
        public int ActiveSeatIndex => activeSeatIndex.Value;
        public int RoundNumber => roundNumber.Value;
        public int MaxPlayers => maxPlayers;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            deck = new Deck();
            seats = new List<PlayerSeat>();
            centerPile = new List<Card>();
            currentTrick = new List<Card>();

            // Initialize seats
            for (int i = 0; i < maxPlayers; i++)
            {
                seats.Add(new PlayerSeat(i));
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                Debug.Log("[CardGameManager] Server spawned - ready to manage games");
            }
        }

        #endregion

        #region Public API - Game Control

        /// <summary>
        /// Assigns a client to an available seat.
        /// Called when a player joins the game.
        /// </summary>
        public bool AssignPlayerToSeat(ulong clientId, string playerName)
        {
            if (!IsServer) return false;

            // Find first available seat
            for (int i = 0; i < seats.Count; i++)
            {
                if (!seats[i].IsActive)
                {
                    seats[i].ClientId = clientId;
                    seats[i].PlayerName = playerName;
                    seats[i].IsActive = true;

                    Debug.Log($"[CardGameManager] Assigned {playerName} (client {clientId}) to seat {i}");

                    // Notify all clients
                    NotifyPlayerJoinedClientRpc(i, playerName);
                    return true;
                }
            }

            Debug.LogWarning($"[CardGameManager] No available seats for {playerName}");
            return false;
        }

        /// <summary>
        /// Removes a player from their seat.
        /// </summary>
        public void RemovePlayerFromSeat(ulong clientId)
        {
            if (!IsServer) return;

            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive && seats[i].ClientId == clientId)
                {
                    string playerName = seats[i].PlayerName;
                    seats[i].IsActive = false;
                    seats[i].ClientId = ulong.MaxValue;
                    seats[i].Reset();

                    Debug.Log($"[CardGameManager] Removed {playerName} from seat {i}");
                    NotifyPlayerLeftClientRpc(i, playerName);
                    return;
                }
            }
        }

        /// <summary>
        /// Gets the seat index for a given client ID.
        /// </summary>
        public int GetSeatIndexForClient(ulong clientId)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive && seats[i].ClientId == clientId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Gets a player's hand (server-side only).
        /// For clients, only returns their own hand.
        /// </summary>
        public List<Card> GetPlayerHand(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= seats.Count)
                return new List<Card>();

            return seats[seatIndex].Hand;
        }

        #endregion

        #region ServerRpc - Game Flow

        /// <summary>
        /// [ServerRpc] Host starts a new game of the specified type.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void StartGameServerRpc(GameType gameType, int seed = -1)
        {
            if (!IsServer) return;

            // Count active players
            int activePlayers = seats.Count(s => s.IsActive);
            if (activePlayers < minPlayers)
            {
                Debug.LogWarning($"[CardGameManager] Need at least {minPlayers} players to start. Current: {activePlayers}");
                NotifyGameEventClientRpc(new GameEvent($"Need at least {minPlayers} players to start!", -1));
                return;
            }

            Debug.Log($"[CardGameManager] Starting {gameType} with {activePlayers} players");

            // Setup game
            currentGameType.Value = gameType;
            gameState.Value = GameState.Starting;
            roundNumber.Value = 1;

            // Generate or use provided seed
            shuffleSeed = seed == -1 ? UnityEngine.Random.Range(1, 1000000) : seed;

            // Reset all seats
            foreach (var seat in seats)
            {
                if (seat.IsActive)
                    seat.Reset();
            }

            // Initialize game rules
            currentGameRules = CreateGameRules(gameType);
            if (currentGameRules == null)
            {
                Debug.LogError($"[CardGameManager] Failed to create rules for {gameType}");
                return;
            }

            // Setup deck
            deck.Reset();
            deck.Shuffle(shuffleSeed);

            // Deal initial cards
            currentGameRules.DealInitialCards(deck, seats);

            // Determine first player
            activeSeatIndex.Value = currentGameRules.GetFirstPlayer(seats);

            // Notify clients
            NotifyGameStartedClientRpc(gameType, shuffleSeed);

            // Send initial hands to each player
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                {
                    UpdatePlayerHandClientRpc(i, seats[i].Hand.ToArray());
                }
            }

            // Start gameplay
            gameState.Value = GameState.InProgress;
            NotifyGameEventClientRpc(new GameEvent($"{gameType} started! {seats[activeSeatIndex.Value].PlayerName}'s turn.", activeSeatIndex.Value));
        }

        /// <summary>
        /// [ServerRpc] Player performs an action.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void PerformActionServerRpc(PlayerAction action, ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer || gameState.Value != GameState.InProgress)
                return;

            ulong clientId = serverRpcParams.Receive.SenderClientId;
            int seatIndex = GetSeatIndexForClient(clientId);

            if (seatIndex == -1)
            {
                Debug.LogWarning($"[CardGameManager] Action from unknown client {clientId}");
                return;
            }

            // Validate it's this player's turn
            if (seatIndex != activeSeatIndex.Value)
            {
                Debug.LogWarning($"[CardGameManager] Player {seatIndex} tried to act out of turn");
                NotifyGameEventClientRpc(new GameEvent("It's not your turn!", seatIndex));
                return;
            }

            // Process action through game rules
            if (currentGameRules != null)
            {
                bool success = currentGameRules.ProcessAction(action, seatIndex, seats, deck, this);

                if (success)
                {
                    // Update player's hand
                    UpdatePlayerHandClientRpc(seatIndex, seats[seatIndex].Hand.ToArray());

                    // Check for round/game end
                    if (currentGameRules.IsRoundOver(seats))
                    {
                        EndRound();
                    }
                    else
                    {
                        // Advance to next player
                        AdvanceTurn();
                    }
                }
            }
        }

        /// <summary>
        /// [ServerRpc] Player draws a card.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void DrawCardServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (!IsServer || gameState.Value != GameState.InProgress)
                return;

            ulong clientId = serverRpcParams.Receive.SenderClientId;
            int seatIndex = GetSeatIndexForClient(clientId);

            if (seatIndex == -1 || deck.CardsRemaining == 0)
                return;

            Card drawnCard = deck.Draw();
            seats[seatIndex].Hand.Add(drawnCard);

            Debug.Log($"[CardGameManager] Player {seatIndex} drew {drawnCard}");

            // Update that player's hand
            UpdatePlayerHandClientRpc(seatIndex, seats[seatIndex].Hand.ToArray());
            NotifyGameEventClientRpc(new GameEvent($"{seats[seatIndex].PlayerName} drew a card", seatIndex));
        }

        #endregion

        #region ClientRpc - Notifications

        /// <summary>
        /// [ClientRpc] Notifies all clients that a game has started.
        /// </summary>
        [ClientRpc]
        private void NotifyGameStartedClientRpc(GameType gameType, int seed)
        {
            Debug.Log($"[CardGameManager] Game started: {gameType} (seed: {seed})");
            // UI will listen to this event
        }

        /// <summary>
        /// [ClientRpc] Updates a specific player's hand.
        /// Clients only show this if it's their seat.
        /// </summary>
        [ClientRpc]
        private void UpdatePlayerHandClientRpc(int seatIndex, Card[] hand)
        {
            Debug.Log($"[CardGameManager] Hand update for seat {seatIndex}: {hand.Length} cards");
            // ControllerUI will listen and update display
        }

        /// <summary>
        /// [ClientRpc] Broadcasts a game event to all players.
        /// </summary>
        [ClientRpc]
        public void NotifyGameEventClientRpc(GameEvent gameEvent)
        {
            Debug.Log($"[CardGameManager] Event: {gameEvent.Message}");
            // BoardUI will display this message
        }

        /// <summary>
        /// [ClientRpc] Notifies when a player joins.
        /// </summary>
        [ClientRpc]
        private void NotifyPlayerJoinedClientRpc(int seatIndex, string playerName)
        {
            Debug.Log($"[CardGameManager] {playerName} joined at seat {seatIndex}");
        }

        /// <summary>
        /// [ClientRpc] Notifies when a player leaves.
        /// </summary>
        [ClientRpc]
        private void NotifyPlayerLeftClientRpc(int seatIndex, string playerName)
        {
            Debug.Log($"[CardGameManager] {playerName} left seat {seatIndex}");
        }

        /// <summary>
        /// [ClientRpc] Updates scores for all seats.
        /// </summary>
        [ClientRpc]
        public void UpdateScoresClientRpc(int[] scores)
        {
            Debug.Log($"[CardGameManager] Scores updated: {string.Join(", ", scores)}");
            // BoardUI will display updated scores
        }

        #endregion

        #region Server-Only Game Flow

        /// <summary>
        /// Advances to the next player's turn.
        /// </summary>
        private void AdvanceTurn()
        {
            if (!IsServer) return;

            int startSeat = activeSeatIndex.Value;
            int nextSeat = (startSeat + 1) % maxPlayers;

            // Find next active player
            int attempts = 0;
            while (!seats[nextSeat].IsActive && attempts < maxPlayers)
            {
                nextSeat = (nextSeat + 1) % maxPlayers;
                attempts++;
            }

            if (seats[nextSeat].IsActive)
            {
                activeSeatIndex.Value = nextSeat;
                Debug.Log($"[CardGameManager] Turn advanced to seat {nextSeat} ({seats[nextSeat].PlayerName})");
                NotifyGameEventClientRpc(new GameEvent($"{seats[nextSeat].PlayerName}'s turn", nextSeat));
            }
        }

        /// <summary>
        /// Ends the current round and calculates scores.
        /// </summary>
        private void EndRound()
        {
            if (!IsServer) return;

            gameState.Value = GameState.RoundEnd;
            Debug.Log($"[CardGameManager] Round {roundNumber.Value} ended");

            // Calculate scores through game rules
            if (currentGameRules != null)
            {
                currentGameRules.CalculateScores(seats);
            }

            // Broadcast scores
            int[] scores = seats.Select(s => s.Score).ToArray();
            UpdateScoresClientRpc(scores);

            // Check if game is over
            if (currentGameRules != null && currentGameRules.IsGameOver(seats))
            {
                EndGame();
            }
            else
            {
                // Start next round
                StartNextRound();
            }
        }

        /// <summary>
        /// Starts the next round.
        /// </summary>
        private void StartNextRound()
        {
            if (!IsServer) return;

            roundNumber.Value++;
            Debug.Log($"[CardGameManager] Starting round {roundNumber.Value}");

            // Reset round state
            foreach (var seat in seats)
            {
                if (seat.IsActive)
                {
                    seat.Hand.Clear();
                    seat.TricksWon = 0;
                }
            }

            // Re-shuffle and deal
            deck.Reset();
            deck.Shuffle(shuffleSeed + roundNumber.Value);
            currentGameRules?.DealInitialCards(deck, seats);

            // Send updated hands
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                {
                    UpdatePlayerHandClientRpc(i, seats[i].Hand.ToArray());
                }
            }

            // Resume gameplay
            gameState.Value = GameState.InProgress;
            NotifyGameEventClientRpc(new GameEvent($"Round {roundNumber.Value} started!", -1));
        }

        /// <summary>
        /// Ends the game and declares winner.
        /// </summary>
        private void EndGame()
        {
            if (!IsServer) return;

            gameState.Value = GameState.GameOver;

            // Find winner (lowest score for Hearts, highest for others)
            bool lowerIsBetter = currentGameType.Value == GameType.Hearts;
            int winnerSeat = -1;
            int bestScore = lowerIsBetter ? int.MaxValue : int.MinValue;

            for (int i = 0; i < seats.Count; i++)
            {
                if (!seats[i].IsActive) continue;

                bool isBetter = lowerIsBetter
                    ? seats[i].Score < bestScore
                    : seats[i].Score > bestScore;

                if (isBetter)
                {
                    bestScore = seats[i].Score;
                    winnerSeat = i;
                }
            }

            if (winnerSeat != -1)
            {
                string winnerName = seats[winnerSeat].PlayerName;
                NotifyGameEventClientRpc(new GameEvent($"Game Over! {winnerName} wins with {bestScore} points!", winnerSeat, default, bestScore));
            }

            Debug.Log($"[CardGameManager] Game over. Winner: Seat {winnerSeat}");
        }

        /// <summary>
        /// Creates the appropriate game rules instance based on game type.
        /// </summary>
        private IGameRules CreateGameRules(GameType gameType)
        {
            return gameType switch
            {
                GameType.War => new WarRules(),
                GameType.GoFish => new GoFishRules(),
                GameType.Hearts => new HeartsRules(),
                _ => null
            };
        }

        #endregion

        #region Public Helpers

        /// <summary>
        /// Gets all active player seats.
        /// </summary>
        public List<PlayerSeat> GetActiveSeats()
        {
            return seats.Where(s => s.IsActive).ToList();
        }

        /// <summary>
        /// Gets seat info by index.
        /// </summary>
        public PlayerSeat GetSeat(int index)
        {
            if (index >= 0 && index < seats.Count)
                return seats[index];
            return null;
        }

        #endregion
    }
}
