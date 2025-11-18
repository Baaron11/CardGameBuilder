using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// Handles exporting custom games as .zip packages and importing them.
    /// Manages mod storage in Application.persistentDataPath/Mods.
    /// </summary>
    public class ExportImportManager : MonoBehaviour
    {
        private const string MODS_FOLDER = "Mods";
        private const string DEFINITION_FILENAME = "definition.json";
        private const string SPRITES_FOLDER = "Sprites";

        private string ModsPath => Path.Combine(Application.persistentDataPath, MODS_FOLDER);

        private void Awake()
        {
            // Ensure mods directory exists
            if (!Directory.Exists(ModsPath))
            {
                Directory.CreateDirectory(ModsPath);
                Debug.Log($"Created mods directory: {ModsPath}");
            }
        }

        /// <summary>
        /// Exports a custom game definition to a .zip file.
        /// </summary>
        public bool Export(CustomGameDefinition definition, out string exportPath)
        {
            exportPath = null;

            if (definition == null)
            {
                Debug.LogError("Cannot export null definition");
                return false;
            }

            // Validate definition
            string validationError;
            if (!definition.Validate(out validationError))
            {
                Debug.LogError($"Cannot export invalid definition: {validationError}");
                return false;
            }

            try
            {
                // Create temporary directory for export
                string tempDir = Path.Combine(Application.temporaryCachePath, $"Export_{definition.gameName}_{DateTime.Now.Ticks}");
                Directory.CreateDirectory(tempDir);

                // Serialize definition to JSON
                CustomGameData data = ConvertToSerializableData(definition);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(Path.Combine(tempDir, DEFINITION_FILENAME), json);

                // Export sprites
                string spritesDir = Path.Combine(tempDir, SPRITES_FOLDER);
                Directory.CreateDirectory(spritesDir);
                ExportSprites(definition, spritesDir);

                // Create zip file
                string zipFileName = $"{SanitizeFileName(definition.gameName)}_v{definition.version}.zip";
                exportPath = Path.Combine(ModsPath, zipFileName);

                // Delete existing zip if it exists
                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                ZipFile.CreateFromDirectory(tempDir, exportPath);

                // Clean up temp directory
                Directory.Delete(tempDir, true);

                Debug.Log($"Exported mod to: {exportPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Export failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Imports a custom game from a .zip file.
        /// </summary>
        public bool Import(string zipPath, out CustomGameDefinition definition)
        {
            definition = null;

            if (!File.Exists(zipPath))
            {
                Debug.LogError($"Import file not found: {zipPath}");
                return false;
            }

            try
            {
                // Create temporary extraction directory
                string tempDir = Path.Combine(Application.temporaryCachePath, $"Import_{DateTime.Now.Ticks}");
                Directory.CreateDirectory(tempDir);

                // Extract zip
                ZipFile.ExtractToDirectory(zipPath, tempDir);

                // Load definition JSON
                string jsonPath = Path.Combine(tempDir, DEFINITION_FILENAME);
                if (!File.Exists(jsonPath))
                {
                    Debug.LogError("Import failed: definition.json not found in zip");
                    Directory.Delete(tempDir, true);
                    return false;
                }

                string json = File.ReadAllText(jsonPath);
                CustomGameData data = JsonUtility.FromJson<CustomGameData>(json);

                if (data == null)
                {
                    Debug.LogError("Import failed: Could not parse definition.json");
                    Directory.Delete(tempDir, true);
                    return false;
                }

                // Convert to CustomGameDefinition
                definition = ConvertFromSerializableData(data);

                // Load sprites
                string spritesDir = Path.Combine(tempDir, SPRITES_FOLDER);
                ImportSprites(definition, spritesDir);

                // Validate imported definition
                string validationError;
                if (!definition.Validate(out validationError))
                {
                    Debug.LogError($"Import failed: Invalid definition - {validationError}");
                    Directory.Delete(tempDir, true);
                    return false;
                }

                // Install mod to persistent location
                string modDir = Path.Combine(ModsPath, SanitizeFileName(definition.gameName));
                if (Directory.Exists(modDir))
                {
                    // Update existing mod
                    Directory.Delete(modDir, true);
                }

                Directory.CreateDirectory(modDir);

                // Copy extracted files to mod directory
                CopyDirectory(tempDir, modDir);

                // Clean up temp directory
                Directory.Delete(tempDir, true);

                Debug.Log($"Imported mod: {definition.gameName} v{definition.version}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Import failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Gets a list of all installed mod definitions.
        /// </summary>
        public List<CustomGameDefinition> GetInstalledMods()
        {
            List<CustomGameDefinition> mods = new List<CustomGameDefinition>();

            if (!Directory.Exists(ModsPath))
                return mods;

            try
            {
                string[] modDirs = Directory.GetDirectories(ModsPath);

                foreach (string modDir in modDirs)
                {
                    string jsonPath = Path.Combine(modDir, DEFINITION_FILENAME);
                    if (File.Exists(jsonPath))
                    {
                        string json = File.ReadAllText(jsonPath);
                        CustomGameData data = JsonUtility.FromJson<CustomGameData>(json);

                        if (data != null)
                        {
                            CustomGameDefinition definition = ConvertFromSerializableData(data);

                            // Load sprites
                            string spritesDir = Path.Combine(modDir, SPRITES_FOLDER);
                            ImportSprites(definition, spritesDir);

                            mods.Add(definition);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading installed mods: {ex.Message}");
            }

            return mods;
        }

        /// <summary>
        /// Deletes an installed mod.
        /// </summary>
        public bool UninstallMod(string gameName)
        {
            try
            {
                string modDir = Path.Combine(ModsPath, SanitizeFileName(gameName));
                if (Directory.Exists(modDir))
                {
                    Directory.Delete(modDir, true);
                    Debug.Log($"Uninstalled mod: {gameName}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Mod not found: {gameName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Uninstall failed: {ex.Message}");
                return false;
            }
        }

        #region Helper Methods

        private CustomGameData ConvertToSerializableData(CustomGameDefinition definition)
        {
            CustomGameData data = new CustomGameData
            {
                gameName = definition.gameName,
                author = definition.author,
                description = definition.description,
                version = definition.version,
                playerCount = definition.playerCount,
                startingHandSize = definition.startingHandSize,
                winConditionScore = definition.winConditionScore,
                allowBots = definition.allowBots,
                cards = new List<SerializableCardDef>(),
                decks = new List<SerializableDeckDef>(),
                rules = SerializeRuleGraph(definition.rules)
            };

            foreach (var card in definition.cards)
            {
                data.cards.Add(new SerializableCardDef
                {
                    id = card.id,
                    name = card.name,
                    suit = card.suit,
                    value = card.value,
                    spritePath = card.spritePath,
                    description = card.description
                });
            }

            foreach (var deck in definition.decks)
            {
                data.decks.Add(new SerializableDeckDef
                {
                    name = deck.name,
                    cardIds = new List<string>(deck.cardIds),
                    shuffleOnStart = deck.shuffleOnStart,
                    refillWhenEmpty = deck.refillWhenEmpty
                });
            }

            return data;
        }

        private CustomGameDefinition ConvertFromSerializableData(CustomGameData data)
        {
            CustomGameDefinition definition = ScriptableObject.CreateInstance<CustomGameDefinition>();

            definition.gameName = data.gameName;
            definition.author = data.author;
            definition.description = data.description;
            definition.version = data.version;
            definition.playerCount = data.playerCount;
            definition.startingHandSize = data.startingHandSize;
            definition.winConditionScore = data.winConditionScore;
            definition.allowBots = data.allowBots;

            definition.cards = new List<CardDef>();
            foreach (var cardData in data.cards)
            {
                definition.cards.Add(new CardDef
                {
                    id = cardData.id,
                    name = cardData.name,
                    suit = cardData.suit,
                    value = cardData.value,
                    spritePath = cardData.spritePath,
                    description = cardData.description
                });
            }

            definition.decks = new List<DeckDef>();
            foreach (var deckData in data.decks)
            {
                definition.decks.Add(new DeckDef
                {
                    name = deckData.name,
                    cardIds = new List<string>(deckData.cardIds),
                    shuffleOnStart = deckData.shuffleOnStart,
                    refillWhenEmpty = deckData.refillWhenEmpty
                });
            }

            definition.rules = DeserializeRuleGraph(data.rules);

            return definition;
        }

        private SerializableRuleGraph SerializeRuleGraph(RuleGraph graph)
        {
            if (graph == null)
                return new SerializableRuleGraph();

            SerializableRuleGraph data = new SerializableRuleGraph
            {
                nodes = new List<SerializableRuleNode>(),
                links = new List<SerializableRuleLink>()
            };

            foreach (var node in graph.nodes)
            {
                data.nodes.Add(new SerializableRuleNode
                {
                    id = node.id,
                    name = node.name,
                    nodeType = (int)node.nodeType,
                    subType = node.subType,
                    parameters = new List<RuleParameter>(node.parameters),
                    positionX = node.position.x,
                    positionY = node.position.y,
                    outputPortIds = new List<string>(node.outputPortIds)
                });
            }

            foreach (var link in graph.links)
            {
                data.links.Add(new SerializableRuleLink
                {
                    id = link.id,
                    fromNodeId = link.fromNodeId,
                    fromPortId = link.fromPortId,
                    toNodeId = link.toNodeId,
                    toPortId = link.toPortId,
                    isConditionBranch = link.isConditionBranch,
                    conditionValue = link.conditionValue
                });
            }

            return data;
        }

        private RuleGraph DeserializeRuleGraph(SerializableRuleGraph data)
        {
            if (data == null)
                return new RuleGraph();

            RuleGraph graph = new RuleGraph
            {
                nodes = new List<RuleNode>(),
                links = new List<RuleLink>()
            };

            foreach (var nodeData in data.nodes)
            {
                graph.nodes.Add(new RuleNode
                {
                    id = nodeData.id,
                    name = nodeData.name,
                    nodeType = (RuleNodeType)nodeData.nodeType,
                    subType = nodeData.subType,
                    parameters = new List<RuleParameter>(nodeData.parameters),
                    position = new Vector2(nodeData.positionX, nodeData.positionY),
                    outputPortIds = new List<string>(nodeData.outputPortIds)
                });
            }

            foreach (var linkData in data.links)
            {
                graph.links.Add(new RuleLink
                {
                    id = linkData.id,
                    fromNodeId = linkData.fromNodeId,
                    fromPortId = linkData.fromPortId,
                    toNodeId = linkData.toNodeId,
                    toPortId = linkData.toPortId,
                    isConditionBranch = linkData.isConditionBranch,
                    conditionValue = linkData.conditionValue
                });
            }

            return graph;
        }

        private void ExportSprites(CustomGameDefinition definition, string spritesDir)
        {
            // In a production implementation, save sprites as PNG files
            // For now, just save sprite paths
            for (int i = 0; i < definition.cards.Count; i++)
            {
                var card = definition.cards[i];
                if (card.sprite != null)
                {
                    // Save sprite as PNG
                    string spritePath = Path.Combine(spritesDir, $"{card.id}.png");
                    SaveSpriteAsPNG(card.sprite, spritePath);
                    card.spritePath = $"{SPRITES_FOLDER}/{card.id}.png";
                }
            }
        }

        private void ImportSprites(CustomGameDefinition definition, string spritesDir)
        {
            if (!Directory.Exists(spritesDir))
                return;

            foreach (var card in definition.cards)
            {
                if (!string.IsNullOrEmpty(card.spritePath))
                {
                    string fullPath = Path.Combine(spritesDir, Path.GetFileName(card.spritePath));
                    if (File.Exists(fullPath))
                    {
                        card.sprite = LoadSpriteFromPNG(fullPath);
                    }
                }
            }
        }

        private void SaveSpriteAsPNG(Sprite sprite, string path)
        {
            try
            {
                Texture2D texture = sprite.texture;
                byte[] pngData = texture.EncodeToPNG();
                File.WriteAllBytes(path, pngData);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not save sprite to {path}: {ex.Message}");
            }
        }

        private Sprite LoadSpriteFromPNG(string path)
        {
            try
            {
                byte[] fileData = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not load sprite from {path}: {ex.Message}");
                return null;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            char[] invalids = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalids, StringSplitOptions.RemoveEmptyEntries));
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        #endregion
    }

    #region Serializable Data Structures

    [Serializable]
    public class CustomGameData
    {
        public string gameName;
        public string author;
        public string description;
        public string version;
        public int playerCount;
        public int startingHandSize;
        public int winConditionScore;
        public bool allowBots;
        public List<SerializableCardDef> cards;
        public List<SerializableDeckDef> decks;
        public SerializableRuleGraph rules;
    }

    [Serializable]
    public class SerializableCardDef
    {
        public string id;
        public string name;
        public string suit;
        public int value;
        public string spritePath;
        public string description;
    }

    [Serializable]
    public class SerializableDeckDef
    {
        public string name;
        public List<string> cardIds;
        public bool shuffleOnStart;
        public bool refillWhenEmpty;
    }

    [Serializable]
    public class SerializableRuleGraph
    {
        public List<SerializableRuleNode> nodes = new List<SerializableRuleNode>();
        public List<SerializableRuleLink> links = new List<SerializableRuleLink>();
    }

    [Serializable]
    public class SerializableRuleNode
    {
        public string id;
        public string name;
        public int nodeType;
        public string subType;
        public List<RuleParameter> parameters;
        public float positionX;
        public float positionY;
        public List<string> outputPortIds;
    }

    [Serializable]
    public class SerializableRuleLink
    {
        public string id;
        public string fromNodeId;
        public string fromPortId;
        public string toNodeId;
        public string toPortId;
        public bool isConditionBranch;
        public bool conditionValue;
    }

    #endregion
}
