# Card Game Builder - Integration & Testing Guide

## 📋 Overview

This guide explains how to integrate the Card Engine system into your Unity project and test the three game prototypes (War, Go Fish, Hearts).

---

## 🔧 Prerequisites

### Required Unity Packages

Before proceeding, install these packages via **Window → Package Manager**:

1. **Netcode for GameObjects** (`com.unity.netcode.gameobjects`)
   - Version: 1.5.0 or later
   - Essential for multiplayer networking

2. **Unity Transport** (`com.unity.transport`)
   - Version: 2.0.0 or later
   - Provides LAN networking capabilities

3. **TextMeshPro** (Usually pre-installed)
   - Required for UI text elements

### Installation Steps

```
1. Open Unity Package Manager (Window → Package Manager)
2. Click the '+' button → "Add package by name"
3. Add: com.unity.netcode.gameobjects
4. Add: com.unity.transport
5. Wait for packages to import
```

---

## 🏗️ Scene Setup

### Step 1: Create Network Manager

1. **Create GameObject**: Right-click in Hierarchy → Create Empty → Name it "NetworkManager"

2. **Add Components** to NetworkManager GameObject:
   - `NetworkManager` (Unity Netcode component)
   - `UnityTransport` (Transport layer)
   - `CardGameManager` (Your custom script)
   - `NetworkGameManager` (Your custom script)

3. **Configure NetworkManager**:
   - In Inspector, under NetworkManager component:
     - **Transport**: Drag the UnityTransport component to this field
     - **PlayerPrefab**: Leave empty for now (not needed for card games)
     - **NetworkConfig**: Leave default settings

4. **Configure UnityTransport**:
   - **Address**: `127.0.0.1` (for local testing)
   - **Port**: `7777`
   - **Protocol Type**: UnityTransport

5. **Configure NetworkGameManager**:
   - **Card Game Manager**: Drag the CardGameManager component (from same GameObject)
   - **Board UI**: Will assign after creating UI (Step 2)

6. **Add NetworkObject Component**:
   - Click "Add Component" → Search for "Network Object"
   - This makes the manager network-aware

---

### Step 2: Create Board UI (Host/Display View)

1. **Create Canvas**:
   - Right-click Hierarchy → UI → Canvas
   - Rename to "BoardCanvas"
   - Canvas Scaler → UI Scale Mode: Scale with Screen Size
   - Reference Resolution: 1920x1080

2. **Add UI Elements** (all children of BoardCanvas):

   **A. Host Control Panel**
   ```
   - Create Empty GameObject: "HostPanel"
     - Add Vertical Layout Group component
     - Child elements:
       - TMP_Text: "Game Setup" (header)
       - TMP_Dropdown: "GameTypeDropdown"
       - TMP_InputField: "SeedInput" (placeholder: "Seed (optional)")
       - Button: "StartGameButton" (text: "Start Game")
   ```

   **B. Game Info Display**
   ```
   - TMP_Text: "GameStateText" (top-left)
   - TMP_Text: "CurrentTurnText" (top-center, larger font)
   - TMP_Text: "RoundNumberText" (top-right)
   ```

   **C. Player List Panel**
   ```
   - Create Empty GameObject: "PlayerListContainer"
     - Add Vertical Layout Group
     - Content Size Fitter: Vertical Fit = Preferred Size
   ```

   **D. Event Log Panel**
   ```
   - Create ScrollView: "EventLog"
     - Child: Viewport → Content
       - Add to Content: TMP_Text "EventLogText"
         - Enable Rich Text
         - Overflow: Overflow
         - Alignment: Top-Left
   ```

3. **Add BoardUI Script**:
   - Select BoardCanvas
   - Add Component → `BoardUI` script
   - Assign references in Inspector:
     - Host Panel: Drag "HostPanel"
     - Game Type Dropdown: Drag "GameTypeDropdown"
     - Start Game Button: Drag "StartGameButton"
     - Seed Input Field: Drag "SeedInput"
     - Game State Text: Drag "GameStateText"
     - Current Turn Text: Drag "CurrentTurnText"
     - Round Number Text: Drag "RoundNumberText"
     - Player List Container: Drag "PlayerListContainer"
     - Event Log Text: Drag "EventLogText"
     - Event Log Scroll Rect: Drag "EventLog"

4. **Create Player Info Prefab** (for player list):
   ```
   - Create Empty GameObject outside canvas: "PlayerInfoPrefab"
   - Add components:
     - Image (background)
     - TMP_Text: "PlayerName"
     - TMP_Text: "Score"
     - TMP_Text: "HandCount"
   - Add Component: PlayerInfoUI script
   - Assign text references in PlayerInfoUI script
   - Drag to Project to create prefab
   - Delete from Hierarchy
   - Assign prefab to BoardUI → Player Info Prefab field
   ```

