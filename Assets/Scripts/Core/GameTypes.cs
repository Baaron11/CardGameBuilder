using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CardGameBuilder.Core
{
    /// <summary>
    /// Core data types and enums for the card game engine.
    /// All types are designed for network serialization and deterministic gameplay.
    /// </summary>

    #region Enums

    /// <summary>
    /// Available game types that can be played.
    /// </summary>
    public enum GameType
    {
        None = 0,
        War = 1,
        GoFish = 2,
        Hearts = 3
    }

    /// <summary>
    /// Card suits in a standard 52-card deck.
    /// </summary>
    public enum Suit
    {
        Hearts = 0,
        Diamonds = 1,
        Clubs = 2,
        Spades = 3
    }

    /// <summary>
    /// Card ranks from Ace (low=1) to King (13).
    /// Ace can be treated as high (14) in specific game rules.
    /// </summary>
    public enum Rank
    {
        Ace = 1,
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13
    }

    /// <summary>
    /// Action types players can perform during their turn.
    /// </summary>
    public enum ActionType
    {
        None = 0,
        Draw = 1,
        Play = 2,
        Discard = 3,
        // Game-specific actions
        Ask = 10,           // Go Fish: ask another player for a rank
        Pass = 11,          // General: pass turn without action
        FlipCard = 12       // War: flip top card
    }

    /// <summary>
    /// Current state of the game.
    /// </summary>
    public enum GameState
    {
        Waiting = 0,        // Waiting for players to join
        Starting = 1,       // Game is initializing
        InProgress = 2,     // Game is being played
        RoundEnd = 3,       // Round has ended, calculating scores
        GameOver = 4        // Game has concluded
    }

    #endregion

    #region Core Structs

    /// <summary>
    /// Represents a single playing card.
    /// Lightweight struct designed for network transmission and deterministic operations.
    /// </summary>
    [Serializable]
    public struct Card : IEquatable<Card>, INetworkSerializable
    {
        public Suit Suit;
        public Rank Rank;

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>
        /// Gets a unique card ID (0-51) for easy serialization.
        /// Format: suit * 13 + (rank - 1)
        /// </summary>
        public int CardId => (int)Suit * 13 + ((int)Rank - 1);

        /// <summary>
        /// Creates a card from its ID (0-51).
        /// </summary>
        public static Card FromId(int id)
        {
            if (id < 0 || id >= 52)
                throw new ArgumentException($"Invalid card ID: {id}. Must be 0-51.");

            int suit = id / 13;
            int rank = (id % 13) + 1;
            return new Card((Suit)suit, (Rank)rank);
        }

        /// <summary>
        /// Returns a human-readable card name (e.g., "Ace of Hearts").
        /// </summary>
        public override string ToString()
        {
            return $"{Rank} of {Suit}";
        }

        /// <summary>
        /// Short form for UI (e.g., "A♥", "K♠").
        /// </summary>
        public string ToShortString()
        {
            string rankStr = Rank switch
            {
                Rank.Ace => "A",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                _ => ((int)Rank).ToString()
            };

            string suitStr = Suit switch
            {
                Suit.Hearts => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs => "♣",
                Suit.Spades => "♠",
                _ => "?"
            };

            return rankStr + suitStr;
        }

        public bool Equals(Card other)
        {
            return Suit == other.Suit && Rank == other.Rank;
        }

        public override bool Equals(object obj)
        {
            return obj is Card other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Suit, Rank);
        }

        public static bool operator ==(Card left, Card right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Card left, Card right)
        {
            return !left.Equals(right);
        }

        // Network serialization
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Suit);
            serializer.SerializeValue(ref Rank);
        }
    }

    /// <summary>
    /// Represents a player's action request.
    /// Used for client -> server communication.
    /// </summary>
    [Serializable]
    public struct PlayerAction : INetworkSerializable
    {
        public ActionType Type;
        public Card Card;               // Card being played/discarded
        public int TargetSeatIndex;     // Target player for actions like Ask (Go Fish)
        public Rank TargetRank;         // Target rank for Go Fish

        public PlayerAction(ActionType type, Card card = default, int targetSeat = -1, Rank targetRank = Rank.Ace)
        {
            Type = type;
            Card = card;
            TargetSeatIndex = targetSeat;
            TargetRank = targetRank;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Card);
            serializer.SerializeValue(ref TargetSeatIndex);
            serializer.SerializeValue(ref TargetRank);
        }
    }

    /// <summary>
    /// Represents a game event to be broadcast to all clients.
    /// Used for server -> client notifications.
    /// </summary>
    [Serializable]
    public struct GameEvent : INetworkSerializable
    {
        public string Message;
        public int SeatIndex;           // Which seat triggered this event
        public Card Card;               // Associated card (if any)
        public int Value;               // Generic value (score, count, etc.)

        public GameEvent(string message, int seatIndex = -1, Card card = default, int value = 0)
        {
            Message = message;
            SeatIndex = seatIndex;
            Card = card;
            Value = value;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Message);
            serializer.SerializeValue(ref SeatIndex);
            serializer.SerializeValue(ref Card);
            serializer.SerializeValue(ref Value);
        }
    }

    /// <summary>
    /// Represents a player seat in the game.
    /// </summary>
    [Serializable]
    public class PlayerSeat
    {
        public int SeatIndex;
        public ulong ClientId;              // Netcode client ID
        public string PlayerName;
        public bool IsActive;               // Is this seat occupied?
        public List<Card> Hand;             // Cards in hand (server-side only for other players)
        public int Score;                   // Current score
        public int TricksWon;               // For trick-taking games like Hearts

        public PlayerSeat(int index)
        {
            SeatIndex = index;
            ClientId = ulong.MaxValue;
            PlayerName = $"Seat {index + 1}";
            IsActive = false;
            Hand = new List<Card>();
            Score = 0;
            TricksWon = 0;
        }

        public void Reset()
        {
            Hand.Clear();
            Score = 0;
            TricksWon = 0;
        }
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Standard 52-card deck with shuffle and deal operations.
    /// Uses deterministic shuffling with seed for network synchronization.
    /// </summary>
    public partial class Deck
    {
        protected List<Card> cards;
        private System.Random rng;

        public int CardsRemaining => cards.Count;

        public Deck()
        {
            cards = new List<Card>(52);
            Reset();
        }

        /// <summary>
        /// Resets deck to standard 52 cards (unshuffled).
        /// </summary>
        public void Reset()
        {
            cards.Clear();
            for (int suit = 0; suit < 4; suit++)
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    cards.Add(new Card((Suit)suit, (Rank)rank));
                }
            }
        }

        /// <summary>
        /// Shuffles the deck using a deterministic seed.
        /// Same seed = same shuffle order (critical for networked games).
        /// </summary>
        public void Shuffle(int seed)
        {
            rng = new System.Random(seed);

            // Fisher-Yates shuffle
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        /// <summary>
        /// Draws a card from the top of the deck.
        /// </summary>
        public Card Draw()
        {
            if (cards.Count == 0)
                throw new InvalidOperationException("Cannot draw from empty deck!");

            Card card = cards[0];
            cards.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Draws multiple cards.
        /// </summary>
        public List<Card> Draw(int count)
        {
            count = Mathf.Min(count, cards.Count);
            List<Card> drawn = new List<Card>(count);

            for (int i = 0; i < count; i++)
            {
                drawn.Add(Draw());
            }

            return drawn;
        }

        /// <summary>
        /// Returns a card to the bottom of the deck.
        /// </summary>
        public void ReturnToBottom(Card card)
        {
            cards.Add(card);
        }

        /// <summary>
        /// Returns multiple cards to the bottom of the deck.
        /// </summary>
        public void ReturnToBottom(List<Card> returnCards)
        {
            cards.AddRange(returnCards);
        }
    }

    /// <summary>
    /// Extension methods and utilities for card game logic.
    /// </summary>
    public static class CardGameUtility
    {
        /// <summary>
        /// Compares two cards by rank (for games like War).
        /// Returns: 1 if card1 wins, -1 if card2 wins, 0 if tie.
        /// </summary>
        public static int CompareRank(Card card1, Card card2, bool aceHigh = true)
        {
            int rank1 = (int)card1.Rank;
            int rank2 = (int)card2.Rank;

            // Treat Ace as high (14) if specified
            if (aceHigh)
            {
                if (rank1 == 1) rank1 = 14;
                if (rank2 == 1) rank2 = 14;
            }

            return rank1.CompareTo(rank2);
        }

        /// <summary>
        /// Gets the point value for a card in Hearts.
        /// Hearts = 1 point, Queen of Spades = 13 points.
        /// </summary>
        public static int GetHeartsPointValue(Card card)
        {
            if (card.Suit == Suit.Hearts)
                return 1;
            if (card.Suit == Suit.Spades && card.Rank == Rank.Queen)
                return 13;
            return 0;
        }

        /// <summary>
        /// Checks if a hand contains only cards of a specific rank.
        /// </summary>
        public static bool HasRank(List<Card> hand, Rank rank)
        {
            foreach (var card in hand)
            {
                if (card.Rank == rank)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets all cards of a specific rank from a hand.
        /// </summary>
        public static List<Card> GetCardsOfRank(List<Card> hand, Rank rank)
        {
            List<Card> result = new List<Card>();
            foreach (var card in hand)
            {
                if (card.Rank == rank)
                    result.Add(card);
            }
            return result;
        }

        /// <summary>
        /// Removes all cards of a specific rank from a hand.
        /// Returns the removed cards.
        /// </summary>
        public static List<Card> RemoveCardsOfRank(List<Card> hand, Rank rank)
        {
            List<Card> removed = new List<Card>();
            for (int i = hand.Count - 1; i >= 0; i--)
            {
                if (hand[i].Rank == rank)
                {
                    removed.Add(hand[i]);
                    hand.RemoveAt(i);
                }
            }
            return removed;
        }
    }

    #endregion
}
