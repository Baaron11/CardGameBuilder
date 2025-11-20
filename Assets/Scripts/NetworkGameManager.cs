using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace CardGameBuilder
{
    /// <summary>
    /// Server-authoritative game manager for the LAN tabletop card game.
    /// Manages 4 seats, a 52-card deck, and per-player 7-card private hands.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        // Game state
        private const int MAX_SEATS = 4;
        private const int CARDS_IN_DECK = 52;
        private const int INITIAL_HAND_SIZE = 7;

        // Server-side data structures
        private Dictionary<ulong, int> clientToSeat = new Dictionary<ulong, int>(); // clientId -> seatIndex
        private int[] seatOwners = new int[MAX_SEATS]; // seatIndex -> clientId (0 = empty, use ulong cast)
        private List<int> deck = new List<int>(); // Cards remaining in deck (0-51)
        private List<int> discardPile = new List<int>(); // Discarded cards
        private Dictionary<ulong, List<int>> playerHands = new Dictionary<ulong, List<int>>(); // clientId -> hand

        // Events for UI subscription
        public event Action<PublicGameState> OnPublicStateChanged;
        public event Action<List<int>> OnPrivateHandChanged;
        public event Action<string> OnErrorMessage;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                InitializeGame();
            }
        }

        #region Server Initialization

        private void InitializeGame()
        {
            // Initialize seats as empty
            for (int i = 0; i < MAX_SEATS; i++)
            {
                seatOwners[i] = 0; // 0 means empty
            }

            // Initialize deck with 52 cards
            deck.Clear();
            for (int i = 0; i < CARDS_IN_DECK; i++)
            {
                deck.Add(i);
            }

            // Shuffle deck
            ShuffleDeck();

            discardPile.Clear();
            playerHands.Clear();
            clientToSeat.Clear();

            Debug.Log("[Server] Game initialized with shuffled deck");
        }

        private void ShuffleDeck()
        {
            System.Random rng = new System.Random();
            int n = deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                int temp = deck[k];
                deck[k] = deck[n];
                deck[n] = temp;
            }
        }

        #endregion

        #region Server RPCs

        /// <summary>
        /// Client requests to claim a specific seat.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ClaimSeatServerRpc(int seatIndex, ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            // Validate seat index
            if (seatIndex < 0 || seatIndex >= MAX_SEATS)
            {
                SendErrorClientRpc("Invalid seat index",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                return;
            }

            // Check if seat is already taken
            if (seatOwners[seatIndex] != 0)
            {
                SendErrorClientRpc($"Seat {seatIndex} is already occupied",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                return;
            }

            // Check if client already has a seat
            if (clientToSeat.ContainsKey(clientId))
            {
                SendErrorClientRpc("You already have a seat. Leave it first.",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                return;
            }

            // Assign seat
            seatOwners[seatIndex] = (int)clientId;
            clientToSeat[clientId] = seatIndex;

            // Deal initial hand
            List<int> hand = new List<int>();
            for (int i = 0; i < INITIAL_HAND_SIZE && deck.Count > 0; i++)
            {
                int card = deck[0];
                deck.RemoveAt(0);
                hand.Add(card);
            }
            playerHands[clientId] = hand;

            Debug.Log($"[Server] Client {clientId} claimed seat {seatIndex} and received {hand.Count} cards");

            // Broadcast public state to all clients
            BroadcastPublicState();

            // Send private hand to this client only
            SendPrivateHandClientRpc(hand.ToArray(),
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }

        /// <summary>
        /// Client requests to leave their current seat.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void LeaveSeatServerRpc(ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            if (!clientToSeat.ContainsKey(clientId))
            {
                SendErrorClientRpc("You don't have a seat to leave",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                return;
            }

            int seatIndex = clientToSeat[clientId];

            // Return cards to discard pile
            if (playerHands.ContainsKey(clientId))
            {
                discardPile.AddRange(playerHands[clientId]);
                playerHands.Remove(clientId);
            }

            // Clear seat
            seatOwners[seatIndex] = 0;
            clientToSeat.Remove(clientId);

            Debug.Log($"[Server] Client {clientId} left seat {seatIndex}");

            // Broadcast updated state
            BroadcastPublicState();

            // Clear private hand for this client
            SendPrivateHandClientRpc(new int[0],
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }

        /// <summary>
        /// Client requests to reorder cards in their hand (from index -> to index).
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReorderHandServerRpc(int fromIndex, int toIndex, ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;

            if (!playerHands.ContainsKey(clientId))
            {
                SendErrorClientRpc("You don't have a hand to reorder",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                return;
            }

            List<int> hand = playerHands[clientId];

            // Validate indices
            if (fromIndex < 0 || fromIndex >= hand.Count || toIndex < 0 || toIndex >= hand.Count)
            {
                SendErrorClientRpc("Invalid card indices for reorder",
                    new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
                return;
            }

            // Reorder
            int card = hand[fromIndex];
            hand.RemoveAt(fromIndex);
            hand.Insert(toIndex, card);

            Debug.Log($"[Server] Client {clientId} reordered hand: {fromIndex} -> {toIndex}");

            // Send updated private hand
            SendPrivateHandClientRpc(hand.ToArray(),
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });
        }

        #endregion

        #region Client RPCs

        /// <summary>
        /// Broadcast public game state to all clients.
        /// </summary>
        [ClientRpc]
        private void UpdatePublicStateClientRpc(PublicGameState state)
        {
            OnPublicStateChanged?.Invoke(state);
        }

        /// <summary>
        /// Send private hand to a specific client.
        /// </summary>
        [ClientRpc]
        private void SendPrivateHandClientRpc(int[] hand, ClientRpcParams clientRpcParams = default)
        {
            OnPrivateHandChanged?.Invoke(hand.ToList());
        }

        /// <summary>
        /// Send error message to a specific client.
        /// </summary>
        [ClientRpc]
        private void SendErrorClientRpc(string message, ClientRpcParams clientRpcParams = default)
        {
            OnErrorMessage?.Invoke(message);
            Debug.LogWarning($"[Client] Error: {message}");
        }

        #endregion

        #region Helper Methods

        private void BroadcastPublicState()
        {
            if (!IsServer) return;

            PublicGameState state = new PublicGameState
            {
                seatOccupied = new bool[MAX_SEATS],
                seatPlayerNames = new string[MAX_SEATS],
                deckCount = deck.Count,
                discardCount = discardPile.Count,
                topDiscardCard = discardPile.Count > 0 ? discardPile[discardPile.Count - 1] : -1
            };

            for (int i = 0; i < MAX_SEATS; i++)
            {
                state.seatOccupied[i] = seatOwners[i] != 0;
                state.seatPlayerNames[i] = seatOwners[i] != 0 ? $"Player {seatOwners[i]}" : "Empty";
            }

            UpdatePublicStateClientRpc(state);
        }

        /// <summary>
        /// Called when a client disconnects (cleanup).
        /// </summary>
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnect;
            }
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (clientToSeat.ContainsKey(clientId))
            {
                int seatIndex = clientToSeat[clientId];

                // Return cards to discard
                if (playerHands.ContainsKey(clientId))
                {
                    discardPile.AddRange(playerHands[clientId]);
                    playerHands.Remove(clientId);
                }

                // Clear seat
                seatOwners[seatIndex] = 0;
                clientToSeat.Remove(clientId);

                Debug.Log($"[Server] Client {clientId} disconnected, freed seat {seatIndex}");

                // Broadcast updated state
                BroadcastPublicState();
            }
        }

        #endregion

        #region Public API for UI

        /// <summary>
        /// Get card display string (e.g., "A♠", "2♥", "K♣", "Q♦").
        /// Cards are numbered 0-51: 0-12 = Spades, 13-25 = Hearts, 26-38 = Clubs, 39-51 = Diamonds.
        /// </summary>
        public static string GetCardDisplay(int cardId)
        {
            if (cardId < 0 || cardId >= CARDS_IN_DECK) return "??";

            string[] suits = { "♠", "♥", "♣", "♦" };
            string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

            int suit = cardId / 13;
            int rank = cardId % 13;

            return ranks[rank] + suits[suit];
        }

        #endregion
    }

    /// <summary>
    /// Public game state visible to all clients.
    /// </summary>
    [Serializable]
    public struct PublicGameState : INetworkSerializable
    {
        public bool[] seatOccupied;
        public string[] seatPlayerNames;
        public int deckCount;
        public int discardCount;
        public int topDiscardCard; // -1 if empty

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int seatCount = seatOccupied?.Length ?? 0;
            serializer.SerializeValue(ref seatCount);

            if (serializer.IsReader)
            {
                seatOccupied = new bool[seatCount];
                seatPlayerNames = new string[seatCount];
            }

            for (int i = 0; i < seatCount; i++)
            {
                serializer.SerializeValue(ref seatOccupied[i]);
                serializer.SerializeValue(ref seatPlayerNames[i]);
            }

            serializer.SerializeValue(ref deckCount);
            serializer.SerializeValue(ref discardCount);
            serializer.SerializeValue(ref topDiscardCard);
        }
    }
}
