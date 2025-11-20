# CardGameBuilder - Setup & Testing Guide

## Overview
This guide provides complete instructions for setting up the Unity scenes and testing the LAN multiplayer functionality.

---

## Prerequisites

1. **Unity Version**: Unity 2021.3 LTS or newer
2. **Packages Required**:
   - Netcode for GameObjects (com.unity.netcode.gameobjects)
   - Unity Transport (com.unity.transport)
   - TextMeshPro (com.unity.textmeshpro)

### Installing Packages

1. Open Unity Package Manager (Window → Package Manager)
2. Click the "+" button and select "Add package by name..."
3. Add these packages:
   ```
   com.unity.netcode.gameobjects
   com.unity.transport
   ```
4. TextMeshPro will prompt to import TMP Essentials on first use - click "Import"

---

## Scene Wiring Checklist

### 1. Board Scene Setup (Host/Server Scene)

#### A. Create the Board Scene
1. Create new scene: `File → New Scene → Basic (Built-in)`
2. Save as: `Assets/Scenes/BoardScene.unity`

#### B. Network Infrastructure
1. **Create NetworkManager GameObject**:
   - Right-click in Hierarchy → `Create Empty`
   - Rename to: `NetworkManager`
   - Add Component: `NetworkManager` (Unity.Netcode)
   - Add Component: `UnityTransport` (Unity.Netcode.Transports.UTP)

2. **Configure NetworkManager Component**:
   - Click `Select transport...` button
   - Choose: `UnityTransport`
   - Leave default settings (or customize as needed)

3. **Configure UnityTransport Component**:
   - Protocol Type: `UnityTransport`
   - Connection Data:
     - Address: `127.0.0.1` (localhost, will be overridden)
     - Port: `7777`
     - Server Listen Address: `0.0.0.0` (listens on all interfaces)

#### C. Network Game Manager
1. **Create NetworkGameManager GameObject**:
   - Right-click in Hierarchy → `Create Empty`
   - Rename to: `NetworkGameManager`
   - Add Component: `Network Object` (Unity.Netcode)
   - Add Component: `NetworkGameManager` (your script)

2. **Configure NetworkObject Component**:
   - Check: ☑ `Don't Destroy with Owner`
   - Uncheck: ☐ `Destroy with Scene`

3. **Add to NetworkManager Prefabs**:
   - Drag `NetworkGameManager` GameObject to Project to create prefab
   - In `NetworkManager` component:
     - Expand `NetworkPrefabs` list
     - Add the `NetworkGameManager` prefab to the list
   - Delete the `NetworkGameManager` from Hierarchy (it will spawn via network)
   - **ALTERNATIVE**: Keep in scene and check `Spawn with Scene` on NetworkObject

#### D. Board UI Setup
1. **Create Canvas**:
   - Right-click in Hierarchy → `UI → Canvas`
   - Rename to: `BoardCanvas`
   - Canvas settings:
     - Render Mode: `Screen Space - Overlay`
     - UI Scale Mode: `Scale with Screen Size`
     - Reference Resolution: `1920 x 1080`

2. **Add BoardUI Script**:
   - Select `BoardCanvas`
   - Add Component: `BoardUI` (your script)

3. **Create UI Elements** (all under BoardCanvas):

   **Network Controls Panel**:
   ```
   - Panel: "NetworkControlsPanel"
     - Button: "HostButton" (Text: "Host Server")
     - Button: "StopButton" (Text: "Stop Server")
     - TextMeshProUGUI: "StatusText" (Text: "Status: Not connected")
   ```

   **Game Display Panel**:
   ```
   - Panel: "GameDisplayPanel"
     - TextMeshProUGUI: "Seat0Text" (Text: "Seat 0: Empty")
     - TextMeshProUGUI: "Seat1Text" (Text: "Seat 1: Empty")
     - TextMeshProUGUI: "Seat2Text" (Text: "Seat 2: Empty")
     - TextMeshProUGUI: "Seat3Text" (Text: "Seat 3: Empty")
     - TextMeshProUGUI: "DeckCountText" (Text: "Deck: 0 cards")
     - TextMeshProUGUI: "DiscardCountText" (Text: "Discard: 0 cards")
     - TextMeshProUGUI: "TopDiscardCardText" (Text: "Top Card: None")
   ```

   **Info Panel**:
   ```
   - Panel: "InfoPanel"
     - TextMeshProUGUI: "InfoText" (Text: "Ready to start")
   ```

