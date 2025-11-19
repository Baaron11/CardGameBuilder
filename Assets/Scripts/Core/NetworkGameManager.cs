using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using CardGameBuilder.UI;
using CardGameBuilder.Net;
using CardGameBuilder.Persistence;

namespace CardGameBuilder.Core
{
    /// <summary>
    /// Network Game Manager - Handles Netcode for GameObjects integration.
    /// Manages player connections, disconnections, and seat assignments.
    ///
    /// Setup Instructions:
    /// 1. Attach this to the same GameObject as Unity's NetworkManager
    /// 2. Ensure CardGameManager exists in the scene
    /// 3. NetworkManager should be configured with Unity Transport
    ///
    /// This script bridges Unity's networking with our card game logic.
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        #region Singleton

        public static NetworkGameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region Configuration

        [Header("References")]
        [SerializeField] private CardGameManager cardGameManager;
        [SerializeField] private BoardUI boardUI;

        [Header("Player Settings")]
        [SerializeField] private string defaultPlayerNamePrefix = "Player";

        #endregion

        #region Private State

        private Dictionary<ulong, string> connectedPlayers = new Dictionary<ulong, string>();
        private Dictionary<ulong, Guid> clientToPlayerId = new Dictionary<ulong, Guid>();
        private NetworkManager netManager;
        private SessionManager sessionManager;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            netManager = NetworkManager.Singleton;

            if (netManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager.Singleton is null! Make sure NetworkManager exists in the scene.");
                return;
            }

            // Find managers and UI if not assigned
            if (cardGameManager == null)
            {
                cardGameManager = FindObjectOfType<CardGameManager>();
            }

            if (boardUI == null)
            {
                boardUI = FindObjectOfType<BoardUI>();
            }

            sessionManager = SessionManager.Instance;

            // Subscribe to network events
            netManager.OnClientConnectedCallback += OnClientConnected;
            netManager.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log("[NetworkGameManager] Initialized and listening for network events");
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            // Unsubscribe from events
            if (netManager != null)
            {
                netManager.OnClientConnectedCallback -= OnClientConnected;
                netManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        #endregion

        #region Network Callbacks

        /// <summary>
        /// Called when a client connects to the server.
        /// M3: Supports reconnection via persistent player ID.
        /// </summary>
        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[NetworkGameManager] Client {clientId} connected");

            if (!IsServer) return;

            // Request player ID from client for reconnection support
            RequestPlayerIdClientRpc(clientId);
        }

        /// <summary>
        /// Complete player connection after receiving their player ID
        /// </summary>
        private void CompletePlayerConnection(ulong clientId, Guid playerId, string displayName)
        {
            if (!IsServer) return;

            connectedPlayers[clientId] = displayName;
            clientToPlayerId[clientId] = playerId;

            // Register with session manager (handles reconnection)
            int seatIndex = sessionManager.RegisterPlayer(clientId, playerId, displayName);

            // Assign to card game manager
            if (cardGameManager != null && seatIndex >= 0)
            {
                bool assigned = cardGameManager.AssignPlayerToSeat(clientId, displayName);

                if (assigned || seatIndex >= 0)
                {
                    Debug.Log($"[NetworkGameManager] {displayName} assigned to seat {seatIndex}");
                    NotifyPlayerAssignedClientRpc(clientId, displayName, seatIndex);
                }
                else
                {
                    Debug.LogWarning($"[NetworkGameManager] Failed to assign {displayName} - game may be full");
                }
            }

            // Update UI
            if (boardUI != null)
            {
                string reconnectTag = sessionManager.GetSessionByPlayerId(playerId)?.lastSeenTimestamp > 0 ? " (Reconnected)" : "";
                boardUI.AddEventLog($"{displayName} joined the game{reconnectTag}");
            }
        }

        /// <summary>
        /// Called when a client disconnects from the server.
        /// M3: Soft disconnect - preserves seat for reconnection.
        /// </summary>
        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[NetworkGameManager] Client {clientId} disconnected");

            if (!IsServer) return;

            string playerName = connectedPlayers.ContainsKey(clientId)
                ? connectedPlayers[clientId]
                : $"Client{clientId}";

            // Notify session manager (soft disconnect - allows reconnection)
            sessionManager.OnPlayerDisconnected(clientId);

            // DON'T remove from seat immediately - allow reconnection
            // Only mark as disconnected in UI

            // Update UI
            if (boardUI != null)
            {
                boardUI.AddEventLog($"{playerName} disconnected (seat reserved for 5 min)");
            }

