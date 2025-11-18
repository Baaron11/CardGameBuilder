using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Unity.Netcode;
using CardGameBuilder.Persistence;

namespace CardGameBuilder.Net
{
    /// <summary>
    /// Player session info for tracking across connections
    /// </summary>
    [Serializable]
    public class PlayerSession
    {
        public Guid playerId;
        public ulong clientId;
        public int seatIndex;
        public string displayName;
        public bool isReady;
        public bool isBot;
        public long connectedTimestamp;
        public long lastSeenTimestamp;

        public PlayerSession(Guid playerId, ulong clientId, int seatIndex, string displayName)
        {
            this.playerId = playerId;
            this.clientId = clientId;
            this.seatIndex = seatIndex;
            this.displayName = displayName;
            this.isReady = false;
            this.isBot = false;
            this.connectedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            this.lastSeenTimestamp = connectedTimestamp;
        }
    }

    /// <summary>
    /// Manages player sessions, reconnection, ready states, and room info
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        private static SessionManager _instance;
        public static SessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SessionManager");
                    _instance = go.AddComponent<SessionManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Session tracking
        private Dictionary<Guid, PlayerSession> _playerSessions = new Dictionary<Guid, PlayerSession>();
        private Dictionary<ulong, Guid> _clientIdToPlayerId = new Dictionary<ulong, Guid>();
        private Dictionary<int, Guid> _seatToPlayerId = new Dictionary<int, Guid>();

        // Room info
        private string _roomName = "Game Room";
        private string _roomCode = "";
        private int _maxPlayers = 4;
        private bool _allowBots = false;

        // Events
        public event Action<Guid, int> OnPlayerSeated;
        public event Action<Guid, int> OnPlayerLeft;
        public event Action<Guid, bool> OnPlayerReadyChanged;
        public event Action OnAllPlayersReady;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #region Room Management

        public void SetRoomName(string name)
        {
            _roomName = string.IsNullOrWhiteSpace(name) ? "Game Room" : name;
            Debug.Log($"[SessionManager] Room name set to: {_roomName}");
        }

        public string GetRoomName() => _roomName;

        public void SetMaxPlayers(int max)
        {
            _maxPlayers = Mathf.Clamp(max, 2, 8);
            Debug.Log($"[SessionManager] Max players set to: {_maxPlayers}");
        }

        public int GetMaxPlayers() => _maxPlayers;

        public void SetAllowBots(bool allow)
        {
            _allowBots = allow;
        }

        public bool GetAllowBots() => _allowBots;

        /// <summary>
        /// Generate a short room code for easier sharing
        /// </summary>
        public string GenerateRoomCode()
        {
            // Simple 6-character alphanumeric code
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude similar chars (I/1, O/0)
            var random = new System.Random();
            _roomCode = new string(Enumerable.Range(0, 6)
                .Select(_ => chars[random.Next(chars.Length)])
                .ToArray());

            Debug.Log($"[SessionManager] Generated room code: {_roomCode}");
            return _roomCode;
        }

        public string GetRoomCode() => _roomCode;

