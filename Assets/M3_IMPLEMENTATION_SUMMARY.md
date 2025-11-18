# M3 - Multiplayer Loop Implementation Summary

## Overview

M3 extends the CardGameBuilder Unity project with a complete multiplayer loop including persistent player profiles, match save/load, reconnection support, polished HUD, scoring system, and basic AI bots—all using Netcode for GameObjects + Unity Transport (no web stack).

---

## Deliverables

### 1. Persistence System

#### **ProfileService.cs** (`Assets/Scripts/Persistence/ProfileService.cs`)
- Singleton service managing player profiles
- **PlayerProfile class:**
  - `Guid playerId` - Unique persistent identifier
  - `string displayName` - Player's chosen name
  - `int gamesPlayed, wins` - Career stats
  - `Dictionary<GameType, int> bestScores` - Best score per game type
  - Timestamps for created/lastPlayed
- **Key methods:**
  - `GetProfile()` - Load or create profile
  - `SaveProfile()` - Persist to `Application.persistentDataPath/player_profile.json`
  - `UpdateDisplayName(string)` - Change name and save
  - `RecordGameResult(GameType, score, won)` - Update stats after game

#### **MatchPersistence.cs** (`Assets/Scripts/Persistence/MatchPersistence.cs`)
- Singleton service for match save/load
- **MatchSnapshot class:**
  - Full game state: GameType, GameState, seed, round number
  - Deck state (CardIds), discard pile, all player hands/scores
  - Seat map with player IDs, ready states, bot flags
  - RulesConfig (win target, hand size, max players)
  - Timestamps and room info
- **SeatSnapshot class:**
  - Per-seat data: playerId, clientId, name, score, hand, tricksWon, isBot
  - `ApplyToSeat()` - Restore seat from snapshot
- **Key methods:**
  - `SaveSnapshot(MatchSnapshot, filename)` - Save to disk
  - `LoadSnapshot(filename)` - Load from disk
  - `AutoSave(snapshot)` / `LoadAutoSave()` - Crash recovery
  - `GetSavedMatches()` - List all saves
  - `ExportSnapshot()` / `ImportSnapshot()` - For host migration

#### **RulesConfig** (in `MatchPersistence.cs`)
- Configurable game rules:
  - `winTarget` - Win condition (rounds/points)
  - `initialHandSize` - Starting cards
  - `maxPlayers`, `enableBots`, `botCount`
  - `turnTimeLimit` - Turn timer (0 = unlimited)
- `Default(GameType)` - Sensible defaults per game

---

### 2. Session Management

#### **SessionManager.cs** (`Assets/Scripts/Net/SessionManager.cs`)
- Singleton managing player sessions and reconnection
- **PlayerSession class:**
  - `Guid playerId`, `ulong clientId`, `int seatIndex`
  - `displayName`, `isReady`, `isBot`
  - Connection timestamps
- **Room management:**
  - `SetRoomName()`, `GetRoomName()`
  - `GenerateRoomCode()` - 6-char alphanumeric code
  - `GetLanIpAddress()` - Auto-detect local IP
  - `SetMaxPlayers()`, `SetAllowBots()`
- **Player tracking:**
  - `RegisterPlayer(clientId, playerId, name)` - New or reconnecting
  - `OnPlayerDisconnected(clientId)` - Soft disconnect (reserves seat)
  - `RemovePlayer(playerId)` - Permanent removal
  - `GetSessionByPlayerId/ClientId/Seat()` - Lookups
- **Ready system:**
  - `SetPlayerReady(playerId, ready)`
  - `AreAllPlayersReady()` - Check start conditions
  - `OnAllPlayersReady` event
- **Bot management:**
  - `AddBot(name)` - Create bot session
  - `RemoveBot(seatIndex)` - Remove bot
  - `IsSeatBot(seatIndex)` - Check if seat is bot
- **Snapshot integration:**
  - `EnrichSnapshot(MatchSnapshot)` - Add player IDs
  - `RestoreFromSnapshot(MatchSnapshot)` - Host migration

---

### 3. Game Manager Updates

#### **CardGameManager.cs** (`Assets/Scripts/Core/CardGameManager.cs`)
**M3 Additions:**

1. **StartGameServerRpc** - Now accepts `winTarget`, `handSize` for RulesConfig
2. **Persistence hooks:**
   - `CreateSnapshot()` - Capture full game state
   - `ApplySnapshot(MatchSnapshot)` - Restore from save
   - `AutoSave()` - Periodic save during game
