using System.Collections.Generic;
using System.Linq;
using CardGameBuilder.Core;
using UnityEngine;

namespace CardGameBuilder.Game
{
    /// <summary>
    /// Pure logic helpers for Go Fish game rules.
    /// All methods are static and side-effect free for easy testing.
    /// </summary>
    public static class GoFishRules
    {
        private const int CARDS_FOR_BOOK = 4;
        public const int INITIAL_HAND_SIZE = 5;

        /// <summary>
        /// Result of resolving an Ask action
        /// </summary>
        public struct AskResult
        {
            public int movedCount;          // How many cards were transferred
            public bool drew;               // Did player draw a card?
            public Card drawnCard;          // What card was drawn (if any)
            public bool drewRankMatches;    // Did drawn card match requested rank?
            public List<Rank> formedBooks;  // Any books formed (empty if none)
            public int nextTurnSeat;        // Who goes next
            public string actionLog;        // Descriptive message for UI

            public AskResult(int moved, bool didDraw, Card drawn, bool matchesRank, List<Rank> books, int nextSeat, string log)
            {
                movedCount = moved;
                drew = didDraw;
                drawnCard = drawn;
                drewRankMatches = matchesRank;
                formedBooks = books ?? new List<Rank>();
                nextTurnSeat = nextSeat;
                actionLog = log ?? "";
            }
        }

        /// <summary>
        /// Checks if a player can ask for a specific rank.
        /// Player must have at least one card of that rank in their hand.
        /// </summary>
        public static bool CanAsk(int currentSeat, int targetSeat, Rank rank, List<Card> hand)
        {
            // Can't ask yourself
            if (currentSeat == targetSeat)
                return false;

            // Must have at least one card of the requested rank
            return CardGameUtility.HasRank(hand, rank);
        }

        /// <summary>
        /// Resolves an Ask action and returns the complete result.
        /// This is the core Go Fish logic in one place.
        /// </summary>
        public static AskResult ResolveAsk(
            Dictionary<int, List<Card>> hands,
            Deck deck,
            ushort[] bookBitmasks,
            int currentSeat,
            int targetSeat,
            Rank rank,
            List<int> activeSeats,
            string[] playerNames)
        {
            List<Card> askerHand = hands[currentSeat];
            List<Card> targetHand = hands[targetSeat];
            List<Rank> formedBooks = new List<Rank>();

            string askerName = playerNames[currentSeat];
            string targetName = playerNames[targetSeat];

            // Check if target has the requested rank
            List<Card> matchingCards = CardGameUtility.GetCardsOfRank(targetHand, rank);

            if (matchingCards.Count > 0)
            {
                // Transfer cards from target to asker
                CardGameUtility.RemoveCardsOfRank(targetHand, rank);
                askerHand.AddRange(matchingCards);

                // Check for books in asker's hand
                formedBooks = CheckAndRemoveBooks(askerHand, bookBitmasks, currentSeat);

                // Player keeps their turn
                string log = $"{askerName} asked {targetName} for {rank}s: took {matchingCards.Count}";
                if (formedBooks.Count > 0)
                {
                    log += $" (made book: {string.Join(", ", formedBooks)})";
                }

                return new AskResult(
                    matchingCards.Count,
                    false,
                    default,
                    false,
                    formedBooks,
                    currentSeat, // Same player goes again
                    log
                );
            }
            else
            {
                // Go Fish! - Draw a card
                Card drawnCard = default;
                bool drewMatching = false;
                int nextSeat = GetNextSeat(currentSeat, activeSeats);

                if (deck.CardsRemaining > 0)
                {
                    drawnCard = deck.Draw();
                    askerHand.Add(drawnCard);
                    drewMatching = (drawnCard.Rank == rank);

                    // Check for books after drawing
                    formedBooks = CheckAndRemoveBooks(askerHand, bookBitmasks, currentSeat);

                    // If drew the rank they asked for, they go again
                    if (drewMatching)
                    {
                        nextSeat = currentSeat;
                    }
                }

                string log = $"{askerName} asked {targetName} for {rank}s: go fish";
                if (drewMatching)
                {
                    log += $", drew {drawnCard.Rank} (go again!)";
                }
                else if (deck.CardsRemaining > 0)
                {
                    log += $", drew card";
                }
                else
                {
                    log += $", deck empty";
                }

                if (formedBooks.Count > 0)
                {
                    log += $" (made book: {string.Join(", ", formedBooks)})";
                }

                return new AskResult(
                    0,
                    deck.CardsRemaining >= 0,
                    drawnCard,
                    drewMatching,
                    formedBooks,
                    nextSeat,
                    log
                );
            }
        }