5. **Link to NetworkGameManager**:
   - Select NetworkManager GameObject
   - In NetworkGameManager component:
     - Board UI: Drag "BoardCanvas"

---

### Step 3: Create Controller UI (Player Hand View)

1. **Create Canvas**:
   - Right-click Hierarchy → UI → Canvas
   - Rename to "ControllerCanvas"
   - Canvas Scaler → UI Scale Mode: Scale with Screen Size

2. **Add UI Elements**:

   **A. Player Info Panel (Top)**
   ```
   - TMP_Text: "PlayerNameText" (large, centered)
   - TMP_Text: "StatusText" (instructions/feedback)
   - TMP_Text: "ScoreText" (current score)
   ```

   **B. Hand Display (Center)**
   ```
   - Create Empty GameObject: "HandContainer"
     - Add Horizontal Layout Group
     - Add Content Size Fitter
     - Child Alignment: Middle Center
     - Spacing: 10
   - TMP_Text: "HandCountText" (shows "Cards: X")
   ```

   **C. Turn Indicator**
   ```
   - Create Panel: "TurnIndicator"
     - Add Image component (background color)
     - Child: TMP_Text "TurnIndicatorText" (large text: "YOUR TURN" / "Waiting...")
   ```

   **D. Action Button Panels** (Create 3 panels, initially inactive):

   **War Panel:**
   ```
   - GameObject: "WarButtonPanel" (Vertical Layout)
     - Button: "FlipCardButton" (text: "Flip Card")
   ```

   **Go Fish Panel:**
   ```
   - GameObject: "GoFishButtonPanel" (Vertical Layout)
     - TMP_Dropdown: "TargetPlayerDropdown"
     - TMP_Dropdown: "TargetRankDropdown"
     - Button: "AskButton" (text: "Ask for Cards")
     - Button: "DrawButton" (text: "Draw Card")
   ```

   **Hearts Panel:**
   ```
   - GameObject: "HeartsButtonPanel" (Vertical Layout)
     - Button: "PlayCardButton" (text: "Play Selected Card")
   ```

3. **Add ControllerUI Script**:
   - Select ControllerCanvas
   - Add Component → `ControllerUI` script
   - Assign all references in Inspector

4. **Create Card UI Prefab**:
   ```
   - Create Button: "CardUIPrefab"
   - Add components:
     - Image (card background)
     - Child: TMP_Text (card text, e.g., "A♥")
   - Add Component: CardUIElement script
   - Assign text/image references
   - Drag to Project to create prefab
   - Delete from Hierarchy
   - Assign prefab to ControllerUI → Card UI Prefab field
   ```

---

### Step 4: Create Network Connection UI (Optional but Recommended)

1. **Create Canvas**:
   - Right-click Hierarchy → UI → Canvas
   - Rename to "NetworkCanvas"

2. **Add UI Elements**:
   ```
   - Panel: "ConnectionPanel"
     - TMP_Text: "Network Setup" (header)
     - TMP_InputField: "IPAddressField" (default: 127.0.0.1)
     - Button: "HostButton" (text: "Start as Host")
     - Button: "ClientButton" (text: "Join as Client")
     - Button: "ServerButton" (text: "Start as Server")
     - TMP_Text: "StatusText" (feedback messages)
   ```

3. **Add NetworkUI Script**:
   - Select NetworkCanvas
   - Add Component → `NetworkUI` script
   - Assign all button/text references

---

## 🎮 Testing Instructions

### Local Testing (Single Computer)

#### Test 1: Host + 1 Client (2 Players)

**Setup:**
1. Open Unity project
2. Build the game:
   - File → Build Settings
   - Add current scene
   - Click "Build" → Save as "CardGame.exe"

**Testing Steps:**

1. **Start Host (Unity Editor)**:
   - Press Play in Unity
   - Click "Start as Host" button
   - BoardUI should show "Started as Host - waiting for players..."
   - Select game type from dropdown (e.g., "War")
   - Wait for client to join

2. **Start Client (Built Executable)**:
   - Run CardGame.exe
   - Enter "127.0.0.1" in IP field
   - Click "Join as Client"
   - Should see "Player{ID} joined the game" on BoardUI

3. **Start Game**:
   - On Host (Unity Editor):
     - Click "Start Game" button
     - Event log should show "{GameType} started!"
     - Both players should receive cards

4. **Play War**:
   - Wait for "YOUR TURN" indicator on client
   - Click "Flip Card" button
   - Host should see card played in event log
   - Host's turn to flip card
   - Winner is determined and shown in event log
   - Repeat until game ends

