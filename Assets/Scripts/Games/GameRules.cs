using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardGameBuilder.Core;

namespace CardGameBuilder.Games
{
    /// <summary>
    /// Interface for game-specific rules.
    /// Each game type implements this to define its unique logic.
    /// </summary>
    public interface IGameRules
    {
        /// <summary>
        /// Deals initial cards to all active players.
        /// </summary>
        void DealInitialCards(Deck deck, List<PlayerSeat> seats);

        /// <summary>
        /// Determines which player goes first.
        /// </summary>
        int GetFirstPlayer(List<PlayerSeat> seats);

        /// <summary>
        /// Processes a player action and updates game state.
        /// Returns true if action was valid and processed.
        /// </summary>
        bool ProcessAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager);

        /// <summary>
        /// Checks if the current round is over.
        /// </summary>
        bool IsRoundOver(List<PlayerSeat> seats);

        /// <summary>
        /// Checks if the entire game is over.
        /// </summary>
        bool IsGameOver(List<PlayerSeat> seats);

        /// <summary>
        /// Calculates and updates scores for all players.
        /// </summary>
        void CalculateScores(List<PlayerSeat> seats);
    }

    #region War Implementation

    /// <summary>
    /// Rules for War card game.
    ///
    /// Gameplay:
    /// 1. Each player flips their top card
    /// 2. Highest card wins all played cards
    /// 3. On tie, play continues (simplified - no "war" mechanic in this version)
    /// 4. Game ends when one player has all cards or deck is empty
    ///
    /// Scoring: Number of cards won
    /// </summary>
    public class WarRules : IGameRules
    {
        private List<Card> currentBattle = new List<Card>();
        private int[] cardsPlayedThisTurn;

        public void DealInitialCards(Deck deck, List<PlayerSeat> seats)
        {
            // Deal entire deck evenly to all active players
            List<PlayerSeat> activeSeats = seats.Where(s => s.IsActive).ToList();
            if (activeSeats.Count == 0) return;

            int cardIndex = 0;
            while (deck.CardsRemaining > 0)
            {
                Card card = deck.Draw();
                activeSeats[cardIndex % activeSeats.Count].Hand.Add(card);
                cardIndex++;
            }

            cardsPlayedThisTurn = new int[seats.Count];

            Debug.Log($"[WarRules] Dealt {cardIndex} cards to {activeSeats.Count} players");
        }

        public int GetFirstPlayer(List<PlayerSeat> seats)
        {
            // First active player starts
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                    return i;
            }
            return 0;
        }

        public bool ProcessAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager)
        {
            if (action.Type != ActionType.FlipCard && action.Type != ActionType.Play)
            {
                Debug.LogWarning($"[WarRules] Invalid action type: {action.Type}");
                return false;
            }

            PlayerSeat player = seats[seatIndex];

            // Check if player has cards
            if (player.Hand.Count == 0)
            {
                manager.NotifyGameEventClientRpc(new GameEvent($"{player.PlayerName} has no cards left!", seatIndex));
                return false;
            }

            // Play top card
            Card playedCard = player.Hand[0];
            player.Hand.RemoveAt(0);
            currentBattle.Add(playedCard);
            cardsPlayedThisTurn[seatIndex] = 1;

            Debug.Log($"[WarRules] {player.PlayerName} played {playedCard}");
            manager.NotifyGameEventClientRpc(new GameEvent($"{player.PlayerName} played {playedCard}", seatIndex, playedCard));

            // Check if all players have played
            List<PlayerSeat> activeSeats = seats.Where(s => s.IsActive).ToList();
            int playersActed = cardsPlayedThisTurn.Count(c => c > 0);

            if (playersActed == activeSeats.Count)
            {
                // Battle is complete - determine winner
                ResolveBattle(seats, manager);
            }

            return true;
        }

        private void ResolveBattle(List<PlayerSeat> seats, CardGameManager manager)
        {
            if (currentBattle.Count == 0) return;

            // Find highest card (Ace is high)
            int winnerIndex = -1;
            Card highestCard = currentBattle[0];
            int highestValue = GetCardValue(highestCard);

            int battleIndex = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                if (cardsPlayedThisTurn[i] > 0)
                {
                    Card card = currentBattle[battleIndex];
                    int value = GetCardValue(card);

                    if (value > highestValue)
                    {
                        highestValue = value;
                        highestCard = card;
                        winnerIndex = i;
                    }
                    else if (winnerIndex == -1)
                    {
                        winnerIndex = i;
                    }

                    battleIndex++;
                }
            }

            // Winner takes all cards
            if (winnerIndex != -1)
            {
                seats[winnerIndex].Hand.AddRange(currentBattle);
                seats[winnerIndex].Score += currentBattle.Count;

                manager.NotifyGameEventClientRpc(new GameEvent(
                    $"{seats[winnerIndex].PlayerName} wins with {highestCard}! Won {currentBattle.Count} cards.",
                    winnerIndex,
                    highestCard,
                    currentBattle.Count));

                Debug.Log($"[WarRules] {seats[winnerIndex].PlayerName} won the battle with {highestCard}");
            }

            // Reset for next battle
            currentBattle.Clear();
            for (int i = 0; i < cardsPlayedThisTurn.Length; i++)
            {
                cardsPlayedThisTurn[i] = 0;
            }
        }

        private int GetCardValue(Card card)
        {
            // Ace is high (14)
            return card.Rank == Rank.Ace ? 14 : (int)card.Rank;
        }

        public bool IsRoundOver(List<PlayerSeat> seats)
        {
            // Round ends when any player runs out of cards
            foreach (var seat in seats)
            {
                if (seat.IsActive && seat.Hand.Count == 0)
                    return true;
            }
            return false;
        }

        public bool IsGameOver(List<PlayerSeat> seats)
        {
            // Game ends after one round in this simplified version
            return IsRoundOver(seats);
        }

        public void CalculateScores(List<PlayerSeat> seats)
        {
            // Scores are already tracked during play (cards won)
            foreach (var seat in seats)
            {
                if (seat.IsActive)
                {
                    Debug.Log($"[WarRules] {seat.PlayerName}: {seat.Score} cards");
                }
            }
        }
    }

    #endregion

    #region Go Fish Implementation

    /// <summary>
    /// Rules for Go Fish card game.
    ///
    /// Gameplay:
    /// 1. Players start with 5-7 cards
    /// 2. On your turn, ask another player for a rank you have in your hand
    /// 3. If they have it, they give you all cards of that rank
    /// 4. If not, you "Go Fish" (draw a card)
    /// 5. When you collect all 4 cards of a rank, it's a "book" (set aside, +1 point)
    /// 6. Game ends when all books are made or deck is empty
    ///
    /// Scoring: Number of books (sets of 4) collected
    /// </summary>
    public class GoFishRules : IGameRules
    {
        private const int INITIAL_HAND_SIZE = 5;
        private const int CARDS_FOR_BOOK = 4;

        public void DealInitialCards(Deck deck, List<PlayerSeat> seats)
        {
            List<PlayerSeat> activeSeats = seats.Where(s => s.IsActive).ToList();

            foreach (var seat in activeSeats)
            {
                for (int i = 0; i < INITIAL_HAND_SIZE && deck.CardsRemaining > 0; i++)
                {
                    seat.Hand.Add(deck.Draw());
                }
                Debug.Log($"[GoFishRules] Dealt {seat.Hand.Count} cards to {seat.PlayerName}");
            }
        }

        public int GetFirstPlayer(List<PlayerSeat> seats)
        {
            // Youngest player traditionally goes first - we'll use first active seat
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                    return i;
            }
            return 0;
        }

        public bool ProcessAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager)
        {
            PlayerSeat player = seats[seatIndex];

            if (action.Type == ActionType.Ask)
            {
                return ProcessAskAction(action, seatIndex, seats, deck, manager);
            }
            else if (action.Type == ActionType.Draw)
            {
                return ProcessDrawAction(seatIndex, seats, deck, manager);
            }

            Debug.LogWarning($"[GoFishRules] Invalid action type: {action.Type}");
            return false;
        }

        private bool ProcessAskAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager)
        {
            PlayerSeat asker = seats[seatIndex];
            int targetSeat = action.TargetSeatIndex;
            Rank targetRank = action.TargetRank;

            // Validate target
            if (targetSeat < 0 || targetSeat >= seats.Count || !seats[targetSeat].IsActive || targetSeat == seatIndex)
            {
                manager.NotifyGameEventClientRpc(new GameEvent("Invalid target player!", seatIndex));
                return false;
            }

            // Check if asker has at least one card of the requested rank
            if (!CardGameUtility.HasRank(asker.Hand, targetRank))
            {
                manager.NotifyGameEventClientRpc(new GameEvent("You must have the rank you're asking for!", seatIndex));
                return false;
            }

            PlayerSeat target = seats[targetSeat];

            // Check if target has the requested rank
            List<Card> matchingCards = CardGameUtility.GetCardsOfRank(target.Hand, targetRank);

            if (matchingCards.Count > 0)
            {
                // Transfer cards
                CardGameUtility.RemoveCardsOfRank(target.Hand, targetRank);
                asker.Hand.AddRange(matchingCards);

                manager.NotifyGameEventClientRpc(new GameEvent(
                    $"{asker.PlayerName} asked {target.PlayerName} for {targetRank}s and got {matchingCards.Count} card(s)!",
                    seatIndex,
                    default,
                    matchingCards.Count));

                Debug.Log($"[GoFishRules] {asker.PlayerName} got {matchingCards.Count} {targetRank}s from {target.PlayerName}");

                // Check for books in asker's hand
                CheckAndRemoveBooks(asker, manager);

                // Asker gets another turn (don't advance turn)
                return true;
            }
            else
            {
                // Go Fish!
                manager.NotifyGameEventClientRpc(new GameEvent(
                    $"{asker.PlayerName} asked {target.PlayerName} for {targetRank}s. Go Fish!",
                    seatIndex));

                // Draw a card if deck has cards
                if (deck.CardsRemaining > 0)
                {
                    Card drawnCard = deck.Draw();
                    asker.Hand.Add(drawnCard);

                    manager.NotifyGameEventClientRpc(new GameEvent($"{asker.PlayerName} drew a card", seatIndex));

                    // Check if drawn card makes a book
                    CheckAndRemoveBooks(asker, manager);
                }

                return true;
            }
        }

        private bool ProcessDrawAction(int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager)
        {
            PlayerSeat player = seats[seatIndex];

            if (deck.CardsRemaining > 0)
            {
                Card drawnCard = deck.Draw();
                player.Hand.Add(drawnCard);

                manager.NotifyGameEventClientRpc(new GameEvent($"{player.PlayerName} drew a card", seatIndex));
                CheckAndRemoveBooks(player, manager);
                return true;
            }

            manager.NotifyGameEventClientRpc(new GameEvent("Deck is empty!", seatIndex));
            return false;
        }

        private void CheckAndRemoveBooks(PlayerSeat player, CardGameManager manager)
        {
            // Check each rank for a set of 4
            for (int rank = 1; rank <= 13; rank++)
            {
                Rank checkRank = (Rank)rank;
                List<Card> cardsOfRank = CardGameUtility.GetCardsOfRank(player.Hand, checkRank);

                if (cardsOfRank.Count == CARDS_FOR_BOOK)
                {
                    // Found a book!
                    CardGameUtility.RemoveCardsOfRank(player.Hand, checkRank);
                    player.Score++;

                    manager.NotifyGameEventClientRpc(new GameEvent(
                        $"{player.PlayerName} made a book of {checkRank}s!",
                        player.SeatIndex,
                        default,
                        player.Score));

                    Debug.Log($"[GoFishRules] {player.PlayerName} completed a book of {checkRank}s. Total books: {player.Score}");
                }
            }
        }

        public bool IsRoundOver(List<PlayerSeat> seats)
        {
            // Game ends when all cards are in books or players have no cards
            int totalCards = 0;
            int totalBooks = 0;

            foreach (var seat in seats)
            {
                if (seat.IsActive)
                {
                    totalCards += seat.Hand.Count;
                    totalBooks += seat.Score;
                }
            }

            // 13 possible books (Ace through King)
            return totalBooks == 13 || totalCards == 0;
        }

        public bool IsGameOver(List<PlayerSeat> seats)
        {
            return IsRoundOver(seats);
        }

        public void CalculateScores(List<PlayerSeat> seats)
        {
            // Scores are already tracked (number of books)
            foreach (var seat in seats)
            {
                if (seat.IsActive)
                {
                    Debug.Log($"[GoFishRules] {seat.PlayerName}: {seat.Score} books");
                }
            }
        }
    }

    #endregion

    #region Hearts Implementation

    /// <summary>
    /// Simplified Hearts card game rules.
    ///
    /// Gameplay:
    /// 1. Each player gets 13 cards (for 4 players)
    /// 2. Player with 2 of Clubs leads first trick
    /// 3. Must follow suit if possible
    /// 4. Highest card of led suit wins trick
    /// 5. Hearts = 1 point, Queen of Spades = 13 points
    /// 6. Lowest score wins
    ///
    /// Simplified: No passing cards, no shooting the moon, basic trick-taking only
    /// </summary>
    public class HeartsRules : IGameRules
    {
        private Suit leadSuit = Suit.Hearts;
        private List<Card> currentTrick = new List<Card>();
        private List<int> playersInTrick = new List<int>();
        private int leadPlayer = -1;

        public void DealInitialCards(Deck deck, List<PlayerSeat> seats)
        {
            List<PlayerSeat> activeSeats = seats.Where(s => s.IsActive).ToList();

            // Deal all cards (13 per player for 4 players)
            int cardIndex = 0;
            while (deck.CardsRemaining > 0)
            {
                Card card = deck.Draw();
                activeSeats[cardIndex % activeSeats.Count].Hand.Add(card);
                cardIndex++;
            }

            Debug.Log($"[HeartsRules] Dealt {cardIndex} cards to {activeSeats.Count} players");
        }

        public int GetFirstPlayer(List<PlayerSeat> seats)
        {
            // Player with 2 of Clubs starts
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                {
                    foreach (var card in seats[i].Hand)
                    {
                        if (card.Suit == Suit.Clubs && card.Rank == Rank.Two)
                        {
                            Debug.Log($"[HeartsRules] {seats[i].PlayerName} has 2 of Clubs, starts first");
                            return i;
                        }
                    }
                }
            }

            // Fallback: first active player
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                    return i;
            }

            return 0;
        }

        public bool ProcessAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager)
        {
            if (action.Type != ActionType.Play)
            {
                Debug.LogWarning($"[HeartsRules] Invalid action type: {action.Type}");
                return false;
            }

            PlayerSeat player = seats[seatIndex];
            Card playedCard = action.Card;

            // Verify player has this card
            if (!player.Hand.Contains(playedCard))
            {
                manager.NotifyGameEventClientRpc(new GameEvent("You don't have that card!", seatIndex));
                return false;
            }

            // First card of trick sets the lead suit
            if (currentTrick.Count == 0)
            {
                leadSuit = playedCard.Suit;
                leadPlayer = seatIndex;
                Debug.Log($"[HeartsRules] {player.PlayerName} leads with {playedCard} ({leadSuit})");
            }
            else
            {
                // Must follow suit if possible
                bool hasLeadSuit = player.Hand.Any(c => c.Suit == leadSuit);
                if (hasLeadSuit && playedCard.Suit != leadSuit)
                {
                    manager.NotifyGameEventClientRpc(new GameEvent("Must follow suit!", seatIndex));
                    return false;
                }
            }

            // Play the card
            player.Hand.Remove(playedCard);
            currentTrick.Add(playedCard);
            playersInTrick.Add(seatIndex);

            manager.NotifyGameEventClientRpc(new GameEvent(
                $"{player.PlayerName} played {playedCard}",
                seatIndex,
                playedCard));

            // Check if trick is complete
            List<PlayerSeat> activeSeats = seats.Where(s => s.IsActive).ToList();
            if (currentTrick.Count == activeSeats.Count)
            {
                ResolveTrick(seats, manager);
            }

            return true;
        }

        private void ResolveTrick(List<PlayerSeat> seats, CardGameManager manager)
        {
            if (currentTrick.Count == 0) return;

            // Find highest card of lead suit
            int winnerIndex = playersInTrick[0];
            Card winningCard = currentTrick[0];
            int highestRank = GetCardRankValue(winningCard);

            for (int i = 1; i < currentTrick.Count; i++)
            {
                Card card = currentTrick[i];
                if (card.Suit == leadSuit)
                {
                    int rank = GetCardRankValue(card);
                    if (rank > highestRank)
                    {
                        highestRank = rank;
                        winningCard = card;
                        winnerIndex = playersInTrick[i];
                    }
                }
            }

            // Calculate points in this trick
            int points = 0;
            foreach (var card in currentTrick)
            {
                points += CardGameUtility.GetHeartsPointValue(card);
            }

            // Award points to winner (in Hearts, points are bad!)
            seats[winnerIndex].TricksWon++;
            if (points > 0)
            {
                seats[winnerIndex].Score += points;

                manager.NotifyGameEventClientRpc(new GameEvent(
                    $"{seats[winnerIndex].PlayerName} won trick with {winningCard} (+{points} points)",
                    winnerIndex,
                    winningCard,
                    points));
            }
            else
            {
                manager.NotifyGameEventClientRpc(new GameEvent(
                    $"{seats[winnerIndex].PlayerName} won trick with {winningCard}",
                    winnerIndex,
                    winningCard));
            }

            Debug.Log($"[HeartsRules] {seats[winnerIndex].PlayerName} won trick. Points: {points}");

            // Clear trick
            currentTrick.Clear();
            playersInTrick.Clear();
        }

        private int GetCardRankValue(Card card)
        {
            // Ace is high (14)
            return card.Rank == Rank.Ace ? 14 : (int)card.Rank;
        }

        public bool IsRoundOver(List<PlayerSeat> seats)
        {
            // Round ends when all players have no cards
            foreach (var seat in seats)
            {
                if (seat.IsActive && seat.Hand.Count > 0)
                    return false;
            }
            return true;
        }

        public bool IsGameOver(List<PlayerSeat> seats)
        {
            // Game ends when any player reaches 100 points (or after 1 round in simplified version)
            foreach (var seat in seats)
            {
                if (seat.IsActive && seat.Score >= 100)
                    return true;
            }

            // For simplified version, game ends after one round
            return IsRoundOver(seats);
        }

        public void CalculateScores(List<PlayerSeat> seats)
        {
            // Scores are already tracked during play
            foreach (var seat in seats)
            {
                if (seat.IsActive)
                {
                    Debug.Log($"[HeartsRules] {seat.PlayerName}: {seat.Score} points, {seat.TricksWon} tricks");
                }
            }
        }
    }

    #endregion
}
