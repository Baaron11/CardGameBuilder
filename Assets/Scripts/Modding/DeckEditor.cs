using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardGameBuilder.Modding
{
    /// <summary>
    /// Runtime deck and card editor UI.
    /// Allows users to create and modify cards and decks for custom games.
    /// </summary>
    public class DeckEditor : MonoBehaviour
    {
        [Header("Card Editor UI")]
        [SerializeField] private GameObject cardEditorPanel;
        [SerializeField] private TMP_InputField cardNameInput;
        [SerializeField] private TMP_InputField cardSuitInput;
        [SerializeField] private TMP_InputField cardValueInput;
        [SerializeField] private TMP_InputField cardDescriptionInput;
        [SerializeField] private Button addCardButton;
        [SerializeField] private Button saveCardButton;
        [SerializeField] private Button deleteCardButton;
        [SerializeField] private Button uploadSpriteButton;
        [SerializeField] private Image cardPreviewImage;

        [Header("Card List UI")]
        [SerializeField] private Transform cardListContainer;
        [SerializeField] private GameObject cardListItemPrefab;

        [Header("Deck Editor UI")]
        [SerializeField] private GameObject deckEditorPanel;
        [SerializeField] private TMP_InputField deckNameInput;
        [SerializeField] private Toggle shuffleOnStartToggle;
        [SerializeField] private Toggle refillWhenEmptyToggle;
        [SerializeField] private Button addDeckButton;
        [SerializeField] private Button saveDeckButton;
        [SerializeField] private Button deleteDeckButton;

        [Header("Deck List UI")]
        [SerializeField] private Transform deckListContainer;
        [SerializeField] private GameObject deckListItemPrefab;
        [SerializeField] private TMP_Dropdown deckSelectionDropdown;

        [Header("Deck Composition UI")]
        [SerializeField] private Transform deckCompositionContainer;
        [SerializeField] private GameObject deckCardItemPrefab;
        [SerializeField] private TMP_Dropdown availableCardsDropdown;
        [SerializeField] private Button addCardToDeckButton;

        private CustomGameDefinition currentDefinition;
        private CardDef selectedCard;
        private DeckDef selectedDeck;
        private Sprite uploadedSprite;

        private void Start()
        {
            SetupUI();
        }

        private void SetupUI()
        {
            if (addCardButton != null)
                addCardButton.onClick.AddListener(OnAddNewCard);

            if (saveCardButton != null)
                saveCardButton.onClick.AddListener(OnSaveCard);

            if (deleteCardButton != null)
                deleteCardButton.onClick.AddListener(OnDeleteCard);

            if (uploadSpriteButton != null)
                uploadSpriteButton.onClick.AddListener(OnUploadSprite);

            if (addDeckButton != null)
                addDeckButton.onClick.AddListener(OnAddNewDeck);

            if (saveDeckButton != null)
                saveDeckButton.onClick.AddListener(OnSaveDeck);

            if (deleteDeckButton != null)
                deleteDeckButton.onClick.AddListener(OnDeleteDeck);

            if (addCardToDeckButton != null)
                addCardToDeckButton.onClick.AddListener(OnAddCardToDeck);

            if (deckSelectionDropdown != null)
                deckSelectionDropdown.onValueChanged.AddListener(OnDeckSelectionChanged);
        }

        public void Initialize(CustomGameDefinition definition)
        {
            currentDefinition = definition;

            if (currentDefinition.cards == null)
                currentDefinition.cards = new List<CardDef>();

            if (currentDefinition.decks == null)
                currentDefinition.decks = new List<DeckDef>();

            RefreshCardList();
            RefreshDeckList();
            RefreshAvailableCardsDropdown();
        }

        #region Card Management

        private void OnAddNewCard()
        {
            CardDef newCard = new CardDef
            {
                id = Guid.NewGuid().ToString(),
                name = "New Card",
                suit = "None",
                value = 1,
                description = ""
            };

            currentDefinition.cards.Add(newCard);
            RefreshCardList();
            RefreshAvailableCardsDropdown();
            SelectCard(newCard);

            Debug.Log($"Created new card: {newCard.name}");
        }

        private void SelectCard(CardDef card)
        {
            selectedCard = card;
            cardEditorPanel?.SetActive(true);

            if (cardNameInput != null)
                cardNameInput.text = card.name;

            if (cardSuitInput != null)
                cardSuitInput.text = card.suit;

            if (cardValueInput != null)
                cardValueInput.text = card.value.ToString();

            if (cardDescriptionInput != null)
                cardDescriptionInput.text = card.description;

            if (cardPreviewImage != null && card.sprite != null)
                cardPreviewImage.sprite = card.sprite;
        }

        private void OnSaveCard()
        {
            if (selectedCard == null)
                return;

            selectedCard.name = cardNameInput?.text ?? selectedCard.name;
            selectedCard.suit = cardSuitInput?.text ?? selectedCard.suit;

            if (cardValueInput != null && int.TryParse(cardValueInput.text, out int value))
                selectedCard.value = value;

            selectedCard.description = cardDescriptionInput?.text ?? selectedCard.description;

            if (uploadedSprite != null)
            {
                selectedCard.sprite = uploadedSprite;
                uploadedSprite = null;
            }

            RefreshCardList();
            RefreshAvailableCardsDropdown();

            Debug.Log($"Saved card: {selectedCard.name}");
        }

        private void OnDeleteCard()
        {
            if (selectedCard == null)
                return;

            // Remove from all decks
            foreach (var deck in currentDefinition.decks)
            {
                deck.cardIds.RemoveAll(id => id == selectedCard.id);
            }

            currentDefinition.cards.Remove(selectedCard);
            selectedCard = null;

            RefreshCardList();
            RefreshDeckList();
            RefreshAvailableCardsDropdown();
            cardEditorPanel?.SetActive(false);

            Debug.Log("Deleted card");
        }

        private void OnUploadSprite()
        {
            // In a real implementation, this would open a file picker
            // For now, we'll use a simple placeholder approach
            Debug.Log("Upload sprite functionality - integrate with file picker");

            // Placeholder: Try to load from Resources or assign a test sprite
            // In production, use StandaloneFileBrowser or similar
#if UNITY_STANDALONE || UNITY_EDITOR
            // Example path-based loading (this is a simplified version)
            // Real implementation would use a file browser dialog
            string examplePath = "UI/card_placeholder";
            Sprite loadedSprite = Resources.Load<Sprite>(examplePath);

            if (loadedSprite != null)
            {
                uploadedSprite = loadedSprite;
                if (cardPreviewImage != null)
                    cardPreviewImage.sprite = uploadedSprite;

                Debug.Log("Sprite loaded successfully");
            }
            else
            {
                Debug.LogWarning("Could not load sprite. Implement file browser for sprite upload.");
            }
#endif
        }

        private void RefreshCardList()
        {
            if (cardListContainer == null || cardListItemPrefab == null)
                return;

            // Clear existing items
            foreach (Transform child in cardListContainer)
            {
                Destroy(child.gameObject);
            }

            // Create list items
            foreach (var card in currentDefinition.cards)
            {
                GameObject itemObj = Instantiate(cardListItemPrefab, cardListContainer);

                TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = $"{card.name} ({card.suit}) - Value: {card.value}";
                }

                Button selectButton = itemObj.GetComponent<Button>();
                if (selectButton != null)
                {
                    CardDef cardRef = card;
                    selectButton.onClick.AddListener(() => SelectCard(cardRef));
                }
            }
        }

        #endregion

        #region Deck Management

        private void OnAddNewDeck()
        {
            DeckDef newDeck = new DeckDef
            {
                name = "New Deck",
                shuffleOnStart = true,
                refillWhenEmpty = false
            };

            currentDefinition.decks.Add(newDeck);
            RefreshDeckList();
            SelectDeck(newDeck);

            Debug.Log($"Created new deck: {newDeck.name}");
        }

        private void SelectDeck(DeckDef deck)
        {
            selectedDeck = deck;
            deckEditorPanel?.SetActive(true);

            if (deckNameInput != null)
                deckNameInput.text = deck.name;

            if (shuffleOnStartToggle != null)
                shuffleOnStartToggle.isOn = deck.shuffleOnStart;

            if (refillWhenEmptyToggle != null)
                refillWhenEmptyToggle.isOn = deck.refillWhenEmpty;

            RefreshDeckComposition();
        }

        private void OnSaveDeck()
        {
            if (selectedDeck == null)
                return;

            selectedDeck.name = deckNameInput?.text ?? selectedDeck.name;
            selectedDeck.shuffleOnStart = shuffleOnStartToggle?.isOn ?? selectedDeck.shuffleOnStart;
            selectedDeck.refillWhenEmpty = refillWhenEmptyToggle?.isOn ?? selectedDeck.refillWhenEmpty;

            RefreshDeckList();

            Debug.Log($"Saved deck: {selectedDeck.name} ({selectedDeck.GetCardCount()} cards)");
        }

        private void OnDeleteDeck()
        {
            if (selectedDeck == null)
                return;

            currentDefinition.decks.Remove(selectedDeck);
            selectedDeck = null;

            RefreshDeckList();
            deckEditorPanel?.SetActive(false);

            Debug.Log("Deleted deck");
        }

        private void OnDeckSelectionChanged(int index)
        {
            if (index >= 0 && index < currentDefinition.decks.Count)
            {
                SelectDeck(currentDefinition.decks[index]);
            }
        }

        private void RefreshDeckList()
        {
            if (deckListContainer == null || deckListItemPrefab == null)
                return;

            // Clear existing items
            foreach (Transform child in deckListContainer)
            {
                Destroy(child.gameObject);
            }

            // Update dropdown
            if (deckSelectionDropdown != null)
            {
                deckSelectionDropdown.ClearOptions();
                List<string> deckNames = new List<string>();
                foreach (var deck in currentDefinition.decks)
                {
                    deckNames.Add($"{deck.name} ({deck.GetCardCount()} cards)");
                }
                deckSelectionDropdown.AddOptions(deckNames);
            }

            // Create list items
            foreach (var deck in currentDefinition.decks)
            {
                GameObject itemObj = Instantiate(deckListItemPrefab, deckListContainer);

                TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = $"{deck.name} ({deck.GetCardCount()} cards)";
                }

                Button selectButton = itemObj.GetComponent<Button>();
                if (selectButton != null)
                {
                    DeckDef deckRef = deck;
                    selectButton.onClick.AddListener(() => SelectDeck(deckRef));
                }
            }
        }

        #endregion

        #region Deck Composition

        private void RefreshAvailableCardsDropdown()
        {
            if (availableCardsDropdown == null)
                return;

            availableCardsDropdown.ClearOptions();

            List<string> cardNames = new List<string>();
            foreach (var card in currentDefinition.cards)
            {
                cardNames.Add($"{card.name} ({card.suit})");
            }

            availableCardsDropdown.AddOptions(cardNames);
        }

        private void OnAddCardToDeck()
        {
            if (selectedDeck == null || currentDefinition.cards.Count == 0)
                return;

            int cardIndex = availableCardsDropdown?.value ?? 0;
            if (cardIndex >= 0 && cardIndex < currentDefinition.cards.Count)
            {
                CardDef card = currentDefinition.cards[cardIndex];
                selectedDeck.cardIds.Add(card.id);

                RefreshDeckComposition();
                RefreshDeckList();

                Debug.Log($"Added {card.name} to {selectedDeck.name}");
            }
        }

        private void RefreshDeckComposition()
        {
            if (deckCompositionContainer == null || deckCardItemPrefab == null || selectedDeck == null)
                return;

            // Clear existing items
            foreach (Transform child in deckCompositionContainer)
            {
                Destroy(child.gameObject);
            }

            // Create items for each card in deck
            for (int i = 0; i < selectedDeck.cardIds.Count; i++)
            {
                string cardId = selectedDeck.cardIds[i];
                CardDef card = currentDefinition.cards.Find(c => c.id == cardId);

                if (card != null)
                {
                    GameObject itemObj = Instantiate(deckCardItemPrefab, deckCompositionContainer);

                    TextMeshProUGUI nameText = itemObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                    if (nameText != null)
                    {
                        nameText.text = $"{card.name} ({card.suit}) - Value: {card.value}";
                    }

                    // Add remove button
                    Button removeButton = itemObj.transform.Find("RemoveButton")?.GetComponent<Button>();
                    if (removeButton != null)
                    {
                        int index = i;
                        removeButton.onClick.AddListener(() => RemoveCardFromDeck(index));
                    }
                }
            }
        }

        private void RemoveCardFromDeck(int index)
        {
            if (selectedDeck != null && index >= 0 && index < selectedDeck.cardIds.Count)
            {
                selectedDeck.cardIds.RemoveAt(index);
                RefreshDeckComposition();
                RefreshDeckList();

                Debug.Log("Removed card from deck");
            }
        }

        #endregion

        public void ClearSelection()
        {
            selectedCard = null;
            selectedDeck = null;
            cardEditorPanel?.SetActive(false);
            deckEditorPanel?.SetActive(false);
        }

        /// <summary>
        /// Quick builder for standard 52-card deck.
        /// </summary>
        public void CreateStandardDeck()
        {
            if (currentDefinition == null)
                return;

            string[] suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
            string[] ranks = { "Ace", "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King" };
            int[] values = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

            currentDefinition.cards.Clear();

            for (int s = 0; s < suits.Length; s++)
            {
                for (int r = 0; r < ranks.Length; r++)
                {
                    CardDef card = new CardDef
                    {
                        id = Guid.NewGuid().ToString(),
                        name = ranks[r],
                        suit = suits[s],
                        value = values[r],
                        description = $"{ranks[r]} of {suits[s]}"
                    };
                    currentDefinition.cards.Add(card);
                }
            }

            // Create a standard deck
            DeckDef standardDeck = new DeckDef
            {
                name = "Standard 52-Card Deck",
                shuffleOnStart = true
            };

            foreach (var card in currentDefinition.cards)
            {
                standardDeck.cardIds.Add(card.id);
            }

            currentDefinition.decks.Clear();
            currentDefinition.decks.Add(standardDeck);

            RefreshCardList();
            RefreshDeckList();
            RefreshAvailableCardsDropdown();

            Debug.Log("Created standard 52-card deck");
        }
    }
}
