# M4 UI Setup Checklist

This checklist guides you through setting up the Unity UI for the M4 Custom Game Editor & Modding System.

## 1. Create GameEditor Scene

### Scene Setup
- [ ] Create new scene: `Assets/Scenes/GameEditor.unity`
- [ ] Set background color to dark gray
- [ ] Lighting: Set ambient to flat

### Canvas Setup
- [ ] Create Canvas (GameObject → UI → Canvas)
- [ ] Set Render Mode: Screen Space - Overlay
- [ ] Add Canvas Scaler component
  - UI Scale Mode: Scale With Screen Size
  - Reference Resolution: 1920x1080
  - Match: 0.5 (width/height)

## 2. Create UI Hierarchy

### Root Objects
```
GameEditorCanvas
├── Background (Image - dark gray)
├── HeaderPanel
├── NavigationPanel
├── ContentArea
├── ActionPanel
└── StatusPanel
```

### HeaderPanel
- [ ] Create Panel: "HeaderPanel"
- [ ] Anchor: Top stretch
- [ ] Height: 80
- [ ] Add child Text (TMP): "TitleText"
  - Text: "Card Game Editor - M4"
  - Font Size: 36
  - Alignment: Center
  - Color: White

### NavigationPanel
- [ ] Create Panel: "NavigationPanel"
- [ ] Anchor: Left stretch (exclude header)
- [ ] Width: 200
- [ ] Add Vertical Layout Group
  - Spacing: 10
  - Padding: 10
- [ ] Add 3 buttons:
  ```
  MetadataTabButton → "Game Info"
  DeckEditorTabButton → "Cards & Decks"
  RuleEditorTabButton → "Rule Graph"
  ```

### ContentArea
- [ ] Create Panel: "ContentArea"
- [ ] Anchor: Fill (exclude header and nav)
- [ ] Add 3 child panels (only one active at a time):

#### MetadataPanel
- [ ] Create Panel: "MetadataPanel"
- [ ] Add Vertical Layout Group (spacing: 15, padding: 20)
- [ ] Add input fields (each with label above):
  ```
  GameNameInput - "Game Name"
  AuthorInput - "Author Name"
  DescriptionInput - "Description" (multiline, height: 100)
  VersionInput - "Version (e.g., 1.0.0)"
  PlayerCountInput - "Player Count (2-8)"
  StartingHandSizeInput - "Starting Hand Size"
  WinConditionScoreInput - "Win Condition Score"
  ```
- [ ] Add Toggle with label:
  ```
  AllowBotsToggle - "Allow Bots"
  ```

#### DeckEditorPanel
- [ ] Create Panel: "DeckEditorPanel"
- [ ] Add 4 sections (use Horizontal Layout Group at root):

**Section 1: Card List (width: 25%)**
- [ ] Header: "Cards"
- [ ] ScrollView with:
  - CardListContainer (Vertical Layout Group)
  - AddCardButton at bottom

**Section 2: Card Editor (width: 35%)**
- [ ] Header: "Edit Card"
- [ ] Vertical layout with:
  ```
  CardNameInput
  CardSuitInput
  CardValueInput
  CardDescriptionInput (multiline)
  UploadSpriteButton
  CardPreviewImage (aspect ratio 2:3, height: 200)
  Row: [SaveCardButton] [DeleteCardButton]
  ```

**Section 3: Deck List (width: 20%)**
- [ ] Header: "Decks"
- [ ] ScrollView with:
  - DeckListContainer (Vertical Layout Group)
  - AddDeckButton at bottom

**Section 4: Deck Editor (width: 20%)**
- [ ] Header: "Deck Composition"
- [ ] Vertical layout with:
  ```
  DeckNameInput
  ShuffleOnStartToggle
  RefillWhenEmptyToggle
  Divider
  ScrollView: DeckCompositionContainer
  Divider
  AvailableCardsDropdown
  AddCardToDeckButton
  SaveDeckButton
  DeleteDeckButton
  ```

#### RuleEditorPanel
- [ ] Create Panel: "RuleEditorPanel"
- [ ] Add 3 sections:

