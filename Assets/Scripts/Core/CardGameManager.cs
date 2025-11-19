using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using CardGameBuilder.Games;
using CardGameBuilder.Persistence;
using CardGameBuilder.Net;
using CardGameBuilder.Modding;

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

            // Initialize NetworkLists
            booksPerSeat = new NetworkList<ushort>();
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

        // Go Fish specific network variables
        private NetworkVariable<int> deckCount = new NetworkVariable<int>(
            52,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkList<ushort> booksPerSeat;

        private NetworkVariable<Unity.Collections.FixedString128Bytes> lastAction =
            new NetworkVariable<Unity.Collections.FixedString128Bytes>(
                "",
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        #endregion

        #region Server-Side State

        // Server-only game state (not synchronized directly)
        private Deck deck;
        private List<PlayerSeat> seats;
        private int shuffleSeed;
        private IGameRules currentGameRules;
        private RulesConfig currentRulesConfig;

        // Custom game support
        private CustomGameDefinition activeCustomGame;

        // Game-specific state
        private List<Card> centerPile;      // For War
        private List<Card> discardPile;     // General discard pile
        private Card leadCard;              // For Hearts trick-taking
        private List<Card> currentTrick;    // For Hearts

        // Go Fish specific server-side state
        private Dictionary<int, List<Card>> serverHands; // Authoritative hands for Go Fish

        #endregion

        #region Public Properties

        // Public NetworkVariable accessors for bot controller
        public NetworkVariable<GameState> State => gameState;
        public NetworkVariable<int> TurnSeat => activeSeatIndex;
        public NetworkVariable<GameType> ActiveGame => currentGameType;

        public GameState CurrentGameState => gameState.Value;
        public GameType CurrentGameType => currentGameType.Value;
        public int ActiveSeatIndex => activeSeatIndex.Value;
        public int RoundNumber => roundNumber.Value;
        public int MaxPlayers => maxPlayers;
        public CustomGameDefinition ActiveCustomGame => activeCustomGame;
        public bool IsCustomGame => activeCustomGame != null;

        // Go Fish specific properties
        public int DeckCount => deckCount.Value;
        public NetworkList<ushort> BooksPerSeat => booksPerSeat;
        public string LastAction => lastAction.Value.ToString();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            deck = new Deck();
            seats = new List<PlayerSeat>();
            centerPile = new List<Card>();
            discardPile = new List<Card>();
            currentTrick = new List<Card>();
            serverHands = new Dictionary<int, List<Card>>();

            // Initialize seats
            for (int i = 0; i < maxPlayers; i++)
            {
                seats.Add(new PlayerSeat(i));
                serverHands[i] = new List<Card>();
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
        /// Sets the active custom game definition.
        /// Should be called before starting a custom game.
        /// </summary>
        public void SetCustomGame(CustomGameDefinition customGame)
        {
            activeCustomGame = customGame;
            Debug.Log($"[CardGameManager] Set custom game: {customGame?.gameName ?? "None"}");
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
#pragma warning disable CS0618
        [ServerRpc(RequireOwnership = false)]
        public void StartGameServerRpc(GameType gameType, int seed = -1, int winTarget = -1, int handSize = -1)
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

            // Setup rules config
            currentRulesConfig = RulesConfig.Default(gameType);
            if (winTarget > 0) currentRulesConfig.winTarget = winTarget;
            if (handSize > 0) currentRulesConfig.initialHandSize = handSize;
            currentRulesConfig.maxPlayers = maxPlayers;

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

            // Initialize Go Fish specific state
            if (gameType == GameType.GoFish)
            {
                // Clear and initialize books
                booksPerSeat.Clear();
                for (int i = 0; i < maxPlayers; i++)
                {
                    booksPerSeat.Add(0);
                    serverHands[i] = new List<Card>();
                }

                // Deal using server-side hands
                List<int> activeSeats = new List<int>();
                for (int i = 0; i < seats.Count; i++)
                {
                    if (seats[i].IsActive)
                        activeSeats.Add(i);
                }

                CardGameBuilder.Game.GoFishRules.DealInitialHands(deck, serverHands, activeSeats);
                deckCount.Value = deck.CardsRemaining;
                lastAction.Value = "Game started!";

                // Determine first player
                activeSeatIndex.Value = activeSeats.Count > 0 ? activeSeats[0] : 0;

                // Notify clients
                NotifyGameStartedClientRpc(gameType, shuffleSeed);

                // Send initial hands to each player (targeted)
                for (int i = 0; i < seats.Count; i++)
                {
                    if (seats[i].IsActive)
                    {
                        SyncHandToClient(i);
                    }
                }

                // Start gameplay
                gameState.Value = GameState.InProgress;
                NotifyGameEventClientRpc(new GameEvent($"Go Fish started! {seats[activeSeatIndex.Value].PlayerName}'s turn.", activeSeatIndex.Value));
            }
            else
            {
                // Standard game setup for War/Hearts
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
        }
#pragma warning restore CS0618

        /// <summary>
        /// [ServerRpc] Player performs an action.
        /// </summary>
#pragma warning disable CS0618
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
#pragma warning restore CS0618

        /// <summary>
        /// [ServerRpc] Player draws a card.
        /// </summary>
#pragma warning disable CS0618
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
#pragma warning restore CS0618

        /// <summary>
        /// [ServerRpc] Go Fish - Player asks another player for a rank.
        /// </summary>
#pragma warning disable CS0618
        [ServerRpc(RequireOwnership = false)]
        public void RequestAskServerRpc(int targetSeat, byte rankValue, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || currentGameType.Value != GameType.GoFish || gameState.Value != GameState.InProgress)
                return;

            ulong clientId = rpcParams.Receive.SenderClientId;
            int askerSeat = GetSeatIndexForClient(clientId);

            if (askerSeat == -1)
            {
                Debug.LogWarning($"[CardGameManager] Ask from unknown client {clientId}");
                return;
            }

            // Validate it's this player's turn
            if (askerSeat != activeSeatIndex.Value)
            {
                Debug.LogWarning($"[CardGameManager] Player {askerSeat} tried to ask out of turn");
                NotifyGameEventClientRpc(new GameEvent("It's not your turn!", askerSeat));
                return;
            }

            Rank rank = (Rank)rankValue;

            // Validate using pure logic helper
            if (!CardGameBuilder.Game.GoFishRules.CanAsk(askerSeat, targetSeat, rank, serverHands[askerSeat]))
            {
                NotifyGameEventClientRpc(new GameEvent("You must have the rank you're asking for!", askerSeat));
                return;
            }

            // Validate target seat
            if (targetSeat < 0 || targetSeat >= seats.Count || !seats[targetSeat].IsActive)
            {
                NotifyGameEventClientRpc(new GameEvent("Invalid target player!", askerSeat));
                return;
            }

            // Get active seats and player names
            List<int> activeSeats = new List<int>();
            string[] playerNames = new string[maxPlayers];
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                {
                    activeSeats.Add(i);
                    playerNames[i] = seats[i].PlayerName;
                }
            }

            // Convert booksPerSeat to array
            ushort[] bookBitmasks = new ushort[maxPlayers];
            for (int i = 0; i < booksPerSeat.Count && i < maxPlayers; i++)
            {
                bookBitmasks[i] = booksPerSeat[i];
            }

            // Execute the ask using pure logic
            var result = CardGameBuilder.Game.GoFishRules.ResolveAsk(
                serverHands,
                deck,
                bookBitmasks,
                askerSeat,
                targetSeat,
                rank,
                activeSeats,
                playerNames
            );

            // Update booksPerSeat from modified bitmasks
            for (int i = 0; i < maxPlayers; i++)
            {
                if (i < booksPerSeat.Count)
                {
                    booksPerSeat[i] = bookBitmasks[i];
                }
                else
                {
                    booksPerSeat.Add(bookBitmasks[i]);
                }
            }

            // Update scores based on books
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive && i < bookBitmasks.Length)
                {
                    seats[i].Score = CardGameBuilder.Game.GoFishRules.CountBooks(bookBitmasks[i]);
                }
            }

            // Update deck count
            deckCount.Value = deck.CardsRemaining;

            // Update last action
            lastAction.Value = result.actionLog;

            // Send updated hands to involved players
            SyncHandToClient(askerSeat);
            SyncHandToClient(targetSeat);

            // Broadcast event
            NotifyGameEventClientRpc(new GameEvent(result.actionLog, askerSeat));

            // Update scores
            UpdateScoresClientRpc(seats.Select(s => s.Score).ToArray());

            // Update turn
            activeSeatIndex.Value = result.nextTurnSeat;

            // Check for game over
            if (CardGameBuilder.Game.GoFishRules.IsGameOver(serverHands, deck.CardsRemaining, bookBitmasks))
            {
                EndGoFishGame(bookBitmasks, activeSeats);
            }

            Debug.Log($"[CardGameManager] Ask processed: {result.actionLog}");
        }