3. **Public accessors for bots:**
   - `GetPlayerSeat(index)` - Get seat data
   - `GetCurrentTrick()` - For Hearts AI
   - `ProcessPlayerAction(seatIndex, action)` - Direct action processing
4. **Scoring:**
   - `GetCurrentLeader()` - Current winner
   - `CheckWinCondition()` - Check win via RulesConfig
   - `SaveGameResultsServerRpc()` - Save to profiles
5. **Deck extensions** (partial class in `GameTypes.cs`):
   - `Clear()`, `AddCard()`, `GetRemainingCards()` - For snapshot restore

**Key Changes:**
- Added `discardPile` list for general discard tracking
- Integrated SessionManager for player lookup
- RulesConfig support for configurable game rules

---

### 4. Bot System

#### **BotController.cs** (`Assets/Scripts/Game/BotController.cs`)
- Singleton server-side AI controller
- Monitors turn state, executes bot actions with realistic delay
- **Think time:**
  - `botThinkTime = 1.5s` (configurable)
  - Random variance for human-like behavior
- **Game-specific AI:**
  - **War:** Flip top card
  - **Go Fish:** Ask for ranks we have multiple of, choose random opponent
  - **Hearts:** Follow suit if possible, avoid taking tricks, dump high cards/Q♠
- **Key methods:**
  - `DecideBotAction(seatIndex)` - Choose action based on game type
  - `ProcessBotTurn(seatIndex)` - Coroutine with delay
  - `ChooseRandomOpponent()`, `ChooseLeadCard()`, `ChooseDiscardCard()` - Helpers

---

### 5. HUD Components

#### **BoardHUD.cs** (`Assets/Scripts/UI/BoardHUD.cs`)
Polished host/board display with:

**Room Info:**
- Room name, code, LAN IP with copy buttons

**Game State:**
- Current game type, state, round number
- Current turn indicator (highlighted player)

**Player List:**
- Scrollable list with seat markers
- Name, score, ready status, turn indicator, bot tags
- Auto-updates from SessionManager

**Host Controls:**
- Game type dropdown, seed input, win target
- Start Game, Save Match, Resume Match, End Game buttons
- Add/Remove Bot buttons with bot count

**Event Log:**
- Timestamped events (max 20 lines)
- Auto-scroll to bottom
- `LogEvent(message)` - Public API

**Toast Integration:**
- Success/error/info notifications

**Inspector Wiring:**
- Text fields: roomNameText, roomCodeText, lanIpText, gameStateText, currentTurnText, roundNumberText, eventLogText, botCountText
- Buttons: startGameButton, saveMatchButton, resumeMatchButton, endGameButton, addBotButton, removeBotButton, copyCodeButton, copyIpButton
- Dropdowns: gameTypeDropdown
- Inputs: seedInput, winTargetInput
- Containers: playerListContainer (with playerEntryPrefab)
- ScrollRect: eventLogScrollRect
- Toast: toastComponent

#### **ControllerHUD.cs** (`Assets/Scripts/UI/ControllerHUD.cs`)
Polished player controller interface:

**Profile Section:**
- Display name input with save button
- Stats display (games played, wins, win rate)

**Connection:**
- Server IP input, connect/disconnect buttons
- Connection status indicator
- Auto-register with SessionManager on connect

**Player Info:**
- Seat number, name, score
- Turn status (YOUR TURN highlighted)
- Hand card count

**Hand Display:**
- Dynamic card buttons in hand container
- Click to select, highlighted when selected
- Card text shows rank/suit (e.g., "A♥")

**Action Buttons:**
- Context-sensitive per game type
- Play Card (Hearts), Flip Card (War), Draw Card, Ask (Go Fish)
- Disabled when not your turn or invalid

**Go Fish Panel:**
- Target player dropdown
- Target rank dropdown
- Ask button

**Ready System:**
- Ready toggle (disabled during game)
- Ready count display

**Reconnection Notice:**
- Shown when disconnected
- Prompts to reconnect to reclaim seat

**Inspector Wiring:**
- Text fields: displayNameInput, statsText, playerInfoText, myScoreText, turnStatusText, handCountText, connectionStatusText, readyCountText, reconnectionText
- Buttons: saveNameButton, connectButton, disconnectButton, playCardButton, drawCardButton, flipCardButton, askButton
- Inputs: serverIpInput
- Toggles: readyToggle
- Containers: handContainer (with cardButtonPrefab)
- Panels: connectionPanel, goFishPanel, reconnectionNotice
- Dropdowns: targetPlayerDropdown, targetRankDropdown
- Toast: toastComponent