**Toolbar (top, height: 60)**
- [ ] Horizontal layout:
  ```
  NodeTypeDropdown (width: 150)
  NodeSubTypeDropdown (width: 200)
  CreateNodeButton
  Spacer
  HelpButton (?)
  ```

**GraphCanvas (center, fill remaining)**
- [ ] Add ScrollRect
- [ ] Content: "GraphContent"
  - RectTransform size: 4000x4000
  - Background: Dark gray grid pattern (optional)
  - This will contain dynamic nodes and links

**NodePropertiesPanel (right, width: 300)**
- [ ] Initially hidden
- [ ] Vertical layout:
  ```
  Header: "Node Properties"
  PropertiesContainer (Vertical Layout Group)
  Spacer
  DeleteNodeButton (red)
  ```

### ActionPanel
- [ ] Create Panel: "ActionPanel"
- [ ] Anchor: Bottom stretch
- [ ] Height: 60
- [ ] Add Horizontal Layout Group
- [ ] Add buttons (equal width):
  ```
  NewGameButton - "New Game"
  SaveGameButton - "Save"
  ExportButton - "Export Mod"
  TestGameButton - "Test Game"
  CreateStandardDeckButton - "Create 52-Card Deck"
  ```

### StatusPanel
- [ ] Create Panel: "StatusPanel"
- [ ] Anchor: Bottom stretch (below ActionPanel)
- [ ] Height: 30
- [ ] Background: Semi-transparent dark
- [ ] Add Text (TMP): "StatusText"
  - Alignment: Left
  - Font Size: 14
  - Color: Light yellow

## 3. Create Prefabs

### NodeTemplate Prefab
```
NodeTemplate (Prefab)
├── Background (Image)
│   - Size: 200x80
│   - Color: Will be set by code
│   - Outline: 2px white
├── NodeNameText (TMP_Text)
│   - Alignment: Center
│   - Font Size: 16
│   - Color: White
└── Button component (on root)
```

**Create steps:**
- [ ] Create empty GameObject
- [ ] Add Image component
- [ ] Add Button component
- [ ] Add child TextMeshProUGUI
- [ ] Save as prefab: `Assets/Prefabs/Modding/NodeTemplate.prefab`

### CardListItemPrefab
```
CardListItemPrefab
├── Background (Image)
├── NameText (TMP_Text)
└── Button component
```

**Create steps:**
- [ ] Create Panel
- [ ] Set height: 40
- [ ] Add Button component
- [ ] Add child TMP_Text: "NameText"
- [ ] Save as prefab: `Assets/Prefabs/Modding/CardListItemPrefab.prefab`

### DeckListItemPrefab
- [ ] Same as CardListItemPrefab
- [ ] Save as: `Assets/Prefabs/Modding/DeckListItemPrefab.prefab`

### ParameterInputPrefab
```
ParameterInputPrefab
├── Horizontal Layout Group
├── Label (TMP_Text, width: 120)
└── InputField (TMP_InputField, flex: 1)
```

**Create steps:**
- [ ] Create Panel with Horizontal Layout
- [ ] Height: 35
- [ ] Add Label (left)
- [ ] Add InputField (right, flexible width)
- [ ] Save as: `Assets/Prefabs/Modding/ParameterInputPrefab.prefab`

### DeckCardItemPrefab
```
DeckCardItemPrefab
├── Horizontal Layout Group
├── NameText (TMP_Text, flex: 1)
└── RemoveButton (Button, width: 60, text: "Remove")
```

**Create steps:**
- [ ] Create Panel with Horizontal Layout
- [ ] Height: 35
- [ ] Add NameText (left, flexible)
- [ ] Add RemoveButton (right, fixed width)
- [ ] Save as: `Assets/Prefabs/Modding/DeckCardItemPrefab.prefab`

## 4. Add Components & Assign References

### Create Manager GameObjects
- [ ] Create empty: "GameEditorManager"
- [ ] Create empty: "DeckEditor"
- [ ] Create empty: "RuleGraphEditor"
- [ ] Create empty: "ExportImportManager"