        /// <summary>
        /// Get LAN IP address for clients to connect
        /// </summary>
        public string GetLanIpAddress()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionManager] Could not get LAN IP: {ex.Message}");
                return "127.0.0.1";
            }
        }

        #endregion

        #region Player Session Management

        /// <summary>
        /// Register a new player or reconnect an existing one
        /// </summary>
        public int RegisterPlayer(ulong clientId, Guid playerId, string displayName)
        {
            // Check if this player was previously connected
            if (_playerSessions.TryGetValue(playerId, out PlayerSession existingSession))
            {
                // Reconnection
                Debug.Log($"[SessionManager] Player {displayName} reconnecting to seat {existingSession.seatIndex}");

                // Update client ID (they may have a new connection)
                ulong oldClientId = existingSession.clientId;
                if (_clientIdToPlayerId.ContainsKey(oldClientId))
                {
                    _clientIdToPlayerId.Remove(oldClientId);
                }

                existingSession.clientId = clientId;
                existingSession.lastSeenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _clientIdToPlayerId[clientId] = playerId;

                OnPlayerSeated?.Invoke(playerId, existingSession.seatIndex);
                return existingSession.seatIndex;
            }
            else
            {
                // New player - find available seat
                int seatIndex = FindAvailableSeat();
                if (seatIndex == -1)
                {
                    Debug.LogWarning($"[SessionManager] No available seats for player {displayName}");
                    return -1;
                }

                PlayerSession session = new PlayerSession(playerId, clientId, seatIndex, displayName);
                _playerSessions[playerId] = session;
                _clientIdToPlayerId[clientId] = playerId;
                _seatToPlayerId[seatIndex] = playerId;

                Debug.Log($"[SessionManager] Registered new player {displayName} at seat {seatIndex}");
                OnPlayerSeated?.Invoke(playerId, seatIndex);
                return seatIndex;
            }
        }

        /// <summary>
        /// Handle player disconnection (soft - allow reconnection)
        /// </summary>
        public void OnPlayerDisconnected(ulong clientId)
        {
            if (!_clientIdToPlayerId.TryGetValue(clientId, out Guid playerId))
            {
                Debug.LogWarning($"[SessionManager] Unknown client disconnected: {clientId}");
                return;
            }

            if (_playerSessions.TryGetValue(playerId, out PlayerSession session))
            {
                session.lastSeenTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Debug.Log($"[SessionManager] Player {session.displayName} disconnected (seat {session.seatIndex} reserved)");

                // Don't remove session immediately - allow reconnection
                // Only remove clientId mapping
                _clientIdToPlayerId.Remove(clientId);
            }
        }

        /// <summary>
        /// Permanently remove a player (e.g., kicked or timeout)
        /// </summary>
        public void RemovePlayer(Guid playerId)
        {
            if (_playerSessions.TryGetValue(playerId, out PlayerSession session))
            {
                int seatIndex = session.seatIndex;
                _playerSessions.Remove(playerId);
                _clientIdToPlayerId.Remove(session.clientId);
                _seatToPlayerId.Remove(seatIndex);

                Debug.Log($"[SessionManager] Removed player {session.displayName} from seat {seatIndex}");
                OnPlayerLeft?.Invoke(playerId, seatIndex);
            }
        }

        /// <summary>
        /// Find the first available seat index
        /// </summary>
        private int FindAvailableSeat()
        {
            for (int i = 0; i < _maxPlayers; i++)
            {
                if (!_seatToPlayerId.ContainsKey(i))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Get player session by various lookups
        /// </summary>
        public PlayerSession GetSessionByPlayerId(Guid playerId)
        {
            return _playerSessions.TryGetValue(playerId, out var session) ? session : null;
        }

        public PlayerSession GetSessionByClientId(ulong clientId)
        {
            if (_clientIdToPlayerId.TryGetValue(clientId, out Guid playerId))
            {
                return GetSessionByPlayerId(playerId);
            }
            return null;
        }

        public PlayerSession GetSessionBySeat(int seatIndex)
        {
            if (_seatToPlayerId.TryGetValue(seatIndex, out Guid playerId))
            {
                return GetSessionByPlayerId(playerId);
            }
            return null;
        }

        public int GetSeatIndex(ulong clientId)
        {
            var session = GetSessionByClientId(clientId);
            return session?.seatIndex ?? -1;
        }

        public Guid GetPlayerId(ulong clientId)
        {
            return _clientIdToPlayerId.TryGetValue(clientId, out Guid playerId) ? playerId : Guid.Empty;
        }

        public List<PlayerSession> GetAllSessions()
        {
            return _playerSessions.Values.ToList();
        }

        public int GetConnectedPlayerCount()
        {
            return _clientIdToPlayerId.Count;
        }

        public int GetTotalPlayerCount()
        {
            return _playerSessions.Count;
        }

        #endregion

        #region Ready State Management

        public void SetPlayerReady(Guid playerId, bool ready)
        {
            if (_playerSessions.TryGetValue(playerId, out PlayerSession session))
            {
                session.isReady = ready;
                Debug.Log($"[SessionManager] Player {session.displayName} ready: {ready}");
                OnPlayerReadyChanged?.Invoke(playerId, ready);

                // Check if all players are ready
                CheckAllPlayersReady();
            }
        }

        public void SetPlayerReadyByClientId(ulong clientId, bool ready)
        {
            if (_clientIdToPlayerId.TryGetValue(clientId, out Guid playerId))
            {
                SetPlayerReady(playerId, ready);
            }
        }

        public bool IsPlayerReady(Guid playerId)
        {
            return _playerSessions.TryGetValue(playerId, out var session) && session.isReady;
        }

        public bool AreAllPlayersReady()
        {
            if (_playerSessions.Count < 2) return false;

            foreach (var session in _playerSessions.Values)
            {
                if (session.isBot) continue; // Bots are always "ready"
                if (!session.isReady) return false;
            }
            return true;
        }

        private void CheckAllPlayersReady()
        {
            if (AreAllPlayersReady())
            {
                Debug.Log("[SessionManager] All players ready!");
                OnAllPlayersReady?.Invoke();
            }
        }

        public void ResetAllReady()
        {
            foreach (var session in _playerSessions.Values)
            {
                session.isReady = false;
            }
            Debug.Log("[SessionManager] Reset all ready states");
        }

        #endregion

        #region Bot Management

        public bool AddBot(string botName = null)
        {
            if (!_allowBots)
            {
                Debug.LogWarning("[SessionManager] Bots are not allowed in this room");
                return false;
            }

            int seatIndex = FindAvailableSeat();
            if (seatIndex == -1)
            {
                Debug.LogWarning("[SessionManager] No available seats for bot");
                return false;
            }

            Guid botId = Guid.NewGuid();
            string name = botName ?? $"Bot {seatIndex + 1}";
            ulong fakeClientId = (ulong)(1000000 + seatIndex); // High client ID for bots

            PlayerSession botSession = new PlayerSession(botId, fakeClientId, seatIndex, name)
            {
                isBot = true,
                isReady = true
            };

            _playerSessions[botId] = botSession;
            _clientIdToPlayerId[fakeClientId] = botId;
            _seatToPlayerId[seatIndex] = botId;

            Debug.Log($"[SessionManager] Added bot {name} at seat {seatIndex}");
            OnPlayerSeated?.Invoke(botId, seatIndex);
            return true;
        }

        public bool RemoveBot(int seatIndex)
        {
            if (_seatToPlayerId.TryGetValue(seatIndex, out Guid playerId))
            {
                if (_playerSessions.TryGetValue(playerId, out PlayerSession session) && session.isBot)
                {
                    RemovePlayer(playerId);
                    return true;
                }
            }
            return false;
        }

        public bool IsSeatBot(int seatIndex)
        {
            var session = GetSessionBySeat(seatIndex);
            return session != null && session.isBot;
        }

        #endregion

        #region Snapshot Integration

        /// <summary>
        /// Enrich snapshot with player IDs from session data
        /// </summary>
        public void EnrichSnapshot(MatchSnapshot snapshot)
        {
            foreach (var seatSnapshot in snapshot.seats)
            {
                var session = GetSessionBySeat(seatSnapshot.seatIndex);
                if (session != null)
                {
                    seatSnapshot.playerId = session.playerId.ToString();
                    seatSnapshot.isBot = session.isBot;
                }
            }

            snapshot.roomName = _roomName;
            snapshot.maxPlayers = _maxPlayers;

            Debug.Log("[SessionManager] Enriched snapshot with session data");
        }

        /// <summary>
        /// Restore session state from snapshot (for host migration)
        /// </summary>
        public void RestoreFromSnapshot(MatchSnapshot snapshot)
        {
            _roomName = snapshot.roomName;
            _maxPlayers = snapshot.maxPlayers;

            // Clear current sessions
            _playerSessions.Clear();
            _clientIdToPlayerId.Clear();
            _seatToPlayerId.Clear();

            // Restore sessions from snapshot
            foreach (var seatSnapshot in snapshot.seats)
            {
                if (!seatSnapshot.isActive) continue;

                Guid playerId = Guid.Parse(seatSnapshot.playerId);
                ulong clientId = seatSnapshot.clientId;

                PlayerSession session = new PlayerSession(playerId, clientId, seatSnapshot.seatIndex, seatSnapshot.playerName)
                {
                    isBot = seatSnapshot.isBot,
                    isReady = false // Players need to ready up again after restore
                };

                _playerSessions[playerId] = session;
                _clientIdToPlayerId[clientId] = playerId;
                _seatToPlayerId[seatSnapshot.seatIndex] = playerId;
            }

            Debug.Log($"[SessionManager] Restored {_playerSessions.Count} sessions from snapshot");
        }

        #endregion

        #region Cleanup

        public void ClearAllSessions()
        {
            _playerSessions.Clear();
            _clientIdToPlayerId.Clear();
            _seatToPlayerId.Clear();
            Debug.Log("[SessionManager] Cleared all sessions");
        }

        void OnDestroy()
        {
            ClearAllSessions();
        }

        #endregion
    }
}