4. **Wire BoardUI Component**:
   - Select `BoardCanvas`
   - In `BoardUI` component, drag references:
     - `Host Button` → HostButton
     - `Stop Button` → StopButton
     - `Status Text` → StatusText
     - `Seat Texts` → Array of 4 seat TextMeshProUGUI elements
     - `Deck Count Text` → DeckCountText
     - `Discard Count Text` → DiscardCountText
     - `Top Discard Card Text` → TopDiscardCardText
     - `Info Text` → InfoText

---

### 2. Controller Scene Setup (Player Controller Scene)

#### A. Create the Controller Scene
1. Create new scene: `File → New Scene → Basic (Built-in)`
2. Save as: `Assets/Scenes/ControllerScene.unity`

#### B. Network Infrastructure (Same as Board Scene)
1. **Create NetworkManager GameObject**:
   - Right-click in Hierarchy → `Create Empty`
   - Rename to: `NetworkManager`
   - Add Component: `NetworkManager`
   - Add Component: `UnityTransport`

2. **Configure NetworkManager**: (Same settings as Board Scene)

3. **Configure UnityTransport**: (Same settings as Board Scene)

4. **Add NetworkGameManager Prefab**:
   - In `NetworkManager` component:
     - Expand `NetworkPrefabs` list
     - Add the `NetworkGameManager` prefab (same as Board Scene)
   - DO NOT instantiate NetworkGameManager in scene (clients receive it from server)

#### C. Controller UI Setup
1. **Create Canvas**:
   - Right-click in Hierarchy → `UI → Canvas`
   - Rename to: `ControllerCanvas`
   - Same settings as Board Scene

2. **Add ControllerUI Script**:
   - Select `ControllerCanvas`
   - Add Component: `ControllerUI` (your script)

3. **Create UI Elements** (all under ControllerCanvas):

   **Connection Panel**:
   ```
   - Panel: "ConnectionPanel"
     - TMP_InputField: "HostIPInputField" (Placeholder: "Enter host IP (e.g., 192.168.1.100)")
     - Button: "JoinButton" (Text: "Join Server")
     - Button: "LeaveButton" (Text: "Leave Server")
     - TextMeshProUGUI: "ConnectionStatusText" (Text: "Connection: Not connected")
   ```

   **Seat Controls Panel**:
   ```
   - Panel: "SeatControlsPanel"
     - TMP_InputField: "SeatInputField" (Placeholder: "Seat # (0-3)")
     - Button: "ClaimSeatButton" (Text: "Claim Seat")
     - Button: "LeaveSeatButton" (Text: "Leave Seat")
     - TextMeshProUGUI: "SeatStatusText" (Text: "Seat: No seat")
   ```

   **Hand Reorder Panel**:
   ```
   - Panel: "HandReorderPanel"
     - TMP_InputField: "FromIndexInputField" (Placeholder: "From index")
     - TMP_InputField: "ToIndexInputField" (Placeholder: "To index")
     - Button: "ReorderButton" (Text: "Reorder Hand")
   ```

   **Hand Display Panel**:
   ```
   - Panel: "HandDisplayPanel"
     - ScrollView: "HandScrollView"
       - Content: "HandContent"
         - TextMeshProUGUI: "HandDisplayText" (Text: "Hand: (empty)")
   ```

   **Info Panel**:
   ```
   - Panel: "InfoPanel"
     - TextMeshProUGUI: "InfoText" (Text: "Not connected")
   ```

