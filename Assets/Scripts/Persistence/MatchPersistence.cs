using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CardGameBuilder.Core;

namespace CardGameBuilder.Persistence
{
    /// <summary>
    /// Configuration for game rules (win conditions, hand sizes, etc.)
    /// </summary>
    [Serializable]
    public class RulesConfig
    {
        public int winTarget = 10;          // For War: rounds to win; GoFish: books to win
        public int initialHandSize = 5;     // For Go Fish (0 = use default)
        public int maxPlayers = 4;
        public bool enableBots = false;
        public int botCount = 0;
        public float turnTimeLimit = 60f;   // Seconds (0 = no limit)

        public RulesConfig() { }

        public RulesConfig(int winTarget, int handSize = 5, int maxPlayers = 4)
        {
            this.winTarget = winTarget;
            this.initialHandSize = handSize;
            this.maxPlayers = maxPlayers;
        }

        public static RulesConfig Default(GameType gameType)
        {
            switch (gameType)
            {
                case GameType.War:
                    return new RulesConfig(5, 0, 4);  // First to 5 rounds
                case GameType.GoFish:
                    return new RulesConfig(13, 5, 4); // All 13 books
                case GameType.Hearts:
                    return new RulesConfig(100, 13, 4); // First to 100 pts loses
                default:
                    return new RulesConfig();
            }
        }
    }

    /// <summary>
    /// Serializable snapshot of a player's seat state
    /// </summary>
    [Serializable]
    public class SeatSnapshot
    {
        public int seatIndex;
        public string playerId;         // Guid as string
        public ulong clientId;
        public string playerName;
        public bool isActive;
        public bool isBot;
        public int score;
        public int tricksWon;
        public List<int> hand;          // Card IDs (0-51)

        public SeatSnapshot() { }

        public SeatSnapshot(PlayerSeat seat)
        {
            seatIndex = seat.SeatIndex;
            playerId = Guid.Empty.ToString(); // Will be set by SessionManager
            clientId = seat.ClientId;
            playerName = seat.PlayerName;
            isActive = seat.IsActive;
            isBot = false;
            score = seat.Score;
            tricksWon = seat.TricksWon;
            hand = seat.Hand.Select(c => c.CardId).ToList();
        }

        public void ApplyToSeat(PlayerSeat seat)
        {
            seat.SeatIndex = seatIndex;
            seat.ClientId = clientId;
            seat.PlayerName = playerName;
            seat.IsActive = isActive;
            seat.Score = score;
            seat.TricksWon = tricksWon;
            seat.Hand = hand.Select(id => Card.FromId(id)).ToList();
        }
    }

    /// <summary>
    /// Complete snapshot of game state for save/load and reconnection
    /// </summary>
    [Serializable]
    public class MatchSnapshot
    {
        public string matchId;
        public GameType gameType;
        public GameState gameState;
        public int seed;
        public int roundNumber;
        public int activeSeatIndex;
        public int dealerSeatIndex;

        public List<int> deckCards;         // Remaining cards in deck (CardIds)
        public List<int> discardPile;       // Discard pile (CardIds)
        public List<SeatSnapshot> seats;    // Player seats with hands
        public RulesConfig rulesConfig;

        public long savedTimestamp;
        public string roomName;
        public int maxPlayers;

        // For host migration
        public string originalHostId;
        public string currentHostId;

        public MatchSnapshot()
        {
            matchId = Guid.NewGuid().ToString();
            deckCards = new List<int>();
            discardPile = new List<int>();
            seats = new List<SeatSnapshot>();
            rulesConfig = new RulesConfig();
            savedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public MatchSnapshot(
            GameType gameType,
            GameState gameState,
            int seed,
            int roundNumber,
            int activeSeatIndex,
            Deck deck,
            List<Card> discard,
            List<PlayerSeat> playerSeats,
            RulesConfig config,
            string roomName = "Game Room")
        {
            matchId = Guid.NewGuid().ToString();
            this.gameType = gameType;
            this.gameState = gameState;
            this.seed = seed;
            this.roundNumber = roundNumber;
            this.activeSeatIndex = activeSeatIndex;
            this.dealerSeatIndex = 0;

            deckCards = deck != null ? deck.GetRemainingCards().Select(c => c.CardId).ToList() : new List<int>();
            discardPile = discard != null ? discard.Select(c => c.CardId).ToList() : new List<int>();
            seats = playerSeats.Select(s => new SeatSnapshot(s)).ToList();
            rulesConfig = config ?? RulesConfig.Default(gameType);

            this.roomName = roomName;
            maxPlayers = playerSeats.Count;
            savedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            originalHostId = "";
            currentHostId = "";
        }
    }

