using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CardGameBuilder.Core;
using CardGameBuilder.Games;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// Runtime interpreter for custom game rules.
    /// Implements IGameRules by executing the visual rule graph.
    /// </summary>
    public class CustomGameRules : IGameRules
    {
        private CustomGameDefinition definition;
        private CardGameManager gameManager;
        private Dictionary<string, List<CustomCard>> customDecks;
        private Dictionary<string, CustomCard> cardLookup;

        // Runtime state
        private int currentPlayerIndex = 0;
        private bool gameEnded = false;
        private bool roundEnded = false;

        public CustomGameRules(CustomGameDefinition definition, CardGameManager manager)
        {
            this.definition = definition;
            this.gameManager = manager;
            this.customDecks = new Dictionary<string, List<CustomCard>>();
            this.cardLookup = new Dictionary<string, CustomCard>();

            InitializeCustomDecks();
        }

        private void InitializeCustomDecks()
        {
            // Create custom card instances
            foreach (var cardDef in definition.cards)
            {
                CustomCard card = new CustomCard(cardDef);
                cardLookup[cardDef.id] = card;
            }

            // Build decks from definitions
            foreach (var deckDef in definition.decks)
            {
                List<CustomCard> deck = new List<CustomCard>();
                foreach (var cardId in deckDef.cardIds)
                {
                    if (cardLookup.ContainsKey(cardId))
                    {
                        deck.Add(cardLookup[cardId]);
                    }
                }
                customDecks[deckDef.name] = deck;
            }
        }

        #region IGameRules Implementation

        public void DealInitialCards(Deck deck, List<PlayerSeat> seats)
        {
            // Execute OnGameStart event nodes
            ExecuteEventNodes(EventNodeSubType.OnGameStart, seats, deck);

            // Default: deal starting hand size to each player
            var mainDeck = GetMainDeck();
            if (mainDeck != null && mainDeck.Count >= definition.startingHandSize * seats.Count)
            {
                // Shuffle deck if configured
                var mainDeckDef = definition.decks.FirstOrDefault();
                if (mainDeckDef != null && mainDeckDef.shuffleOnStart)
                {
                    ShuffleDeck(mainDeck);
                }

                for (int i = 0; i < seats.Count; i++)
                {
                    seats[i].Hand.Clear();
                    for (int j = 0; j < definition.startingHandSize; j++)
                    {
                        if (mainDeck.Count > 0)
                        {
                            var customCard = mainDeck[0];
                            mainDeck.RemoveAt(0);

                            // Convert to standard Card for compatibility
                            Card standardCard = ConvertToStandardCard(customCard);
                            seats[i].Hand.Add(standardCard);
                        }
                    }
                }

                Debug.Log($"Dealt {definition.startingHandSize} cards to {seats.Count} players");
            }
        }

        public int GetFirstPlayer(List<PlayerSeat> seats)
        {
            // Default: first active player
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsActive)
                {
                    currentPlayerIndex = i;
                    return i;
                }
            }
            return 0;
        }

        public bool ProcessAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck, CardGameManager manager)
        {
            this.gameManager = manager;
            currentPlayerIndex = seatIndex;

            // Execute OnTurnStart nodes
            ExecuteEventNodes(EventNodeSubType.OnTurnStart, seats, deck);

            // Process the action
            bool actionProcessed = false;

            switch (action.Type)
            {
                case ActionType.Draw:
                    actionProcessed = ProcessDrawAction(seatIndex, seats, deck);
                    break;

                case ActionType.Play:
                    actionProcessed = ProcessPlayAction(action, seatIndex, seats, deck);
                    break;

                case ActionType.Discard:
                    actionProcessed = ProcessDiscardAction(action, seatIndex, seats, deck);
                    break;

                default:
                    Debug.LogWarning($"Unhandled action type: {action.Type}");
                    break;
            }

            if (actionProcessed)
            {
                // Execute OnCardPlayed or OnCardDrawn nodes
                if (action.Type == ActionType.Play)
                {
                    ExecuteEventNodes(EventNodeSubType.OnCardPlayed, seats, deck);
                }
                else if (action.Type == ActionType.Draw)
                {
                    ExecuteEventNodes(EventNodeSubType.OnCardDrawn, seats, deck);
                }

                // Execute OnTurnEnd nodes
                ExecuteEventNodes(EventNodeSubType.OnTurnEnd, seats, deck);
            }

            return actionProcessed;
        }

        public bool IsRoundOver(List<PlayerSeat> seats)
        {
            return roundEnded;
        }

        public bool IsGameOver(List<PlayerSeat> seats)
        {
            if (gameEnded)
                return true;

            // Check win condition
            foreach (var seat in seats)
            {
                if (seat.IsActive && seat.Score >= definition.winConditionScore)
                {
                    return true;
                }
            }

            return false;
        }

        public void CalculateScores(List<PlayerSeat> seats)
        {
            // Execute OnRoundEnd nodes
            ExecuteEventNodes(EventNodeSubType.OnRoundEnd, seats, null);

            // Scores are updated via rule nodes
        }

        #endregion

        #region Action Processing

        private bool ProcessDrawAction(int seatIndex, List<PlayerSeat> seats, Deck deck)
        {
            var mainDeck = GetMainDeck();
            if (mainDeck != null && mainDeck.Count > 0)
            {
                var customCard = mainDeck[0];
                mainDeck.RemoveAt(0);

                Card standardCard = ConvertToStandardCard(customCard);
                seats[seatIndex].Hand.Add(standardCard);

                gameManager.NotifyGameEvent($"{seats[seatIndex].PlayerName} drew a card");
                return true;
            }
            else
            {
                gameManager.NotifyGameEvent($"Deck is empty!");
                return false;
            }
        }

        private bool ProcessPlayAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck)
        {
            var seat = seats[seatIndex];

            if (seat.Hand.Contains(action.Card))
            {
                seat.Hand.Remove(action.Card);
                gameManager.NotifyGameEvent($"{seat.PlayerName} played {action.Card.ToShortString()}");

                // Update score based on card value (default behavior)
                // This can be overridden by rule nodes
                return true;
            }

            return false;
        }

        private bool ProcessDiscardAction(PlayerAction action, int seatIndex, List<PlayerSeat> seats, Deck deck)
        {
            var seat = seats[seatIndex];

            if (seat.Hand.Contains(action.Card))
            {
                seat.Hand.Remove(action.Card);
                gameManager.NotifyGameEvent($"{seat.PlayerName} discarded {action.Card.ToShortString()}");
                return true;
            }

            return false;
        }

        #endregion

        #region Rule Graph Execution

        private void ExecuteEventNodes(EventNodeSubType eventType, List<PlayerSeat> seats, Deck deck)
        {
            if (definition.rules == null)
                return;

            // Find all event nodes of this type
            var eventNodes = definition.rules.nodes.FindAll(n =>
                n.nodeType == RuleNodeType.Event &&
                n.subType == eventType.ToString()
            );

            foreach (var eventNode in eventNodes)
            {
                ExecuteNode(eventNode, seats, deck);
            }
        }

        private void ExecuteNode(RuleNode node, List<PlayerSeat> seats, Deck deck)
        {
            if (node == null)
                return;

            switch (node.nodeType)
            {
                case RuleNodeType.Event:
                    // Events just trigger their connected nodes
                    ExecuteConnectedNodes(node, seats, deck);
                    break;

                case RuleNodeType.Condition:
                    ExecuteConditionNode(node, seats, deck);
                    break;

                case RuleNodeType.Action:
                    ExecuteActionNode(node, seats, deck);
                    break;
            }
        }

        private void ExecuteConditionNode(RuleNode node, List<PlayerSeat> seats, Deck deck)
        {
            bool conditionResult = EvaluateCondition(node, seats, deck);

            // Find links for true/false branches
            var links = definition.rules.links.FindAll(l =>
                l.fromNodeId == node.id &&
                l.isConditionBranch &&
                l.conditionValue == conditionResult
            );

            foreach (var link in links)
            {
                var nextNode = definition.rules.GetNodeById(link.toNodeId);
                if (nextNode != null)
                {
                    ExecuteNode(nextNode, seats, deck);
                }
            }
        }

        private bool EvaluateCondition(RuleNode node, List<PlayerSeat> seats, Deck deck)
        {
            switch (node.subType)
            {
                case "CheckHandEmpty":
                    int playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
                    return seats[playerIndex].Hand.Count == 0;

                case "CheckHandCount":
                    playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
                    int count = node.GetParameterInt("count", 0);
                    string op = node.GetParameter("operator", ">");
                    return CompareValues(seats[playerIndex].Hand.Count, count, op);

                case "CheckScore":
                    playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
                    int scoreValue = node.GetParameterInt("value", 0);
                    op = node.GetParameter("operator", ">=");
                    return CompareValues(seats[playerIndex].Score, scoreValue, op);

                case "CheckDeckEmpty":
                    return GetMainDeck()?.Count == 0;

                default:
                    Debug.LogWarning($"Unhandled condition type: {node.subType}");
                    return false;
            }
        }

        private void ExecuteActionNode(RuleNode node, List<PlayerSeat> seats, Deck deck)
        {
            switch (node.subType)
            {
                case "DrawCard":
                    ExecuteDrawCardAction(node, seats, deck);
                    break;

                case "AddScore":
                    ExecuteAddScoreAction(node, seats);
                    break;

                case "SubtractScore":
                    ExecuteSubtractScoreAction(node, seats);
                    break;

                case "SetScore":
                    ExecuteSetScoreAction(node, seats);
                    break;

                case "ShowMessage":
                    string message = node.GetParameter("message", "");
                    gameManager.NotifyGameEvent(message);
                    break;

                case "NextTurn":
                    // Handled by CardGameManager
                    break;

                case "EndRound":
                    roundEnded = true;
                    break;

                case "EndGame":
                    gameEnded = true;
                    break;

                default:
                    Debug.LogWarning($"Unhandled action type: {node.subType}");
                    break;
            }

            // Execute connected nodes
            ExecuteConnectedNodes(node, seats, deck);
        }

        private void ExecuteDrawCardAction(RuleNode node, List<PlayerSeat> seats, Deck deck)
        {
            int playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
            int count = node.GetParameterInt("count", 1);

            var mainDeck = GetMainDeck();
            if (mainDeck == null)
                return;

            for (int i = 0; i < count && mainDeck.Count > 0; i++)
            {
                var customCard = mainDeck[0];
                mainDeck.RemoveAt(0);

                Card standardCard = ConvertToStandardCard(customCard);
                seats[playerIndex].Hand.Add(standardCard);
            }

            gameManager.NotifyGameEvent($"{seats[playerIndex].PlayerName} drew {count} card(s)");
        }

        private void ExecuteAddScoreAction(RuleNode node, List<PlayerSeat> seats)
        {
            int playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
            int points = node.GetParameterInt("points", 1);

            seats[playerIndex].Score += points;
            gameManager.NotifyGameEvent($"{seats[playerIndex].PlayerName} gained {points} point(s)");
        }

        private void ExecuteSubtractScoreAction(RuleNode node, List<PlayerSeat> seats)
        {
            int playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
            int points = node.GetParameterInt("points", 1);

            seats[playerIndex].Score -= points;
            gameManager.NotifyGameEvent($"{seats[playerIndex].PlayerName} lost {points} point(s)");
        }

        private void ExecuteSetScoreAction(RuleNode node, List<PlayerSeat> seats)
        {
            int playerIndex = ParsePlayerIndex(node.GetParameter("playerIndex", "current"), seats);
            int score = node.GetParameterInt("score", 0);

            seats[playerIndex].Score = score;
        }

        private void ExecuteConnectedNodes(RuleNode node, List<PlayerSeat> seats, Deck deck)
        {
            var links = definition.rules.links.FindAll(l =>
                l.fromNodeId == node.id &&
                !l.isConditionBranch
            );

            foreach (var link in links)
            {
                var nextNode = definition.rules.GetNodeById(link.toNodeId);
                if (nextNode != null)
                {
                    ExecuteNode(nextNode, seats, deck);
                }
            }
        }

        #endregion

        #region Helper Methods

        private List<CustomCard> GetMainDeck()
        {
            if (customDecks.Count > 0)
            {
                return customDecks.Values.First();
            }
            return null;
        }

        private void ShuffleDeck(List<CustomCard> deck)
        {
            System.Random rng = new System.Random();
            int n = deck.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                CustomCard value = deck[k];
                deck[k] = deck[n];
                deck[n] = value;
            }
        }

        private int ParsePlayerIndex(string indexStr, List<PlayerSeat> seats)
        {
            if (indexStr == "current")
            {
                return currentPlayerIndex;
            }
            else if (int.TryParse(indexStr, out int index))
            {
                return Mathf.Clamp(index, 0, seats.Count - 1);
            }
            return 0;
        }

        private bool CompareValues(int a, int b, string op)
        {
            switch (op)
            {
                case ">": return a > b;
                case ">=": return a >= b;
                case "<": return a < b;
                case "<=": return a <= b;
                case "==": return a == b;
                case "!=": return a != b;
                default: return false;
            }
        }

        private Card ConvertToStandardCard(CustomCard customCard)
        {
            // Map custom card to standard card structure
            // Use value and suit to create a compatible Card
            Suit suit = ParseSuit(customCard.Definition.suit);
            Rank rank = (Rank)Mathf.Clamp(customCard.Definition.value, 1, 13);

            return new Card { Suit = suit, Rank = rank };
        }

        private Suit ParseSuit(string suitStr)
        {
            switch (suitStr.ToLower())
            {
                case "hearts": return Suit.Hearts;
                case "diamonds": return Suit.Diamonds;
                case "clubs": return Suit.Clubs;
                case "spades": return Suit.Spades;
                default: return Suit.Hearts;
            }
        }

        #endregion
    }

    /// <summary>
    /// Runtime representation of a custom card.
    /// </summary>
    public class CustomCard
    {
        public CardDef Definition { get; private set; }

        public CustomCard(CardDef definition)
        {
            Definition = definition;
        }

        public override string ToString()
        {
            return $"{Definition.name} ({Definition.suit})";
        }
    }
}
