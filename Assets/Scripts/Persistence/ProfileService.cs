using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CardGameBuilder.Persistence
{
    /// <summary>
    /// Player profile with persistent stats across sessions
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        public string playerId;  // Serialized as string for JSON compatibility
        public string displayName;
        public int gamesPlayed;
        public int wins;
        public Dictionary<string, int> bestScores;  // GameType.ToString() -> score
        public long createdTimestamp;
        public long lastPlayedTimestamp;

        [NonSerialized]
        private Guid _playerIdGuid;

        public Guid PlayerId
        {
            get
            {
                if (_playerIdGuid == Guid.Empty && !string.IsNullOrEmpty(playerId))
                {
                    _playerIdGuid = Guid.Parse(playerId);
                }
                return _playerIdGuid;
            }
            set
            {
                _playerIdGuid = value;
                playerId = value.ToString();
            }
        }

        public PlayerProfile()
        {
            PlayerId = Guid.NewGuid();
            displayName = "Player";
            gamesPlayed = 0;
            wins = 0;
            bestScores = new Dictionary<string, int>();
            createdTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            lastPlayedTimestamp = createdTimestamp;
        }

        public PlayerProfile(string name) : this()
        {
            displayName = name;
        }

        public void RecordGamePlayed(GameType gameType, int score, bool won)
        {
            gamesPlayed++;
            if (won) wins++;
            lastPlayedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            string gameTypeKey = gameType.ToString();

            // For Hearts, lower is better; for others, higher is better
            bool isBetter = gameType == GameType.Hearts
                ? (!bestScores.ContainsKey(gameTypeKey) || score < bestScores[gameTypeKey])
                : (!bestScores.ContainsKey(gameTypeKey) || score > bestScores[gameTypeKey]);

            if (isBetter)
            {
                bestScores[gameTypeKey] = score;
            }
        }

        public int GetBestScore(GameType gameType)
        {
            string key = gameType.ToString();
            return bestScores.ContainsKey(key) ? bestScores[key] : 0;
        }
    }

    /// <summary>
    /// Singleton service for managing player profile persistence
    /// </summary>
    public class ProfileService : MonoBehaviour
    {
        private static ProfileService _instance;
        public static ProfileService Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("ProfileService");
                    _instance = go.AddComponent<ProfileService>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private const string PROFILE_FILENAME = "player_profile.json";
        private PlayerProfile _currentProfile;
        private string ProfilePath => Path.Combine(Application.persistentDataPath, PROFILE_FILENAME);

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            LoadProfile();
        }

        /// <summary>
        /// Get the current player profile, creating one if it doesn't exist
        /// </summary>
        public PlayerProfile GetProfile()
        {
            if (_currentProfile == null)
            {
                LoadProfile();
            }
            return _currentProfile;
        }

        /// <summary>
        /// Load profile from disk, or create new one if none exists
        /// </summary>
        public void LoadProfile()
        {
            try
            {
                if (File.Exists(ProfilePath))
                {
                    string json = File.ReadAllText(ProfilePath);
                    _currentProfile = JsonUtility.FromJson<PlayerProfile>(json);

                    // Ensure PlayerId Guid is parsed
                    if (_currentProfile.PlayerId == Guid.Empty && !string.IsNullOrEmpty(_currentProfile.playerId))
                    {
                        _currentProfile.PlayerId = Guid.Parse(_currentProfile.playerId);
                    }

                    Debug.Log($"[ProfileService] Loaded profile: {_currentProfile.displayName} (ID: {_currentProfile.PlayerId})");
                }
                else
                {
                    Debug.Log("[ProfileService] No profile found, creating new one");
                    _currentProfile = new PlayerProfile();
                    SaveProfile();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileService] Error loading profile: {ex.Message}. Creating new profile.");
                _currentProfile = new PlayerProfile();
                SaveProfile();
            }
        }

        /// <summary>
        /// Save current profile to disk
        /// </summary>
        public void SaveProfile()
        {
            try
            {
                if (_currentProfile == null)
                {
                    Debug.LogWarning("[ProfileService] No profile to save");
                    return;
                }

                string json = JsonUtility.ToJson(_currentProfile, true);
                File.WriteAllText(ProfilePath, json);
                Debug.Log($"[ProfileService] Saved profile: {_currentProfile.displayName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileService] Error saving profile: {ex.Message}");
            }
        }

        /// <summary>
        /// Update display name and persist immediately
        /// </summary>
        public void UpdateDisplayName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                Debug.LogWarning("[ProfileService] Cannot set empty display name");
                return;
            }

            _currentProfile.displayName = newName;
            SaveProfile();
            Debug.Log($"[ProfileService] Updated display name to: {newName}");
        }

        /// <summary>
        /// Record a completed game and update stats
        /// </summary>
        public void RecordGameResult(GameType gameType, int score, bool won)
        {
            _currentProfile.RecordGamePlayed(gameType, score, won);
            SaveProfile();
            Debug.Log($"[ProfileService] Recorded game: {gameType}, Score: {score}, Won: {won}");
        }

        /// <summary>
        /// Get stats summary for display
        /// </summary>
        public string GetStatsSummary()
        {
            var profile = GetProfile();
            float winRate = profile.gamesPlayed > 0
                ? (float)profile.wins / profile.gamesPlayed * 100f
                : 0f;

            return $"Games: {profile.gamesPlayed} | Wins: {profile.wins} | Win Rate: {winRate:F1}%";
        }

        /// <summary>
        /// Reset profile (for testing or user request)
        /// </summary>
        public void ResetProfile()
        {
            _currentProfile = new PlayerProfile();
            SaveProfile();
            Debug.Log("[ProfileService] Profile reset");
        }

        /// <summary>
        /// Get the profile file path for debugging
        /// </summary>
        public string GetProfilePath()
        {
            return ProfilePath;
        }
    }
}
