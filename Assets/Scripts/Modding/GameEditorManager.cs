using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// Main manager for the Game Editor scene.
    /// Coordinates DeckEditor, RuleGraphEditor, and ExportImportManager.
    /// Provides unified interface for creating and editing custom games.
    /// </summary>
    public class GameEditorManager : MonoBehaviour
    {
        [Header("Current Game Definition")]
        [SerializeField] private CustomGameDefinition currentDefinition;

        [Header("Editor Components")]
        [SerializeField] private DeckEditor deckEditor;
        [SerializeField] private RuleGraphEditor ruleGraphEditor;
        [SerializeField] private ExportImportManager exportImportManager;

        [Header("UI Panels")]
        [SerializeField] private GameObject metadataPanel;
        [SerializeField] private GameObject deckEditorPanel;
        [SerializeField] private GameObject ruleEditorPanel;

        [Header("Metadata UI")]
        [SerializeField] private TMP_InputField gameNameInput;
        [SerializeField] private TMP_InputField authorInput;
        [SerializeField] private TMP_InputField descriptionInput;
        [SerializeField] private TMP_InputField versionInput;
        [SerializeField] private TMP_InputField playerCountInput;
        [SerializeField] private TMP_InputField startingHandSizeInput;
        [SerializeField] private TMP_InputField winConditionScoreInput;
        [SerializeField] private Toggle allowBotsToggle;

        [Header("Navigation Buttons")]
        [SerializeField] private Button metadataTabButton;
        [SerializeField] private Button deckEditorTabButton;
        [SerializeField] private Button ruleEditorTabButton;

        [Header("Action Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button saveGameButton;
        [SerializeField] private Button exportButton;
        [SerializeField] private Button testGameButton;
        [SerializeField] private Button createStandardDeckButton;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI statusText;

        private void Start()
        {
            SetupUI();
            CreateNewGame();
        }

        private void SetupUI()
        {
            // Navigation buttons
            if (metadataTabButton != null)
                metadataTabButton.onClick.AddListener(() => ShowPanel("metadata"));

            if (deckEditorTabButton != null)
                deckEditorTabButton.onClick.AddListener(() => ShowPanel("deck"));

            if (ruleEditorTabButton != null)
                ruleEditorTabButton.onClick.AddListener(() => ShowPanel("rules"));

            // Action buttons
            if (newGameButton != null)
                newGameButton.onClick.AddListener(CreateNewGame);

            if (saveGameButton != null)
                saveGameButton.onClick.AddListener(SaveCurrentGame);

            if (exportButton != null)
                exportButton.onClick.AddListener(ExportCurrentGame);

            if (testGameButton != null)
                testGameButton.onClick.AddListener(TestCurrentGame);

            if (createStandardDeckButton != null)
                createStandardDeckButton.onClick.AddListener(CreateStandardDeck);

            // Start with metadata panel
            ShowPanel("metadata");
        }

        #region Game Management

        public void CreateNewGame()
        {
            currentDefinition = ScriptableObject.CreateInstance<CustomGameDefinition>();
            currentDefinition.gameName = "New Custom Game";
            currentDefinition.author = "Unknown";
            currentDefinition.description = "A custom card game";
            currentDefinition.version = "1.0.0";
            currentDefinition.playerCount = 2;
            currentDefinition.startingHandSize = 5;
            currentDefinition.winConditionScore = 10;
            currentDefinition.allowBots = true;

            RefreshEditors();
            LoadMetadataToUI();
            UpdateStatus("Created new game definition");

            Debug.Log("Created new game definition");
        }

        public void SaveCurrentGame()
        {
            if (currentDefinition == null)
            {
                UpdateStatus("Error: No game definition loaded");
                return;
            }

            SaveMetadataFromUI();

            string validationError;
            if (currentDefinition.Validate(out validationError))
            {
                UpdateStatus($"Game '{currentDefinition.gameName}' saved successfully!");
                Debug.Log($"Saved game: {currentDefinition.gameName}");
            }
            else
            {
                UpdateStatus($"Validation failed: {validationError}");
                Debug.LogWarning($"Validation error: {validationError}");
            }
        }

        public void ExportCurrentGame()
        {
            if (currentDefinition == null)
            {
                UpdateStatus("Error: No game definition loaded");
                return;
            }

            SaveMetadataFromUI();

            if (exportImportManager == null)
            {
                UpdateStatus("Error: ExportImportManager not assigned");
                return;
            }

            string exportPath;
            if (exportImportManager.Export(currentDefinition, out exportPath))
            {
                UpdateStatus($"Exported to: {exportPath}");
                Debug.Log($"Exported to: {exportPath}");
            }
            else
            {
                UpdateStatus("Export failed!");
                Debug.LogError("Export failed");
            }
        }

        public void TestCurrentGame()
        {
            if (currentDefinition == null)
            {
                UpdateStatus("Error: No game definition loaded");
                return;
            }

            SaveMetadataFromUI();

            string validationError;
            if (!currentDefinition.Validate(out validationError))
            {
                UpdateStatus($"Cannot test - validation failed: {validationError}");
                return;
            }

            UpdateStatus("Test mode not yet implemented - export and play via Mod Browser");
            Debug.Log("Test game functionality - would launch a local test instance");

            // In a full implementation, this would:
            // 1. Save the current definition
            // 2. Load a test scene
            // 3. Start a local game with the custom definition
        }

        private void CreateStandardDeck()
        {
            if (deckEditor != null)
            {
                deckEditor.CreateStandardDeck();
                UpdateStatus("Created standard 52-card deck");
            }
        }

        #endregion

        #region UI Management

        private void ShowPanel(string panelName)
        {
            if (metadataPanel != null)
                metadataPanel.SetActive(panelName == "metadata");

            if (deckEditorPanel != null)
                deckEditorPanel.SetActive(panelName == "deck");

            if (ruleEditorPanel != null)
                ruleEditorPanel.SetActive(panelName == "rules");
        }

        private void LoadMetadataToUI()
        {
            if (currentDefinition == null)
                return;

            if (gameNameInput != null)
                gameNameInput.text = currentDefinition.gameName;

            if (authorInput != null)
                authorInput.text = currentDefinition.author;

            if (descriptionInput != null)
                descriptionInput.text = currentDefinition.description;

            if (versionInput != null)
                versionInput.text = currentDefinition.version;

            if (playerCountInput != null)
                playerCountInput.text = currentDefinition.playerCount.ToString();

            if (startingHandSizeInput != null)
                startingHandSizeInput.text = currentDefinition.startingHandSize.ToString();

            if (winConditionScoreInput != null)
                winConditionScoreInput.text = currentDefinition.winConditionScore.ToString();

            if (allowBotsToggle != null)
                allowBotsToggle.isOn = currentDefinition.allowBots;
        }

        private void SaveMetadataFromUI()
        {
            if (currentDefinition == null)
                return;

            if (gameNameInput != null)
                currentDefinition.gameName = gameNameInput.text;

            if (authorInput != null)
                currentDefinition.author = authorInput.text;

            if (descriptionInput != null)
                currentDefinition.description = descriptionInput.text;

            if (versionInput != null)
                currentDefinition.version = versionInput.text;

            if (playerCountInput != null && int.TryParse(playerCountInput.text, out int playerCount))
                currentDefinition.playerCount = playerCount;

            if (startingHandSizeInput != null && int.TryParse(startingHandSizeInput.text, out int handSize))
                currentDefinition.startingHandSize = handSize;

            if (winConditionScoreInput != null && int.TryParse(winConditionScoreInput.text, out int winScore))
                currentDefinition.winConditionScore = winScore;

            if (allowBotsToggle != null)
                currentDefinition.allowBots = allowBotsToggle.isOn;
        }

        private void RefreshEditors()
        {
            if (deckEditor != null)
            {
                deckEditor.Initialize(currentDefinition);
            }

            if (ruleGraphEditor != null)
            {
                ruleGraphEditor.Initialize(currentDefinition);
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            }

            Debug.Log($"[GameEditorManager] {message}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Loads an existing custom game definition for editing.
        /// </summary>
        public void LoadGameDefinition(CustomGameDefinition definition)
        {
            if (definition == null)
            {
                UpdateStatus("Error: Cannot load null definition");
                return;
            }

            currentDefinition = definition;
            RefreshEditors();
            LoadMetadataToUI();
            UpdateStatus($"Loaded game: {definition.gameName}");
        }

        /// <summary>
        /// Gets the current game definition being edited.
        /// </summary>
        public CustomGameDefinition GetCurrentDefinition()
        {
            return currentDefinition;
        }

        #endregion
    }
}