        /// <summary>
        /// Checks a hand for any 4-of-a-kind books and removes them.
        /// Updates the book bitmask for the seat.
        /// Returns list of ranks that formed books.
        /// </summary>
        public static List<Rank> CheckAndRemoveBooks(List<Card> hand, ushort[] bookBitmasks, int seatIndex)
        {
            List<Rank> newBooks = new List<Rank>();

            for (int rankValue = 1; rankValue <= 13; rankValue++)
            {
                Rank rank = (Rank)rankValue;
                List<Card> cardsOfRank = CardGameUtility.GetCardsOfRank(hand, rank);

                if (cardsOfRank.Count == CARDS_FOR_BOOK)
                {
                    // Found a book! Remove from hand
                    CardGameUtility.RemoveCardsOfRank(hand, rank);

                    // Set bit in bitmask (Ace=bit0, King=bit12)
                    int bitIndex = rankValue - 1;
                    bookBitmasks[seatIndex] |= (ushort)(1 << bitIndex);

                    newBooks.Add(rank);
                    Debug.Log($"[GoFishRules] Seat {seatIndex} formed book of {rank}s");
                }
            }

            return newBooks;
        }

        /// <summary>
        /// Gets the number of books a player has from their bitmask.
        /// </summary>
        public static int CountBooks(ushort bookBitmask)
        {
            int count = 0;
            for (int i = 0; i < 13; i++)
            {
                if ((bookBitmask & (1 << i)) != 0)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Gets the list of rank names that are in a book bitmask.
        /// </summary>
        public static List<string> GetBookRanks(ushort bookBitmask)
        {
            List<string> ranks = new List<string>();
            for (int i = 0; i < 13; i++)
            {
                if ((bookBitmask & (1 << i)) != 0)
                {
                    Rank rank = (Rank)(i + 1);
                    ranks.Add(rank.ToString());
                }
            }
            return ranks;
        }

        /// <summary>
        /// Checks if the game is over (all 13 books made or all hands empty).
        /// </summary>
        public static bool IsGameOver(Dictionary<int, List<Card>> hands, int deckCount, ushort[] bookBitmasks)
        {
            // Count total books across all players
            int totalBooks = 0;
            foreach (var bitmask in bookBitmasks)
            {
                totalBooks += CountBooks(bitmask);
            }

            // All 13 books have been made
            if (totalBooks == 13)
                return true;

            // All hands are empty and deck is empty
            if (deckCount == 0)
            {
                bool anyCardsLeft = hands.Values.Any(hand => hand.Count > 0);
                if (!anyCardsLeft)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Determines the winner(s) based on book counts.
        /// Returns list of seat indices with the highest book count.
        /// </summary>
        public static List<int> GetWinners(ushort[] bookBitmasks, List<int> activeSeats)
        {
            int maxBooks = 0;
            List<int> winners = new List<int>();

            foreach (int seat in activeSeats)
            {
                int books = CountBooks(bookBitmasks[seat]);
                if (books > maxBooks)
                {
                    maxBooks = books;
                    winners.Clear();
                    winners.Add(seat);
                }
                else if (books == maxBooks)
                {
                    winners.Add(seat);
                }
            }

            return winners;
        }

        /// <summary>
        /// Gets the next active seat in turn order.
        /// </summary>
        private static int GetNextSeat(int currentSeat, List<int> activeSeats)
        {
            int currentIndex = activeSeats.IndexOf(currentSeat);
            if (currentIndex == -1)
                return activeSeats[0];

            int nextIndex = (currentIndex + 1) % activeSeats.Count;
            return activeSeats[nextIndex];
        }

        /// <summary>
        /// Deals initial hands for Go Fish.
        /// </summary>
        public static void DealInitialHands(Deck deck, Dictionary<int, List<Card>> hands, List<int> activeSeats)
        {
            foreach (int seat in activeSeats)
            {
                hands[seat] = new List<Card>();
                for (int i = 0; i < INITIAL_HAND_SIZE && deck.CardsRemaining > 0; i++)
                {
                    hands[seat].Add(deck.Draw());
                }
            }
        }

        /// <summary>
        /// Gets all unique ranks present in a hand (for UI dropdown).
        /// </summary>
        public static List<Rank> GetRanksInHand(List<Card> hand)
        {
            return hand.Select(c => c.Rank).Distinct().OrderBy(r => r).ToList();
        }

        /// <summary>
        /// Validates joining mid-game is blocked.
        /// </summary>
        public static bool CanJoinInProgress()
        {
            return false; // Go Fish doesn't allow mid-game joining
        }
    }
}