### GameEditorManager Component
- [ ] Add component: `GameEditorManager`
- [ ] Assign references:
  ```
  Editor Components:
    - DeckEditor: DeckEditor GameObject
    - RuleGraphEditor: RuleGraphEditor GameObject
    - ExportImportManager: ExportImportManager GameObject

  UI Panels:
    - MetadataPanel
    - DeckEditorPanel
    - RuleEditorPanel

  Metadata UI:
    - GameNameInput
    - AuthorInput
    - DescriptionInput
    - VersionInput
    - PlayerCountInput
    - StartingHandSizeInput
    - WinConditionScoreInput
    - AllowBotsToggle

  Navigation Buttons:
    - MetadataTabButton
    - DeckEditorTabButton
    - RuleEditorTabButton

  Action Buttons:
    - NewGameButton
    - SaveGameButton
    - ExportButton
    - TestGameButton
    - CreateStandardDeckButton

  Status:
    - StatusText
  ```

### DeckEditor Component
- [ ] Add component: `DeckEditor`
- [ ] Assign references:
  ```
  Card Editor UI:
    - CardEditorPanel
    - CardNameInput
    - CardSuitInput
    - CardValueInput
    - CardDescriptionInput
    - AddCardButton
    - SaveCardButton
    - DeleteCardButton
    - UploadSpriteButton
    - CardPreviewImage

  Card List UI:
    - CardListContainer
    - CardListItemPrefab

  Deck Editor UI:
    - DeckEditorPanel
    - DeckNameInput
    - ShuffleOnStartToggle
    - RefillWhenEmptyToggle
    - AddDeckButton
    - SaveDeckButton
    - DeleteDeckButton

  Deck List UI:
    - DeckListContainer
    - DeckListItemPrefab
    - DeckSelectionDropdown

  Deck Composition UI:
    - DeckCompositionContainer
    - DeckCardItemPrefab
    - AvailableCardsDropdown
    - AddCardToDeckButton
  ```

### RuleGraphEditor Component
- [ ] Add component: `RuleGraphEditor`
- [ ] Assign references:
  ```
  References:
    - GraphCanvas: GraphContent RectTransform
    - NodeTemplate: NodeTemplate prefab
    - LinkLineTemplate: LineRenderer prefab (create if needed)
    - ScrollRect: GraphCanvas ScrollRect

  UI Panels:
    - NodeCreationPanel: Toolbar panel
    - NodeTypeDropdown
    - NodeSubTypeDropdown
    - CreateNodeButton
    - NodePropertiesPanel
    - PropertiesContainer

  Prefabs:
    - ParameterInputPrefab
  ```

### ExportImportManager Component
- [ ] Add component: `ExportImportManager`
- [ ] No references needed (uses persistentDataPath)

## 5. Create LineRenderer Prefab for Links

### LinkLineTemplate Prefab
- [ ] Create empty GameObject: "LinkLine"
- [ ] Add LineRenderer component:
  ```
  Width: 3
  Color: White
  Material: Default-Line (or Sprites/Default)
  Positions: Array of 2 points
  ```
- [ ] Save as: `Assets/Prefabs/Modding/LinkLineTemplate.prefab`

## 6. Mod Browser UI Setup

### Add to Main Menu Scene
- [ ] Open main menu/lobby scene
- [ ] Add button: "Custom Games"
- [ ] Create ModBrowser UI panel (hidden by default)

### ModBrowserCanvas Hierarchy
```
ModBrowserPanel (full screen overlay)
├── Background (semi-transparent black)
├── ContentPanel (centered, 80% width/height)
│   ├── HeaderPanel
│   │   ├── TitleText: "Custom Games"
│   │   ├── RefreshButton
│   │   └── CloseButton (X)
│   ├── ModListPanel (left, 40%)
│   │   ├── ScrollView
│   │   │   └── ModListContainer (Vertical Layout)
│   │   └── ImportButton
│   ├── PreviewPanel (right, 60%)
│   │   ├── GameNameText (large)
│   │   ├── AuthorText
│   │   ├── DescriptionText (scrollable)
│   │   ├── StatsPanel (grid):
│   │   │   ├── VersionText
│   │   │   ├── PlayerCountText
│   │   │   ├── CardCountText
│   │   │   └── RuleCountText
│   │   ├── PlayButton (large, green)
│   │   └── UninstallButton (red)
│   └── StatusPanel (bottom)
│       └── StatusText
```