5. **Play Go Fish**:
   - Current player:
     - Select target player from dropdown
     - Select target rank (e.g., "Ace")
     - Click "Ask for Cards"
   - If target has the rank: cards are transferred
   - If not: "Go Fish!" message appears, player draws
   - When 4 of a kind collected: "made a book" message

6. **Play Hearts**:
   - Current player:
     - Click a card in your hand to select it
     - Card highlights yellow
     - Click "Play Selected Card"
   - Server validates suit rules
   - When trick complete: winner shown, points awarded
   - Game continues until all cards played

---

### Network Testing (LAN - Multiple Computers)

**Setup:**
1. Build game on Computer A (host)
2. Copy build to Computer B and C (clients)
3. Ensure all computers on same network

**Host Computer (A):**
```
1. Run CardGame.exe
2. Click "Start as Host"
3. Open Command Prompt (Windows) or Terminal (Mac/Linux)
4. Type: ipconfig (Windows) or ifconfig (Mac/Linux)
5. Note your local IP (e.g., 192.168.1.100)
6. Share this IP with client computers
```

**Client Computers (B, C):**
```
1. Run CardGame.exe
2. Enter host IP address (e.g., 192.168.1.100)
3. Click "Join as Client"
4. Should connect and be assigned a seat
```

**Play Testing:**
- Follow same game testing steps as local testing
- Monitor event log for connection status
- Test disconnection/reconnection scenarios

---

### Test Scenarios Checklist

#### Connectivity Tests
- [ ] Host starts successfully
- [ ] Client connects to host
- [ ] Multiple clients connect (up to 4 players)
- [ ] Player names appear in BoardUI player list
- [ ] Client disconnect is handled gracefully
- [ ] Client reconnect works

#### War Game Tests
- [ ] Cards are dealt evenly
- [ ] Turn indicator shows correct player
- [ ] Flip card action works
- [ ] Highest card wins the round
- [ ] Winner collects all cards
- [ ] Score updates correctly
- [ ] Game ends when player runs out of cards
- [ ] Winner is declared

#### Go Fish Tests
- [ ] 5 cards dealt to each player
- [ ] Target player dropdown populates correctly
- [ ] Ask action transfers cards when target has rank
- [ ] "Go Fish" draws card when target doesn't have rank
- [ ] Books (4 of a kind) are detected and scored
- [ ] Game ends when all books made or cards depleted
- [ ] Correct winner declared

#### Hearts Tests
- [ ] 13 cards dealt to each player (4 players)
- [ ] Player with 2 of Clubs leads first trick
- [ ] Must follow suit when possible
- [ ] Highest card of lead suit wins trick
- [ ] Hearts = 1 point, Queen of Spades = 13 points
- [ ] Scores update after each trick
- [ ] Game ends after all cards played
- [ ] Lowest score wins

#### UI Tests
- [ ] Event log displays all game events
- [ ] Event log auto-scrolls to bottom
- [ ] Player list shows all connected players
- [ ] Score displays update in real-time
- [ ] Hand count updates correctly
- [ ] Turn indicator shows current player
- [ ] Action buttons enable/disable based on turn
- [ ] Card selection highlights correctly
- [ ] Host controls only visible to host

---

## 🐛 Troubleshooting

### Issue: "NetworkManager not found"
**Solution**: Ensure NetworkManager GameObject exists in scene with NetworkManager component attached.

### Issue: "Transport not set"
**Solution**:
1. Select NetworkManager GameObject
2. Find NetworkManager component
3. Drag UnityTransport component to "Transport" field

### Issue: "Clients can't connect"
**Solution**:
1. Check firewall settings (allow Unity/game through firewall)
2. Verify IP address is correct
3. Ensure UnityTransport port (7777) is not blocked
4. Try localhost (127.0.0.1) first to rule out network issues

### Issue: "Cards not displaying"
**Solution**:
1. Check CardUIPrefab is assigned in ControllerUI
2. Verify HandContainer has Horizontal Layout Group
3. Check console for errors about missing TextMeshPro

### Issue: "Game won't start"
**Solution**:
1. Ensure at least 2 players are connected
2. Check that GameType dropdown has a game selected (not "Select Game...")
3. Verify CardGameManager is attached to NetworkManager GameObject

### Issue: "Actions not working"
**Solution**:
1. Check it's the player's turn (turn indicator should say "YOUR TURN")
2. Verify action buttons are interactable (not grayed out)
3. For Hearts: ensure a card is selected before playing
4. Check console for validation error messages

---

## 🔌 Script Attachment Reference