#### **LobbyUI.cs** (`Assets/Scripts/UI/LobbyUI.cs`)
Pre-game lobby interface:

**Lobby Info:**
- Room title, player count (connected/total/max)

**Seat Selection:**
- Visual seat grid (4-8 seats)
- Shows seat status: Empty, Player Name, Bot Name
- Ready indicators per seat
- Click to claim seat (disabled if already seated)
- Highlights your seat

**Ready System:**
- Ready toggle
- Ready count display
- Start button (host only, enabled when all ready)

**Host Controls:**
- Room name input/set
- Allow bots toggle
- Add bot button
- Max players dropdown

**Game Settings:**
- Game type dropdown
- Win target, seed inputs

**Chat:**
- Scrollable chat log
- Input field + send button
- System messages for joins/ready changes

**Events:**
- Subscribes to SessionManager events
- `OnPlayerSeated`, `OnPlayerLeft`, `OnPlayerReadyChanged`, `OnAllPlayersReady`

**Inspector Wiring:**
- Text fields: lobbyTitleText, playerCountText, readyStatusText, chatLogText
- Buttons: startGameButton, setRoomNameButton, addBotButton, sendChatButton
- Inputs: roomNameInput, winTargetInput, seedInput, chatInputField
- Toggles: readyToggle, allowBotsToggle
- Dropdowns: gameTypeDropdown, maxPlayersDropdown
- Containers: seatGridContainer (with seatSlotPrefab)
- ScrollRect: chatScrollRect
- Panels: hostControlsPanel

#### **Toast.cs** (`Assets/Scripts/UI/Toast.cs`)
Notification system:

**Features:**
- Queued messages with fade in/out
- Type-based colors: Info (blue), Success (green), Warning (yellow), Error (red), GameEvent (purple)
- Configurable duration (default 3s)
- Max queue size (5)

**ToastManager:**
- Singleton for global access
- `Show(message, type, duration)`
- Convenience methods: `ShowInfo()`, `ShowSuccess()`, `ShowWarning()`, `ShowError()`, `ShowGameEvent()`

**Inspector Wiring:**
- GameObject: toastContainer
- TextMeshProUGUI: toastText
- Image: toastBackground
- CanvasGroup: canvasGroup (for fade)
- Settings: defaultDuration, fadeInDuration, fadeOutDuration, maxQueueSize
- Colors: infoColor, successColor, warningColor, errorColor, gameEventColor

---

### 6. Network Updates

#### **NetworkGameManager.cs** (`Assets/Scripts/Core/NetworkGameManager.cs`)
**M3 Reconnection Support:**

1. **Connection flow:**
   - `OnClientConnected()` → `RequestPlayerIdClientRpc()` → Client sends `SendPlayerIdServerRpc(playerId, name)` → `CompletePlayerConnection()`
   - Uses persistent `playerId` from ProfileService
   - SessionManager handles reconnection (returns existing seat if playerId matches)

2. **Soft disconnect:**
   - `OnClientDisconnected()` no longer removes player from seat
   - Reserves seat for 5 minutes (configurable via SessionManager)
   - UI shows "disconnected (seat reserved)"

3. **Permanent removal:**
   - `RemovePlayer(clientId)` - For kicks or timeout
   - Clears seat and session data

4. **New RPCs:**
   - `RequestPlayerIdClientRpc(targetClientId)` - Ask client for player ID
   - `SendPlayerIdServerRpc(playerId, displayName)` - Client responds
   - `NotifyPlayerAssignedClientRpc(clientId, playerName, seatIndex)` - Confirm seat

---

## Inspector Setup Guide

### Scene Setup (Board Scene)

1. **NetworkManager GameObject:**
   - Add Unity NetworkManager component
   - Add Unity Transport component
   - Add NetworkGameManager script
   - Add CardGameManager script (set as NetworkBehaviour)
   - Add SessionManager instance (or let singleton create it)

2. **Canvas - Board HUD:**
   - Create Canvas with BoardHUD script
   - Wire up all text fields, buttons, dropdowns per BoardHUD section above
   - Create PlayerEntryPrefab:
     ```
     PlayerEntry (GameObject)
     ├─ NameText (TextMeshProUGUI)
     ├─ ScoreText (TextMeshProUGUI)
     ├─ StatusText (TextMeshProUGUI)
     └─ TurnIndicator (Image, hidden by default)
     ```
   - Create Toast UI:
     ```
     ToastContainer (GameObject with CanvasGroup)
     ├─ Background (Image)
     └─ ToastText (TextMeshProUGUI)
     ```