4. **Wire ControllerUI Component**:
   - Select `ControllerCanvas`
   - In `ControllerUI` component, drag references:
     - `Host IP Input Field` → HostIPInputField
     - `Join Button` → JoinButton
     - `Leave Button` → LeaveButton
     - `Connection Status Text` → ConnectionStatusText
     - `Seat Input Field` → SeatInputField
     - `Claim Seat Button` → ClaimSeatButton
     - `Leave Seat Button` → LeaveSeatButton
     - `Seat Status Text` → SeatStatusText
     - `From Index Input Field` → FromIndexInputField
     - `To Index Input Field` → ToIndexInputField
     - `Reorder Button` → ReorderButton
     - `Hand Display Text` → HandDisplayText
     - `Info Text` → InfoText

---

## Build Settings

1. **Add Scenes to Build**:
   - Open `File → Build Settings`
   - Click `Add Open Scenes` (or drag scenes from Project)
   - Ensure both scenes are in this order:
     ```
     0. BoardScene
     1. ControllerScene
     ```

2. **Platform Settings**:
   - For Desktop testing: Select `PC, Mac & Linux Standalone`
   - For Mobile testing: Select `Android` or `iOS`

---

## LAN Testing Steps

### Step 1: Prepare the Host (Board)

1. **Open Board Scene**:
   - In Unity Editor, open `BoardScene.unity`

2. **Enter Play Mode**:
   - Press Play button in Unity Editor

3. **Start Host**:
   - Click the `Host Server` button in the Game view
   - The Status should change to "Hosting..."
   - Check Console for local IP address message:
     ```
     [BoardUI] Server started. Clients should connect to: 192.168.X.X
     ```
   - **Note this IP address** - clients will need it

4. **Verify Hosting**:
   - Status should show: "Connected - 52 cards in deck"
   - All 4 seats should show: "Seat X: EMPTY"
   - Deck should show: "Deck: 52 cards"

### Step 2: Connect a Controller (Client)

#### Option A: Using Unity Editor (Second Instance)

1. **Open Second Unity Editor Instance**:
   - On Windows: Duplicate the Unity project folder, open in new editor
   - On Mac: Use `open -n -a Unity`

2. **Open Controller Scene**:
   - In the second editor, open `ControllerScene.unity`

3. **Enter Play Mode**:
   - Press Play in the second editor

4. **Join Server**:
   - Enter the host IP (from Step 1.3) in the "Host IP" field
     - Example: `192.168.1.100`
     - For same machine: Use `127.0.0.1`
   - Click `Join Server` button
   - Connection status should change to "Connected"

#### Option B: Using Standalone Build

1. **Build Controller Application**:
   - Go to `File → Build Settings`
   - Ensure `ControllerScene` is checked (or set as default)
   - Click `Build` and save executable
   - Run the built executable

2. **Join from Built Application**:
   - Enter host IP in input field
   - Click `Join Server`

### Step 3: Claim a Seat and Test

1. **In Controller (Client)**:
   - Enter a seat number (0, 1, 2, or 3) in "Seat #" field
   - Click `Claim Seat` button

2. **Expected Results**:
   - **Controller**:
     - Seat status changes to "Seated"
     - Hand display shows 7 cards, e.g.:
       ```
       Hand:
       [0] A♠  [1] 5♥  [2] K♣  [3] 2♦
       [4] 9♠  [5] J♥  [6] 3♣
       ```
   - **Board**:
     - Corresponding seat shows: "Seat X: OCCUPIED" in green
     - Player name appears (e.g., "Player 1234567890")
     - Deck count decreases to 45 cards

3. **Test Hand Reordering**:
   - In Controller, enter:
     - From: `0`
     - To: `3`
   - Click `Reorder Hand`
   - The card at position [0] should move to position [3]
   - Hand display updates immediately

4. **Test Multiple Clients**:
   - Repeat Step 2-3 with additional controllers
   - Each should claim different seats (0-3)
   - Board should update to show all occupied seats

### Step 4: Test Disconnect/Reconnect