### GameObject: NetworkManager
```
Components:
├── NetworkManager (Unity component)
├── UnityTransport (Unity component)
├── CardGameManager (Custom script)
├── NetworkGameManager (Custom script)
└── NetworkObject (Unity component)
```

### GameObject: BoardCanvas
```
Components:
└── BoardUI (Custom script)
    └── References all child UI elements
```

### GameObject: ControllerCanvas
```
Components:
└── ControllerUI (Custom script)
    └── References all child UI elements
```

### GameObject: NetworkCanvas (Optional)
```
Components:
└── NetworkUI (Custom script)
    └── References connection UI elements
```

---

## 📝 Button → Method Hookups

All button connections are handled in the UI scripts' `Start()` methods via code:

### BoardUI.cs
- `startGameButton.onClick` → `OnStartGameClicked()`

### ControllerUI.cs
- `flipCardButton.onClick` → `OnFlipCardClicked()`
- `askButton.onClick` → `OnAskClicked()`
- `drawButton.onClick` → `OnDrawClicked()`
- `playCardButton.onClick` → `OnPlayCardClicked()`
- Card UI elements → `OnCardClicked(Card card)`

### NetworkUI.cs
- `hostButton.onClick` → `OnHostClicked()`
- `clientButton.onClick` → `OnClientClicked()`
- `serverButton.onClick` → `OnServerClicked()`

**No manual hookup required** - buttons are connected automatically via Inspector references.

---

## 🚀 Quick Start Summary

1. ✅ **Install Packages**: Netcode for GameObjects + Unity Transport
2. ✅ **Create NetworkManager**: Add all manager scripts
3. ✅ **Create BoardUI**: Host controls and game display
4. ✅ **Create ControllerUI**: Player hand and actions
5. ✅ **Create NetworkUI**: Connection menu (optional)
6. ✅ **Assign References**: Link all UI elements in Inspector
7. ✅ **Build & Test**: Create build for multi-instance testing
8. ✅ **Play Games**: Test War, Go Fish, and Hearts

---

## 📚 Additional Notes

### Extending to New Games

To add a new card game:

1. **Create Rules Class** in `Assets/Scripts/Games/GameRules.cs`:
   ```csharp
   public class MyNewGameRules : IGameRules
   {
       // Implement all interface methods
   }
   ```

2. **Add to GameType Enum** in `GameTypes.cs`:
   ```csharp
   public enum GameType
   {
       None = 0,
       War = 1,
       GoFish = 2,
       Hearts = 3,
       MyNewGame = 4  // Add here
   }
   ```

3. **Update CardGameManager** `CreateGameRules()`:
   ```csharp
   private IGameRules CreateGameRules(GameType gameType)
   {
       return gameType switch
       {
           GameType.War => new WarRules(),
           GameType.GoFish => new GoFishRules(),
           GameType.Hearts => new HeartsRules(),
           GameType.MyNewGame => new MyNewGameRules(),  // Add here
           _ => null
       };
   }
   ```

4. **Add UI Support**: Create action panel in ControllerUI if needed

### Performance Tips

- **Limit Event Log**: Max 15 lines (configurable in BoardUI)
- **Card Pooling**: For large hands, consider object pooling for CardUI elements
- **Network Bandwidth**: Current implementation is optimized for small payloads
- **Tick Rate**: Default Netcode settings (30 ticks/sec) work well for turn-based games

### Security Considerations

- **Server Authority**: All game logic runs on server - clients only send requests
- **Validation**: Server validates all actions before processing
- **Cheating Prevention**: Clients never see other players' cards
- **Deterministic Shuffle**: Using seed ensures same deck order for validation

---

## ✅ Final Checklist

Before deploying:

- [ ] All packages installed (Netcode + Transport + TMP)
- [ ] NetworkManager configured with Transport
- [ ] All UI canvases created and scripts attached
- [ ] All Inspector references assigned (no missing references)
- [ ] Prefabs created (PlayerInfoPrefab, CardUIPrefab)
- [ ] Built executable tested with Editor as host
- [ ] All three games tested and working
- [ ] Network functionality verified on LAN
- [ ] Event log displaying messages
- [ ] Score tracking working correctly
- [ ] Turn indicators functioning
- [ ] Player disconnection handled gracefully

---

## 📞 Support

If you encounter issues:

1. **Check Console**: Look for error messages in Unity Console
2. **Debug Logs**: All scripts include detailed Debug.Log statements
3. **Netcode Debugger**: Window → Multiplayer → Netcode Debugger (shows network state)
4. **Profiler**: Window → Analysis → Profiler (check for performance issues)

---

**Happy Gaming! 🎮🃏**