    /// <summary>
    /// Service for saving and loading match snapshots
    /// </summary>
    public class MatchPersistence : MonoBehaviour
    {
        private static MatchPersistence _instance;
        public static MatchPersistence Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MatchPersistence");
                    _instance = go.AddComponent<MatchPersistence>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private const string SAVE_FOLDER = "Matches";
        private const string AUTOSAVE_FILENAME = "autosave_match.json";
        private string SaveFolderPath => Path.Combine(Application.persistentDataPath, SAVE_FOLDER);

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Ensure save folder exists
            if (!Directory.Exists(SaveFolderPath))
            {
                Directory.CreateDirectory(SaveFolderPath);
                Debug.Log($"[MatchPersistence] Created save folder: {SaveFolderPath}");
            }
        }

        /// <summary>
        /// Save a match snapshot to disk
        /// </summary>
        public bool SaveSnapshot(MatchSnapshot snapshot, string filename = null)
        {
            try
            {
                if (snapshot == null)
                {
                    Debug.LogError("[MatchPersistence] Cannot save null snapshot");
                    return false;
                }

                if (string.IsNullOrEmpty(filename))
                {
                    filename = $"match_{snapshot.gameType}_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.json";
                }

                string path = Path.Combine(SaveFolderPath, filename);
                string json = JsonUtility.ToJson(snapshot, true);
                File.WriteAllText(path, json);

                Debug.Log($"[MatchPersistence] Saved match to: {path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchPersistence] Error saving snapshot: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load a match snapshot from disk
        /// </summary>
        public MatchSnapshot LoadSnapshot(string filename)
        {
            try
            {
                string path = Path.Combine(SaveFolderPath, filename);

                if (!File.Exists(path))
                {
                    Debug.LogError($"[MatchPersistence] File not found: {path}");
                    return null;
                }

                string json = File.ReadAllText(path);
                MatchSnapshot snapshot = JsonUtility.FromJson<MatchSnapshot>(json);

                Debug.Log($"[MatchPersistence] Loaded match: {snapshot.gameType} (Round {snapshot.roundNumber})");
                return snapshot;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchPersistence] Error loading snapshot: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Auto-save current match (for crash recovery)
        /// </summary>
        public bool AutoSave(MatchSnapshot snapshot)
        {
            return SaveSnapshot(snapshot, AUTOSAVE_FILENAME);
        }

        /// <summary>
        /// Load the most recent auto-save
        /// </summary>
        public MatchSnapshot LoadAutoSave()
        {
            return LoadSnapshot(AUTOSAVE_FILENAME);
        }

        /// <summary>
        /// Check if auto-save exists
        /// </summary>
        public bool HasAutoSave()
        {
            string path = Path.Combine(SaveFolderPath, AUTOSAVE_FILENAME);
            return File.Exists(path);
        }

        /// <summary>
        /// Get all saved match files
        /// </summary>
        public List<string> GetSavedMatches()
        {
            try
            {
                if (!Directory.Exists(SaveFolderPath))
                    return new List<string>();

                var files = Directory.GetFiles(SaveFolderPath, "*.json")
                    .Select(Path.GetFileName)
                    .Where(f => f != AUTOSAVE_FILENAME)
                    .OrderByDescending(f => File.GetLastWriteTime(Path.Combine(SaveFolderPath, f)))
                    .ToList();

                return files;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchPersistence] Error listing saves: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Get the most recently modified snapshot file
        /// </summary>
        public string GetLatestSnapshotPath()
        {
            var files = GetSavedMatches();
            return files.Count > 0 ? files[0] : null;
        }

        /// <summary>
        /// Delete a saved match
        /// </summary>
        public bool DeleteSnapshot(string filename)
        {
            try
            {
                string path = Path.Combine(SaveFolderPath, filename);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Debug.Log($"[MatchPersistence] Deleted: {filename}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchPersistence] Error deleting snapshot: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get match info without fully loading it
        /// </summary>
        public string GetMatchInfo(string filename)
        {
            try
            {
                var snapshot = LoadSnapshot(filename);
                if (snapshot == null) return "Error loading match";

                var date = DateTimeOffset.FromUnixTimeSeconds(snapshot.savedTimestamp).ToLocalTime();
                int activePlayers = snapshot.seats.Count(s => s.isActive);

                return $"{snapshot.gameType} | Round {snapshot.roundNumber} | {activePlayers} players | {date:g}";
            }
            catch
            {
                return "Invalid save file";
            }
        }

        /// <summary>
        /// Export snapshot to specific path (for host migration)
        /// </summary>
        public bool ExportSnapshot(MatchSnapshot snapshot, string fullPath)
        {
            try
            {
                string json = JsonUtility.ToJson(snapshot, true);
                File.WriteAllText(fullPath, json);
                Debug.Log($"[MatchPersistence] Exported to: {fullPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchPersistence] Export error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Import snapshot from external path
        /// </summary>
        public MatchSnapshot ImportSnapshot(string fullPath)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    Debug.LogError($"[MatchPersistence] Import file not found: {fullPath}");
                    return null;
                }

                string json = File.ReadAllText(fullPath);
                var snapshot = JsonUtility.FromJson<MatchSnapshot>(json);
                Debug.Log($"[MatchPersistence] Imported: {snapshot.gameType}");
                return snapshot;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MatchPersistence] Import error: {ex.Message}");
                return null;
            }
        }
    }
}