### ModListItemPrefab
```
ModListItemPrefab
├── Background (Image)
│   - Height: 80
├── NameText (TMP_Text, bold)
├── InfoText (TMP_Text, smaller)
└── Button component
```

**Create steps:**
- [ ] Create Panel (height: 80)
- [ ] Add Button component
- [ ] Add NameText (top)
- [ ] Add InfoText (bottom, smaller font)
- [ ] Save as: `Assets/Prefabs/Modding/ModListItemPrefab.prefab`

### ModBrowserUI Component
- [ ] Add component: `ModBrowserUI`
- [ ] Assign references:
  ```
  References:
    - ExportImportManager
    - GameManager (CardGameManager)

  UI Elements:
    - ModBrowserPanel
    - ModListContainer
    - ModListItemPrefab
    - RefreshButton
    - ImportButton
    - StatusText

  Preview Panel:
    - PreviewPanel
    - PreviewGameName
    - PreviewAuthor
    - PreviewDescription
    - PreviewVersion
    - PreviewPlayerCount
    - PreviewCardCount
    - PreviewRuleCount
    - PlayButton
    - UninstallButton
  ```

## 7. Testing Checklist

### Basic Functionality
- [ ] Open GameEditor scene
- [ ] All panels load without errors
- [ ] Clicking navigation tabs switches panels
- [ ] Input fields accept text
- [ ] Buttons respond to clicks

### Deck Editor
- [ ] "Add Card" creates new card entry
- [ ] Clicking card in list populates editor
- [ ] "Save Card" updates card
- [ ] "Delete Card" removes card
- [ ] "Add Deck" creates new deck
- [ ] Can add cards to deck

### Rule Editor
- [ ] Node type dropdown populated
- [ ] Sub-type dropdown updates on type change
- [ ] "Create Node" adds node to canvas
- [ ] Nodes appear in graph canvas
- [ ] Nodes are draggable
- [ ] Clicking node shows properties panel

### Export/Import
- [ ] "Export" button creates .zip file
- [ ] Check Application.persistentDataPath/Mods for file
- [ ] "Import" (via ModBrowser) loads mod
- [ ] Imported mod appears in ModBrowser list

### Mod Browser
- [ ] Opens from main menu
- [ ] Lists installed mods
- [ ] Clicking mod shows preview
- [ ] "Play" button closes browser and starts game
- [ ] "Uninstall" removes mod

## 8. Styling (Optional but Recommended)

### Color Scheme
```
Background: #2B2B2B
Panels: #3C3C3C
Headers: #4A4A4A
Buttons (Normal): #5A5A5A
Buttons (Hover): #6A6A6A
Buttons (Pressed): #4A4A4A
Text: #FFFFFF
Accent (Play): #4CAF50
Accent (Delete): #F44336
Accent (Export): #2196F3
```

### Fonts
- [ ] Headers: Bold, Size 24-36
- [ ] Body: Regular, Size 14-16
- [ ] Small text: Size 12

### Spacing
- [ ] Panel padding: 15-20px
- [ ] Button height: 40px
- [ ] Input field height: 35px
- [ ] Section spacing: 20px

## 9. Final Validation

- [ ] No null reference errors in console
- [ ] All buttons have onClick listeners
- [ ] All input fields can be edited
- [ ] All prefabs assigned in components
- [ ] Scene saves without warnings
- [ ] Can build and run scene
- [ ] UI scales correctly on different resolutions

## 10. Build Settings

- [ ] Add GameEditor.unity to Build Settings
- [ ] Set scene index (e.g., 2)
- [ ] Test scene transition from main menu
- [ ] Test return to main menu from editor

---

**Completion Time Estimate:** 2-3 hours for full UI setup

**Next Steps:** After UI setup complete, proceed to M4_IMPLEMENTATION_GUIDE.md for testing workflow.