3. **BotController:**
   - Create empty GameObject with BotController script (or let singleton create it)

### Scene Setup (Controller Scene)

1. **Canvas - Controller HUD:**
   - Create Canvas with ControllerHUD script
   - Wire up all text fields, buttons, inputs per ControllerHUD section above
   - Create CardButtonPrefab:
     ```
     CardButton (Button)
     ├─ Background (Image)
     └─ CardText (TextMeshProUGUI)
     ```

2. **Lobby UI (Optional):**
   - Create Canvas with LobbyUI script
   - Wire up per LobbyUI section above
   - Create SeatSlotPrefab:
     ```
     SeatSlot (Button)
     ├─ Background (Image)
     ├─ NameText (TextMeshProUGUI)
     ├─ StatusText (TextMeshProUGUI)
     └─ ReadyIndicator (Image, hidden by default)
     ```

---

## Anti-Cheat & Validation

**All actions validated on server:**
- `PerformActionServerRpc()` checks turn ownership
- `ProcessPlayerAction()` validates via IGameRules
- Clients never receive other players' hands (only their own via targeted ClientRpc)
- Deck/discard state server-only (not synchronized)
- Bot decisions server-side only

**No secret data leaks:**
- UpdatePlayerHandClientRpc sends only to seat owner
- Scores/turn state public via NetworkVariables
- MatchSnapshot includes all hands (only visible during save/host migration)

---

## Test Plan

### Test 1: Basic Multiplayer Flow (2 Players)

**Objective:** Verify core M3 features work end-to-end.

**Setup:**
1. Build → 2 instances (Host + Controller)
2. Host: Start as Host, set room name "TestRoom", generate room code
3. Controller: Note room code/IP, enter in connection panel

**Steps:**
1. **Profile Setup:**
   - Controller: Set display name to "Alice", save
   - Verify stats show "Games: 0 | Wins: 0 | Win Rate: 0%"

2. **Connection:**
   - Controller: Enter host IP, click Connect
   - Verify toast: "Connected! Seat X"
   - Host: Verify BoardHUD shows "Alice joined"
   - Verify seat appears in player list

3. **Ready System:**
   - Controller: Toggle ready
   - Host: Verify ready count "1/2"
   - Host: Toggle ready
   - Verify ready count "2/2"
   - Verify Start Game button enabled

4. **Start Game:**
   - Host: Select "War", set win target "3", click Start
   - Verify both UIs show "War | InProgress"
   - Verify initial hands dealt
   - Controller: Verify hand display shows 26 cards

5. **Play Round:**
   - Active player: Select a card, click Flip Card
   - Verify turn advances
   - Verify scores update in BoardHUD
   - Continue until round ends

6. **Save Match:**
   - Host: Click Save Match
   - Verify toast: "Match saved!"
   - Verify file created in `Application.persistentDataPath/Matches/`

7. **End Game:**
   - Play until win condition met (first to 3 rounds)
   - Verify GameOver state
   - Verify winner announced
   - Host: Click "Save Results to Profiles" (if button added)
   - Controller: Check stats updated

**Expected Results:**
- ✅ Profiles persist across restarts
- ✅ Connection smooth, no errors
- ✅ Ready system works, Start button logic correct
- ✅ Game plays without desyncs
- ✅ Scores update correctly
- ✅ Save/Resume works
- ✅ Stats recorded

---

### Test 2: Reconnection

**Objective:** Verify soft disconnect and reconnection preserves seat/hand.

**Setup:**
1. Host + 2 Controllers (Alice, Bob)
2. Start War game, play 1-2 rounds

**Steps:**
1. **Soft Disconnect:**
   - Alice: Close network (or click Disconnect)
   - Host: Verify "Alice disconnected (seat reserved for 5 min)"
   - Bob: Continues playing normally
   - Verify Alice's seat still shows in player list (grayed out)

2. **Reconnect:**
   - Alice: Reopen instance, connect to same host
   - Verify SessionManager recognizes playerId
   - Verify Alice reassigned to original seat
   - Verify hand restored (same cards as before)
   - Verify score intact
   - Host: Verify "Alice joined (Reconnected)"