1. **Leave Seat**:
   - In Controller, click `Leave Seat`
   - Hand should clear: "Hand: (empty)"
   - Board seat should show "EMPTY" again
   - Cards return to discard pile

2. **Disconnect**:
   - In Controller, click `Leave Server`
   - Connection status: "Not connected"

3. **Reconnect**:
   - Enter IP again and click `Join Server`
   - Claim a different seat
   - Receive new 7-card hand

---

## Troubleshooting

### Connection Issues

**Problem**: Client can't connect to host
- **Solution**:
  - Verify both devices are on same LAN
  - Check firewall settings (allow port 7777)
  - Try `127.0.0.1` for same-machine testing
  - Ensure host IP is correct (not 0.0.0.0)

**Problem**: "NetworkManager not found"
- **Solution**:
  - Ensure NetworkManager GameObject exists in scene
  - Verify NetworkManager component is attached
  - Check NetworkManager has UnityTransport selected

### Gameplay Issues

**Problem**: No cards dealt when claiming seat
- **Solution**:
  - Check Console for errors
  - Verify NetworkGameManager is in NetworkPrefabs list
  - Ensure NetworkGameManager spawns (check server console)

**Problem**: Hand doesn't update on reorder
- **Solution**:
  - Verify indices are valid (0 to hand size - 1)
  - Check ControllerUI is subscribed to OnPrivateHandChanged
  - Look for error messages in Info panel

**Problem**: Board doesn't show seat updates
- **Solution**:
  - Verify BoardUI is subscribed to OnPublicStateChanged
  - Check NetworkGameManager.Instance is not null
  - Ensure UI text references are assigned in Inspector

---

## Network Architecture Summary

### Server Authority Model
- **Server (Host)**: Manages all game state
  - Owns the deck, discard pile, and all player hands
  - Validates all player actions (seat claims, hand reorders)
  - Broadcasts public state to all clients
  - Sends private hands to individual clients

### Client Responsibilities
- **Board Client**: Displays public game state only
- **Controller Clients**:
  - Send action requests to server via ServerRpc
  - Display only their own private hand via ClientRpc
  - Cannot see other players' cards

### RPC Flow
1. **Client → Server**: `ClaimSeatServerRpc(seatIndex)`
   - Server validates and assigns seat
   - Server deals 7 cards to player

2. **Server → All Clients**: `UpdatePublicStateClientRpc(state)`
   - All clients update seat displays, deck count, etc.

3. **Server → Specific Client**: `SendPrivateHandClientRpc(hand)`
   - Only target client receives their private cards
   - Uses `ClientRpcParams` for targeted delivery

---

## Quick Reference: Button → Method Mapping

### BoardUI
| Button | Method Called | Action |
|--------|---------------|--------|
| Host Server | `OnHostButtonClicked()` | `NetworkManager.StartHost()` |
| Stop Server | `OnStopButtonClicked()` | `NetworkManager.Shutdown()` |

### ControllerUI
| Button | Method Called | Action |
|--------|---------------|--------|
| Join Server | `OnJoinButtonClicked()` | `NetworkManager.StartClient()` |
| Leave Server | `OnLeaveButtonClicked()` | `NetworkManager.Shutdown()` |
| Claim Seat | `OnClaimSeatButtonClicked()` | `ClaimSeatServerRpc(seatIndex)` |
| Leave Seat | `OnLeaveSeatButtonClicked()` | `LeaveSeatServerRpc()` |
| Reorder Hand | `OnReorderButtonClicked()` | `ReorderHandServerRpc(from, to)` |

---

## Next Steps

After successful LAN testing:
1. Add card drag-and-drop for hand reordering
2. Implement card play/discard to shared discard pile
3. Add turn-based game logic
4. Create visual card sprites and animations
5. Add sound effects and polish

---

## Support

For issues or questions:
- Check Unity Console for error messages
- Review Netcode for GameObjects documentation: https://docs-multiplayer.unity3d.com/
- Verify all UI references are assigned in Inspector
- Ensure all required packages are installed

Happy coding!
