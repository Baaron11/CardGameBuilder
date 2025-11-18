using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CardGameBuilder.Core;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// UI for browsing and launching custom game mods.
    /// Displays installed mods with preview details and play button.
    /// </summary>
    public class ModBrowserUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExportImportManager exportImportManager;
        [SerializeField] private CardGameManager gameManager;

        [Header("UI Elements")]
        [SerializeField] private GameObject modBrowserPanel;
        [SerializeField] private Transform modListContainer;
        [SerializeField] private GameObject modListItemPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button importButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Preview Panel")]
        [SerializeField] private GameObject previewPanel;
        [SerializeField] private TextMeshProUGUI previewGameName;
        [SerializeField] private TextMeshProUGUI previewAuthor;
        [SerializeField] private TextMeshProUGUI previewDescription;
        [SerializeField] private TextMeshProUGUI previewVersion;
        [SerializeField] private TextMeshProUGUI previewPlayerCount;
        [SerializeField] private TextMeshProUGUI previewCardCount;
        [SerializeField] private TextMeshProUGUI previewRuleCount;
        [SerializeField] private Button playButton;
        [SerializeField] private Button uninstallButton;

        private List<CustomGameDefinition> installedMods = new List<CustomGameDefinition>();
        private CustomGameDefinition selectedMod;

        private void Start()
        {
            SetupUI();
            RefreshModList();
        }

        private void SetupUI()
        {
            if (refreshButton != null)
                refreshButton.onClick.AddListener(RefreshModList);

            if (importButton != null)
                importButton.onClick.AddListener(OnImportClicked);

            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (uninstallButton != null)
                uninstallButton.onClick.AddListener(OnUninstallClicked);

            if (previewPanel != null)
                previewPanel.SetActive(false);
        }

        public void Show()
        {
            if (modBrowserPanel != null)
                modBrowserPanel.SetActive(true);

            RefreshModList();
        }

        public void Hide()
        {
            if (modBrowserPanel != null)
                modBrowserPanel.SetActive(false);

            if (previewPanel != null)
                previewPanel.SetActive(false);
        }

        private void RefreshModList()
        {
            if (exportImportManager == null)
            {
                Debug.LogError("ExportImportManager not assigned!");
                return;
            }

            installedMods = exportImportManager.GetInstalledMods();
            UpdateModListUI();

            if (statusText != null)
            {
                statusText.text = $"Found {installedMods.Count} installed mod(s)";
            }

            Debug.Log($"Loaded {installedMods.Count} installed mods");
        }

        private void UpdateModListUI()
        {
            if (modListContainer == null || modListItemPrefab == null)
                return;

            // Clear existing items
            foreach (Transform child in modListContainer)
            {
                Destroy(child.gameObject);
            }

            // Create list items
            foreach (var mod in installedMods)
            {
                GameObject itemObj = Instantiate(modListItemPrefab, modListContainer);

                // Set mod name
                TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = mod.gameName;
                }

                // Set mod info
                TextMeshProUGUI infoText = itemObj.transform.Find("InfoText")?.GetComponent<TextMeshProUGUI>();
                if (infoText != null)
                {
                    infoText.text = $"by {mod.author} | v{mod.version} | {mod.cards.Count} cards";
                }

                // Add select button
                Button selectButton = itemObj.GetComponent<Button>();
                if (selectButton != null)
                {
                    CustomGameDefinition modRef = mod;
                    selectButton.onClick.AddListener(() => SelectMod(modRef));
                }
            }
        }

        private void SelectMod(CustomGameDefinition mod)
        {
            selectedMod = mod;
            ShowPreview(mod);
        }

        private void ShowPreview(CustomGameDefinition mod)
        {
            if (previewPanel == null)
                return;

            previewPanel.SetActive(true);

            if (previewGameName != null)
                previewGameName.text = mod.gameName;

            if (previewAuthor != null)
                previewAuthor.text = $"Author: {mod.author}";

            if (previewDescription != null)
                previewDescription.text = mod.description;

            if (previewVersion != null)
                previewVersion.text = $"Version: {mod.version}";

            if (previewPlayerCount != null)
                previewPlayerCount.text = $"Players: {mod.playerCount}";

            if (previewCardCount != null)
                previewCardCount.text = $"Cards: {mod.cards.Count}";

            if (previewRuleCount != null)
                previewRuleCount.text = $"Rule Nodes: {mod.rules.nodes.Count}";
        }

        private void OnImportClicked()
        {
            // In a real implementation, open file browser
            // For now, provide instructions
            Debug.Log("Import mod functionality - integrate with file browser");

            if (statusText != null)
            {
                statusText.text = "Import: Place .zip file in Mods folder and click Refresh";
            }

            // Example: Auto-import any zip files in the mods directory
#if UNITY_STANDALONE || UNITY_EDITOR
            AutoImportZipFiles();
#endif
        }

        private void AutoImportZipFiles()
        {
            if (exportImportManager == null)
                return;

            string modsPath = System.IO.Path.Combine(Application.persistentDataPath, "Mods");
            if (!System.IO.Directory.Exists(modsPath))
                return;

            string[] zipFiles = System.IO.Directory.GetFiles(modsPath, "*.zip");

            int importedCount = 0;
            foreach (string zipFile in zipFiles)
            {
                CustomGameDefinition importedMod;
                if (exportImportManager.Import(zipFile, out importedMod))
                {
                    importedCount++;
                    Debug.Log($"Auto-imported: {importedMod.gameName}");

                    // Optionally delete the zip file after successful import
                    // System.IO.File.Delete(zipFile);
                }
            }

            if (importedCount > 0)
            {
                RefreshModList();
                if (statusText != null)
                {
                    statusText.text = $"Imported {importedCount} mod(s) successfully";
                }
            }
        }

        private void OnPlayClicked()
        {
            if (selectedMod == null)
            {
                Debug.LogWarning("No mod selected");
                return;
            }

            // Validate mod before launching
            string validationError;
            if (!selectedMod.Validate(out validationError))
            {
                Debug.LogError($"Cannot play mod - validation failed: {validationError}");
                if (statusText != null)
                {
                    statusText.text = $"Error: {validationError}";
                }
                return;
            }

            // Launch the custom game
            LaunchCustomGame(selectedMod);
        }

        private void LaunchCustomGame(CustomGameDefinition mod)
        {
            if (gameManager == null)
            {
                Debug.LogError("CardGameManager not assigned!");
                return;
            }

            // Set the custom game on the game manager
            gameManager.SetCustomGame(mod);

            Debug.Log($"Launching custom game: {mod.gameName}");

            // Hide browser UI
            Hide();

            // In a real implementation, transition to the game scene or start the game
            // For now, just log the action
            if (statusText != null)
            {
                statusText.text = $"Launching {mod.gameName}...";
            }
        }

        private void OnUninstallClicked()
        {
            if (selectedMod == null)
            {
                Debug.LogWarning("No mod selected");
                return;
            }

            if (exportImportManager == null)
            {
                Debug.LogError("ExportImportManager not assigned!");
                return;
            }

            string modName = selectedMod.gameName;

            // Show confirmation (in production, use a proper dialog)
            Debug.Log($"Uninstalling mod: {modName}");

            if (exportImportManager.UninstallMod(modName))
            {
                selectedMod = null;
                if (previewPanel != null)
                    previewPanel.SetActive(false);

                RefreshModList();

                if (statusText != null)
                {
                    statusText.text = $"Uninstalled {modName}";
                }
            }
            else
            {
                if (statusText != null)
                {
                    statusText.text = $"Failed to uninstall {modName}";
                }
            }
        }

        /// <summary>
        /// Opens the mod browser from the main menu.
        /// </summary>
        public void OpenFromMainMenu()
        {
            Show();
        }

        /// <summary>
        /// Returns to main menu.
        /// </summary>
        public void ReturnToMainMenu()
        {
            Hide();
            // In a real implementation, show main menu UI
        }
    }
}