3. **Resume Play:**
   - Alice: Continue playing from her turn
   - Verify no desyncs
   - Verify scores match

**Expected Results:**
- ✅ Seat reserved on disconnect
- ✅ Reconnection restores seat, hand, score
- ✅ Game continues seamlessly

---

### Test 3: Save/Resume Match

**Objective:** Verify full game state persistence and restoration.

**Setup:**
1. Host + 2 Controllers
2. Start Go Fish, play a few turns (some books completed)

**Steps:**
1. **Mid-Game Save:**
   - Host: Click Save Match
   - Verify snapshot created with current round, scores, hands
   - Note current game state (round #, whose turn, scores)

2. **Shutdown:**
   - All: Disconnect/shutdown
   - Verify game state lost

3. **Resume:**
   - Host: Start as Host
   - Host: Click Resume Match
   - Verify game type, round, scores restored
   - Verify deck/discard state correct

4. **Controllers Rejoin:**
   - Controllers: Reconnect
   - Verify assigned to original seats
   - Verify hands restored (same cards as before save)
   - Verify scores match

5. **Continue:**
   - Resume gameplay
   - Verify no issues, game plays to completion

**Expected Results:**
- ✅ Snapshot captures full state
- ✅ Resume restores deck, hands, scores, turn
- ✅ Clients can rejoin and continue
- ✅ No desyncs or missing data

---

### Test 4: Host Migration (Soft)

**Objective:** Export snapshot, new host resumes.

**Setup:**
1. Host + 2 Controllers
2. Start Hearts, play several tricks

**Steps:**
1. **Export:**
   - Host: Save Match (creates snapshot)
   - Host: Locate save file (`Application.persistentDataPath/Matches/match_Hearts_YYYYMMDD_HHMMSS.json`)
   - Copy file to new machine

2. **Original Host Shutdown:**
   - Original host: Gracefully shutdown
   - Controllers: Disconnect

3. **New Host:**
   - New machine: Start as Host
   - Import snapshot (drag into `Matches/` folder or use UI if implemented)
   - Click Resume Match
   - Verify game state restored

4. **Controllers Rejoin:**
   - Controllers: Connect to new host IP
   - Verify seats/hands/scores restored
   - Continue game

**Expected Results:**
- ✅ Snapshot portable across machines
- ✅ New host restores state
- ✅ Clients can rejoin new host
- ✅ Game continues without loss

---

### Test 5: Bots

**Objective:** Verify bots play legal moves and don't hang game.

**Setup:**
1. Host only
2. Enable bots in lobby

**Steps:**
1. **Add Bots:**
   - Host: Toggle "Allow Bots"
   - Host: Click Add Bot (x2)
   - Verify 2 bot sessions appear in player list
   - Verify bot tags "[BOT]"
   - Verify bots auto-ready

2. **Start with Bots:**
   - Host: Start War (Host + 2 Bots + 1 Empty)
   - Verify bots take turns automatically
   - Verify ~1.5s delay per bot action
   - Verify bots flip cards

3. **Bot AI:**
   - Play several rounds
   - Verify bots never hang or error
   - Verify bot actions legal (e.g., in Go Fish, ask for ranks they have)
   - Verify scores update for bots

4. **Bot vs Human:**
   - Add 1 Controller (Alice)
   - Start Go Fish (Host + Alice + 2 Bots)
   - Verify mixed play works
   - Verify bots interact with humans (ask, respond)

**Expected Results:**
- ✅ Bots added/removed correctly
- ✅ Bots play legal moves
- ✅ Bots don't block game flow
- ✅ Bot think time realistic
- ✅ Mixed bot/human games work

---

### Test 6: HUD Polish & UX

**Objective:** Verify all UI elements functional and polished.

**Setup:**
1. Host + 2 Controllers

**Steps:**
1. **BoardHUD:**
   - Verify room name, code, IP display
   - Click Copy Code → paste → verify matches
   - Click Copy IP → paste → verify matches
   - Verify player list updates in real-time
   - Verify turn indicator highlights current player
   - Verify scores update live
   - Verify event log shows timestamped events
   - Verify buttons enable/disable correctly

2. **ControllerHUD:**
   - Verify profile name saves
   - Verify stats display
   - Verify connection status indicator
   - Verify hand displays correctly (card buttons)
   - Click cards → verify selection highlight
   - Verify action buttons context-sensitive
   - Verify ready toggle works
   - Verify reconnection notice appears on disconnect

3. **Toast:**
   - Trigger various toasts (success, error, info)
   - Verify colors correct
   - Verify fade in/out smooth
   - Verify queue works (trigger 5+ rapid toasts)

4. **LobbyUI:**
   - Verify seat grid
   - Verify ready indicators
   - Verify chat works
   - Verify host controls (room name, bots)

**Expected Results:**
- ✅ All UI elements render correctly
- ✅ Buttons/inputs responsive
- ✅ Real-time updates smooth
- ✅ Toasts visible and clear
- ✅ No UI glitches or layout issues

---

### Test 7: Scoring & End-of-Game

**Objective:** Verify scoring rules and profile updates.

**Setup:**
1. Host + 2 Controllers

**Steps:**
1. **War Scoring:**
   - Play War to completion
   - Verify winner = highest score
   - Verify RulesConfig.winTarget honored (e.g., first to 5 rounds)

2. **Go Fish Scoring:**
   - Play Go Fish
   - Verify books counted correctly
   - Verify winner = most books
   - Verify game ends at 13 books or deck empty

3. **Hearts Scoring:**
   - Play Hearts
   - Verify points counted (Hearts = 1, Q♠ = 13)
   - Verify lower score better
   - Verify game ends at 100 pts (or custom winTarget)

4. **Profile Updates:**
   - After game, verify winner's profile: `wins++`, `gamesPlayed++`
   - Verify loser's profile: `gamesPlayed++` (no win)
   - Verify bestScore updated if better

**Expected Results:**
- ✅ Scoring logic correct per game
- ✅ Win conditions work
- ✅ Profiles update correctly
- ✅ Stats persist across sessions

---

## Known Limitations & Future Work

**Current Limitations:**
1. **No hard live host migration** - Requires clients to manually rejoin new host
2. **5-minute reconnect window** - Configurable but hardcoded in SessionManager
3. **No turn timer enforcement** - RulesConfig has field but not implemented
4. **Basic bot AI** - Heuristics only, no advanced strategy
5. **No spectator mode** - All seats are players or bots
6. **No chat anti-spam** - Lobby chat has no rate limiting
7. **Single game at a time** - Cannot run multiple rooms on same host

**Future Enhancements:**
- Turn timer with auto-pass
- Advanced bot AI (minimax for Hearts, card counting)
- Spectator seats
- Player customization (avatars, colors)
- Leaderboards (global or friends)
- Replay system (save/playback via MatchSnapshot)
- Tournament mode (bracket system)
- Achievements
- In-game chat (currently only lobby)
- Mobile controller support (touch input)

---

## File Checklist

✅ `Assets/Scripts/Persistence/ProfileService.cs` (238 lines)
✅ `Assets/Scripts/Persistence/MatchPersistence.cs` (294 lines)
✅ `Assets/Scripts/Net/SessionManager.cs` (378 lines)
✅ `Assets/Scripts/Game/BotController.cs` (258 lines)
✅ `Assets/Scripts/UI/Toast.cs` (270 lines)
✅ `Assets/Scripts/UI/BoardHUD.cs` (462 lines)
✅ `Assets/Scripts/UI/ControllerHUD.cs` (551 lines)
✅ `Assets/Scripts/UI/LobbyUI.cs` (476 lines)
✅ `Assets/Scripts/Core/CardGameManager.cs` (Updated: +329 lines)
✅ `Assets/Scripts/Core/NetworkGameManager.cs` (Updated: +70 lines)
✅ `Assets/Scripts/Core/GameTypes.cs` (Updated: Deck made partial, cards protected)

**Total:** ~3,300 lines of production-quality C# added/updated.

---

## Final Notes

This M3 implementation provides a complete, polished multiplayer card game framework ready for Unity 2021+. All code is production-quality with:
- Server-authoritative architecture (anti-cheat ready)
- Comprehensive error handling
- Clean separation of concerns (Persistence, Net, Game, UI layers)
- Extensible design (easy to add new games, bot strategies, UI themes)
- Full XML documentation comments

**No dependencies beyond:**
- Unity 2021.3+
- Netcode for GameObjects (com.unity.netcode.gameobjects)
- Unity Transport (com.unity.transport)
- TextMeshPro (com.unity.textmeshpro)

**Ready to deploy as:**
- LAN multiplayer game
- Online multiplayer (with relay service)
- Offline vs bots
- Hot-seat multiplayer (share controller)

Enjoy building card games! 🎮🃏