            Debug.Log($"[NetworkGameManager] {playerName} disconnected - seat reserved");
        }

        /// <summary>
        /// Permanently remove a player (timeout or kick)
        /// </summary>
        public void RemovePlayer(ulong clientId)
        {
            if (!IsServer) return;

            string playerName = connectedPlayers.ContainsKey(clientId)
                ? connectedPlayers[clientId]
                : $"Client{clientId}";

            // Remove from session manager
            if (clientToPlayerId.TryGetValue(clientId, out Guid playerId))
            {
                sessionManager.RemovePlayer(playerId);
                clientToPlayerId.Remove(clientId);
            }

            // Remove from card game manager
            if (cardGameManager != null)
            {
                cardGameManager.RemovePlayerFromSeat(clientId);
            }

            // Remove from tracking
            connectedPlayers.Remove(clientId);

            // Update UI
            if (boardUI != null)
            {
                boardUI.AddEventLog($"{playerName} permanently removed");
            }

            Debug.Log($"[NetworkGameManager] {playerName} permanently removed");
        }

        #endregion

        #region ClientRpc / ServerRpc

        /// <summary>
        /// [ClientRpc] Request player ID from connecting client
        /// </summary>
        [ClientRpc]
        private void RequestPlayerIdClientRpc(ulong targetClientId)
        {
            // Only process if this is for the local client
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            // Send our player ID and display name to server
            var profile = ProfileService.Instance.GetProfile();
            SendPlayerIdServerRpc(profile.PlayerId, profile.displayName);
        }

        /// <summary>
        /// [ServerRpc] Receive player ID from client for reconnection
        /// </summary>
#pragma warning disable CS0618
        [ServerRpc(RequireOwnership = false)]
        private void SendPlayerIdServerRpc(Guid playerId, string displayName, ServerRpcParams serverRpcParams = default)
        {
            ulong clientId = serverRpcParams.Receive.SenderClientId;
            CompletePlayerConnection(clientId, playerId, displayName);
        }
#pragma warning restore CS0618

        /// <summary>
        /// [ClientRpc] Notifies a client that they've been assigned to a seat.
        /// </summary>
        [ClientRpc]
        private void NotifyPlayerAssignedClientRpc(ulong clientId, string playerName, int seatIndex)
        {
            // Only process if this is for the local client
            if (NetworkManager.Singleton.LocalClientId != clientId) return;

            Debug.Log($"[NetworkGameManager] You are {playerName} at seat {seatIndex}");

            // Update local UI
            ControllerUI controllerUI = FindObjectOfType<ControllerUI>();
            if (controllerUI != null)
            {
                controllerUI.SetSeatIndex(seatIndex);
            }
        }

        #endregion

        #region Public API - Host Management

        /// <summary>
        /// Starts the game as a host (server + client).
        /// </summary>
        public void StartHost()
        {
            if (netManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager not found!");
                return;
            }

            bool success = netManager.StartHost();

            if (success)
            {
                Debug.Log("[NetworkGameManager] Started as Host");

                if (boardUI != null)
                {
                    boardUI.SetHostMode(true);
                    boardUI.AddEventLog("Started as Host - waiting for players...");
                }
            }
            else
            {
                Debug.LogError("[NetworkGameManager] Failed to start as Host");
            }
        }

        /// <summary>
        /// Starts as a client and connects to the host.
        /// </summary>
        public void StartClient()
        {
            if (netManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager not found!");
                return;
            }

            bool success = netManager.StartClient();

            if (success)
            {
                Debug.Log("[NetworkGameManager] Started as Client - connecting to host...");
            }
            else
            {
                Debug.LogError("[NetworkGameManager] Failed to start as Client");
            }
        }

        /// <summary>
        /// Starts as a dedicated server (no local player).
        /// </summary>
        public void StartServer()
        {
            if (netManager == null)
            {
                Debug.LogError("[NetworkGameManager] NetworkManager not found!");
                return;
            }

            bool success = netManager.StartServer();

            if (success)
            {
                Debug.Log("[NetworkGameManager] Started as Server");

                if (boardUI != null)
                {
                    boardUI.AddEventLog("Started as Server - waiting for players...");
                }
            }
            else
            {
                Debug.LogError("[NetworkGameManager] Failed to start as Server");
            }
        }

        /// <summary>
        /// Shuts down the current network session.
        /// </summary>
        public void Shutdown()
        {
            if (netManager == null) return;

            netManager.Shutdown();
            Debug.Log("[NetworkGameManager] Network shutdown");

            if (boardUI != null)
            {
                boardUI.AddEventLog("Network session ended");
            }
        }

        #endregion

        #region Public API - Player Info

        /// <summary>
        /// Gets the local player's client ID.
        /// </summary>
        public ulong GetLocalClientId()
        {
            return netManager != null ? netManager.LocalClientId : 0;
        }

        /// <summary>
        /// Gets the local player's seat index.
        /// </summary>
        public int GetLocalSeatIndex()
        {
            if (netManager == null || cardGameManager == null)
                return -1;

            ulong clientId = netManager.LocalClientId;
            return cardGameManager.GetSeatIndexForClient(clientId);
        }

        /// <summary>
        /// Checks if the local player is the host.
        /// </summary>
        public bool IsLocalPlayerHost()
        {
            return netManager != null && netManager.IsHost;
        }

        /// <summary>
        /// Gets the number of connected players.
        /// </summary>
        public int GetConnectedPlayerCount()
        {
            return connectedPlayers.Count;
        }

        #endregion
    }
}
