using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// ScriptableObject representing a complete custom game definition.
    /// Contains all metadata, cards, decks, and rule graph for a moddable game.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomGame", menuName = "CardGameBuilder/Custom Game Definition")]
    public class CustomGameDefinition : ScriptableObject
    {
        [Header("Metadata")]
        public string gameName = "New Custom Game";
        public string author = "Unknown";
        [TextArea(3, 6)]
        public string description = "A custom card game";
        public string version = "1.0.0";

        [Header("Game Configuration")]
        public int playerCount = 2;
        public int startingHandSize = 5;
        public int winConditionScore = 10;
        public bool allowBots = true;

        [Header("Cards & Decks")]
        public List<CardDef> cards = new List<CardDef>();
        public List<DeckDef> decks = new List<DeckDef>();

        [Header("Rules")]
        public RuleGraph rules = new RuleGraph();

        /// <summary>
        /// Validates the custom game definition for completeness and correctness.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(gameName))
            {
                errorMessage = "Game name cannot be empty";
                return false;
            }

            if (playerCount < 1 || playerCount > 8)
            {
                errorMessage = "Player count must be between 1 and 8";
                return false;
            }

            if (cards.Count == 0)
            {
                errorMessage = "Game must have at least one card defined";
                return false;
            }

            if (decks.Count == 0)
            {
                errorMessage = "Game must have at least one deck defined";
                return false;
            }

            if (rules == null || rules.nodes.Count == 0)
            {
                errorMessage = "Game must have at least one rule node";
                return false;
            }

            // Validate card IDs are unique
            HashSet<string> cardIds = new HashSet<string>();
            foreach (var card in cards)
            {
                if (cardIds.Contains(card.id))
                {
                    errorMessage = $"Duplicate card ID: {card.id}";
                    return false;
                }
                cardIds.Add(card.id);
            }

            // Validate deck references
            foreach (var deck in decks)
            {
                foreach (var cardId in deck.cardIds)
                {
                    if (!cardIds.Contains(cardId))
                    {
                        errorMessage = $"Deck '{deck.name}' references unknown card ID: {cardId}";
                        return false;
                    }
                }
            }

            // Validate rule graph has at least one event node
            bool hasEventNode = false;
            foreach (var node in rules.nodes)
            {
                if (node.nodeType == RuleNodeType.Event)
                {
                    hasEventNode = true;
                    break;
                }
            }

            if (!hasEventNode)
            {
                errorMessage = "Rule graph must have at least one Event node";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// Defines a single card with metadata and optional sprite.
    /// </summary>
    [Serializable]
    public class CardDef
    {
        public string id = Guid.NewGuid().ToString();
        public string name = "Card";
        public string suit = "None";
        public int value = 1;
        public string spritePath = "";  // Path to sprite asset or file

        [NonSerialized]
        public Sprite sprite;  // Runtime sprite reference (not serialized)

        [TextArea(2, 4)]
        public string description = "";

        public Dictionary<string, object> customProperties = new Dictionary<string, object>();

        public CardDef Clone()
        {
            return new CardDef
            {
                id = this.id,
                name = this.name,
                suit = this.suit,
                value = this.value,
                spritePath = this.spritePath,
                sprite = this.sprite,
                description = this.description,
                customProperties = new Dictionary<string, object>(this.customProperties)
            };
        }
    }

    /// <summary>
    /// Defines a deck as a collection of card IDs with quantities.
    /// </summary>
    [Serializable]
    public class DeckDef
    {
        public string name = "Main Deck";
        public List<string> cardIds = new List<string>();  // Can have duplicates for multiple copies
        public bool shuffleOnStart = true;
        public bool refillWhenEmpty = false;

        public int GetCardCount()
        {
            return cardIds.Count;
        }
    }

    /// <summary>
    /// Complete rule graph containing all nodes and connections.
    /// </summary>
    [Serializable]
    public class RuleGraph
    {
        public List<RuleNode> nodes = new List<RuleNode>();
        public List<RuleLink> links = new List<RuleLink>();

        /// <summary>
        /// Validates the rule graph for infinite loops and disconnected nodes.
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            // Check for orphaned nodes (except Event nodes which are entry points)
            HashSet<string> linkedNodeIds = new HashSet<string>();
            foreach (var link in links)
            {
                linkedNodeIds.Add(link.toNodeId);
            }

            foreach (var node in nodes)
            {
                if (node.nodeType != RuleNodeType.Event && !linkedNodeIds.Contains(node.id))
                {
                    // Warning: node is not connected from any other node
                    Debug.LogWarning($"Node '{node.name}' ({node.id}) is not connected from any other node");
                }
            }

            // Check for circular references (basic check)
            foreach (var link in links)
            {
                if (link.fromNodeId == link.toNodeId)
                {
                    errorMessage = $"Self-referencing link detected on node {link.fromNodeId}";
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }

        public RuleNode GetNodeById(string nodeId)
        {
            return nodes.Find(n => n.id == nodeId);
        }

        public List<RuleNode> GetEventNodes()
        {
            return nodes.FindAll(n => n.nodeType == RuleNodeType.Event);
        }
    }

    /// <summary>
    /// Types of rule nodes available in the graph editor.
    /// </summary>
    public enum RuleNodeType
    {
        Event,      // Entry points: OnTurnStart, OnPlayCard, OnMatchEnd
        Condition,  // Branching logic: CompareValue, CheckHandEmpty, etc.
        Action      // Effects: DrawCard, PlayCard, AddScore, EndTurn, EndGame
    }

    /// <summary>
    /// Specific event types that can trigger rule execution.
    /// </summary>
    public enum EventNodeSubType
    {
        OnGameStart,
        OnRoundStart,
        OnTurnStart,
        OnTurnEnd,
        OnCardPlayed,
        OnCardDrawn,
        OnScoreChanged,
        OnRoundEnd,
        OnGameEnd
    }

    /// <summary>
    /// Condition types for branching logic.
    /// </summary>
    public enum ConditionNodeSubType
    {
        CompareCardValue,
        CheckHandEmpty,
        CheckHandCount,
        CheckDeckEmpty,
        CheckScore,
        CheckPlayerCount,
        CompareCards,
        CheckSuit,
        CheckRank,
        Custom
    }

    /// <summary>
    /// Action types that modify game state.
    /// </summary>
    public enum ActionNodeSubType
    {
        DrawCard,
        PlayCard,
        DiscardCard,
        AddScore,
        SubtractScore,
        SetScore,
        TransferCard,
        ShuffleDeck,
        NextTurn,
        EndRound,
        EndGame,
        ShowMessage,
        Custom
    }

    /// <summary>
    /// A single node in the rule graph.
    /// </summary>
    [Serializable]
    public class RuleNode
    {
        public string id = Guid.NewGuid().ToString();
        public string name = "New Node";
        public RuleNodeType nodeType = RuleNodeType.Action;

        // Sub-type as string to allow custom types
        public string subType = "";

        // Parameters stored as key-value pairs
        public List<RuleParameter> parameters = new List<RuleParameter>();

        // Visual position for graph editor
        public Vector2 position = Vector2.zero;

        // Output ports for connections
        public List<string> outputPortIds = new List<string>();

        public RuleNode()
        {
            // Default output port
            outputPortIds.Add("out");
        }

        public void SetParameter(string key, object value)
        {
            var param = parameters.Find(p => p.key == key);
            if (param != null)
            {
                param.value = value?.ToString() ?? "";
            }
            else
            {
                parameters.Add(new RuleParameter { key = key, value = value?.ToString() ?? "" });
            }
        }

        public string GetParameter(string key, string defaultValue = "")
        {
            var param = parameters.Find(p => p.key == key);
            return param?.value ?? defaultValue;
        }

        public int GetParameterInt(string key, int defaultValue = 0)
        {
            var param = parameters.Find(p => p.key == key);
            if (param != null && int.TryParse(param.value, out int result))
            {
                return result;
            }
            return defaultValue;
        }

        public float GetParameterFloat(string key, float defaultValue = 0f)
        {
            var param = parameters.Find(p => p.key == key);
            if (param != null && float.TryParse(param.value, out float result))
            {
                return result;
            }
            return defaultValue;
        }

        public bool GetParameterBool(string key, bool defaultValue = false)
        {
            var param = parameters.Find(p => p.key == key);
            if (param != null && bool.TryParse(param.value, out bool result))
            {
                return result;
            }
            return defaultValue;
        }
    }

    /// <summary>
    /// A parameter for a rule node.
    /// </summary>
    [Serializable]
    public class RuleParameter
    {
        public string key;
        public string value;  // Stored as string, parsed at runtime
    }

    /// <summary>
    /// A connection between two nodes in the rule graph.
    /// </summary>
    [Serializable]
    public class RuleLink
    {
        public string id = Guid.NewGuid().ToString();
        public string fromNodeId;
        public string fromPortId = "out";  // Output port ID
        public string toNodeId;
        public string toPortId = "in";     // Input port ID (usually just "in")

        // For condition nodes, specify which branch (true/false)
        public bool isConditionBranch = false;
        public bool conditionValue = true;  // true = "true branch", false = "false branch"
    }
}