#pragma warning restore CS0618

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
        /// [ClientRpc] Syncs a hand to a specific client (Go Fish).
        /// Only the target client receives this.
        /// </summary>
        [ClientRpc]
        private void SyncHandClientRpc(Card[] myHand, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"[CardGameManager] Hand sync received: {myHand.Length} cards");
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
            // Check for custom game first
            if (activeCustomGame != null && gameType == GameType.None)
            {
                Debug.Log($"[CardGameManager] Creating custom game rules: {activeCustomGame.gameName}");
                return new CustomGameRules(activeCustomGame, this);
            }

            // Standard games
            return gameType switch
            {
                GameType.War => new WarRules(),
                GameType.GoFish => new GoFishRules(),
                GameType.Hearts => new HeartsRules(),
                _ => null
            };
        }

        /// <summary>
        /// [Go Fish] Syncs a player's hand to their client only.
        /// </summary>
        private void SyncHandToClient(int seatIndex)
        {
            if (!IsServer || seatIndex < 0 || seatIndex >= seats.Count)
                return;

            if (!seats[seatIndex].IsActive)
                return;

            ulong targetClientId = seats[seatIndex].ClientId;
            Card[] hand = serverHands[seatIndex].ToArray();

            // Send to specific client
            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };

            SyncHandClientRpc(hand, clientRpcParams);
            Debug.Log($"[CardGameManager] Synced {hand.Length} cards to seat {seatIndex} (client {targetClientId})");
        }

        /// <summary>
        /// [Go Fish] Ends the Go Fish game and declares winner(s).
        /// </summary>
        private void EndGoFishGame(ushort[] bookBitmasks, List<int> activeSeats)
        {
            if (!IsServer)
                return;

            gameState.Value = GameState.GameOver;

            List<int> winners = CardGameBuilder.Game.GoFishRules.GetWinners(bookBitmasks, activeSeats);

            if (winners.Count == 1)
            {
                int winnerSeat = winners[0];
                int winnerBooks = CardGameBuilder.Game.GoFishRules.CountBooks(bookBitmasks[winnerSeat]);
                string winnerName = seats[winnerSeat].PlayerName;

                NotifyGameEventClientRpc(new GameEvent(
                    $"Game Over! {winnerName} wins with {winnerBooks} books!",
                    winnerSeat,
                    default,
                    winnerBooks));

                Debug.Log($"[CardGameManager] Go Fish winner: {winnerName} ({winnerBooks} books)");
            }
            else if (winners.Count > 1)
            {
                int tieBooks = CardGameBuilder.Game.GoFishRules.CountBooks(bookBitmasks[winners[0]]);
                string winnerNames = string.Join(", ", winners.Select(s => seats[s].PlayerName));

                NotifyGameEventClientRpc(new GameEvent(
                    $"Game Over! Tie between {winnerNames} with {tieBooks} books each!",
                    -1,
                    default,
                    tieBooks));

                Debug.Log($"[CardGameManager] Go Fish tie: {winnerNames}");
            }
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

        /// <summary>
        /// Gets player seat (alias for GetSeat for bot controller compatibility).
        /// </summary>
        public PlayerSeat GetPlayerSeat(int index)
        {
            return GetSeat(index);
        }

        /// <summary>
        /// Gets current trick cards (for Hearts AI).
        /// </summary>
        public List<Card> GetCurrentTrick()
        {
            return currentTrick ?? new List<Card>();
        }

        /// <summary>
        /// Process player action directly (for bot controller).
        /// </summary>
        public void ProcessPlayerAction(int seatIndex, PlayerAction action)
        {
            if (!IsServer || gameState.Value != GameState.InProgress)
                return;

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
        /// Notify game event (stub for custom game rules integration).
        /// </summary>
        public void NotifyGameEvent(string eventName, string payload = null)
        {
            // TODO: Later route to HUD / log / ClientRpc. For now, no-op to satisfy references.
        }

        #endregion

        #region M3 - Persistence & Snapshot

        /// <summary>
        /// Create a snapshot of the current game state for save/load.
        /// </summary>
        public MatchSnapshot CreateSnapshot()
        {
            if (!IsServer)
            {
                Debug.LogWarning("[CardGameManager] Only server can create snapshots");
                return null;
            }

            MatchSnapshot snapshot = new MatchSnapshot(
                currentGameType.Value,
                gameState.Value,
                shuffleSeed,
                roundNumber.Value,
                activeSeatIndex.Value,
                deck,
                discardPile,
                seats,
                currentRulesConfig,
                SessionManager.Instance.GetRoomName()
            );

            // Enrich with session data (player IDs, bot flags)
            SessionManager.Instance.EnrichSnapshot(snapshot);

            Debug.Log($"[CardGameManager] Created snapshot: {snapshot.gameType} Round {snapshot.roundNumber}");
            return snapshot;
        }

        /// <summary>
        /// Restore game state from a snapshot.
        /// </summary>
        public bool ApplySnapshot(MatchSnapshot snapshot)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[CardGameManager] Only server can apply snapshots");
                return false;
            }

            if (snapshot == null)
            {
                Debug.LogError("[CardGameManager] Cannot apply null snapshot");
                return false;
            }

            try
            {
                // Restore game state
                currentGameType.Value = snapshot.gameType;
                gameState.Value = snapshot.gameState;
                roundNumber.Value = snapshot.roundNumber;
                activeSeatIndex.Value = snapshot.activeSeatIndex;
                shuffleSeed = snapshot.seed;
                currentRulesConfig = snapshot.rulesConfig ?? RulesConfig.Default(snapshot.gameType);

                // Restore deck
                deck.Reset();
                deck.Clear();
                foreach (int cardId in snapshot.deckCards)
                {
                    deck.AddCard(Card.FromId(cardId));
                }

                // Restore discard pile
                discardPile.Clear();
                foreach (int cardId in snapshot.discardPile)
                {
                    discardPile.Add(Card.FromId(cardId));
                }

                // Restore seats
                for (int i = 0; i < snapshot.seats.Count && i < seats.Count; i++)
                {
                    snapshot.seats[i].ApplyToSeat(seats[i]);
                }

                // Restore game rules
                currentGameRules = CreateGameRules(snapshot.gameType);

                // Notify clients
                NotifyGameStartedClientRpc(snapshot.gameType, shuffleSeed);

                // Send hands to players
                for (int i = 0; i < seats.Count; i++)
                {
                    if (seats[i].IsActive)
                    {
                        UpdatePlayerHandClientRpc(i, seats[i].Hand.ToArray());
                    }
                }

                // Update scores
                int[] scores = seats.Select(s => s.Score).ToArray();
                UpdateScoresClientRpc(scores);

                Debug.Log($"[CardGameManager] Applied snapshot: {snapshot.gameType} Round {snapshot.roundNumber}");
                NotifyGameEventClientRpc(new GameEvent("Game resumed from save!", -1));

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CardGameManager] Error applying snapshot: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Auto-save current game state.
        /// </summary>
        public void AutoSave()
        {
            if (!IsServer || gameState.Value == GameState.Waiting)
                return;

            var snapshot = CreateSnapshot();
            if (snapshot != null)
            {
                MatchPersistence.Instance.AutoSave(snapshot);
            }
        }

        #endregion

        #region M3 - Scoring & End Game

        /// <summary>
        /// Get the current winner (for UI display during game).
        /// </summary>
        public int GetCurrentLeader()
        {
            bool lowerIsBetter = currentGameType.Value == GameType.Hearts;
            int leaderSeat = -1;
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
                    leaderSeat = i;
                }
            }

            return leaderSeat;
        }

        /// <summary>
        /// Check if win condition is met based on rules config.
        /// </summary>
        private bool CheckWinCondition()
        {
            if (currentRulesConfig == null)
                return false;

            bool lowerIsBetter = currentGameType.Value == GameType.Hearts;

            foreach (var seat in seats)
            {
                if (!seat.IsActive) continue;

                if (lowerIsBetter)
                {
                    // Hearts: game ends if someone reaches point limit
                    if (seat.Score >= currentRulesConfig.winTarget)
                        return true;
                }
                else
                {
                    // War/GoFish: game ends if someone reaches target score
                    if (seat.Score >= currentRulesConfig.winTarget)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Save game results to player profiles.
        /// </summary>
#pragma warning disable CS0618
        [ServerRpc(RequireOwnership = false)]
        public void SaveGameResultsServerRpc()
        {
            if (!IsServer || gameState.Value != GameState.GameOver)
                return;

            int winnerSeat = GetCurrentLeader();
            if (winnerSeat == -1)
                return;

            // Save results for all human players
            for (int i = 0; i < seats.Count; i++)
            {
                if (!seats[i].IsActive)
                    continue;

                bool isBot = SessionManager.Instance.IsSeatBot(i);
                if (isBot)
                    continue;

                bool won = (i == winnerSeat);
                var session = SessionManager.Instance.GetSessionBySeat(i);

                if (session != null)
                {
                    ProfileService.Instance.RecordGameResult(
                        currentGameType.Value,
                        seats[i].Score,
                        won
                    );
                }
            }

            Debug.Log("[CardGameManager] Game results saved to profiles");
            NotifyGameEventClientRpc(new GameEvent("Results saved to profiles!", -1));
        }
#pragma warning restore CS0618

        #endregion
    }
}

// Extension for Deck to support snapshot
namespace CardGameBuilder.Core
{
    public partial class Deck
    {
        public void Clear()
        {
            cards.Clear();
        }

        public void AddCard(Card card)
        {
            cards.Add(card);
        }

        public List<Card> GetRemainingCards()
        {
            return new List<Card>(cards);
        }
    }
}
