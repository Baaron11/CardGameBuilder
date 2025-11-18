# 🃏 Card Game Builder - Unity Card Engine

A server-authoritative, networked card game engine for Unity using Netcode for GameObjects. Supports multiple card game types with extensible rules system.

## 🎮 Features

- **Server-Authoritative Architecture**: All game logic runs on server, preventing cheating
- **Deterministic Gameplay**: Seed-based shuffling ensures reproducible games
- **Three Playable Games**:
  - **War**: Simple high-card competition
  - **Go Fish**: Ask opponents for cards to make books
  - **Hearts**: Classic trick-taking game with point avoidance
- **LAN Multiplayer**: 2-4 players over local network
- **Clean UI**: Separate Board view (table) and Controller view (player hand)
- **Extensible**: Easy to add new card games

## 📁 Project Structure

```
CardGameBuilder/
├── Assets/
│   └── Scripts/
│       ├── Core/
│       │   ├── GameTypes.cs          # Card, Suit, Rank, enums, utilities
│       │   ├── CardGameManager.cs    # Main game logic & networking
│       │   └── NetworkGameManager.cs # Netcode integration
│       ├── Games/
│       │   └── GameRules.cs          # War, Go Fish, Hearts implementations
│       └── UI/
│           ├── BoardUI.cs            # Host/table display
│           ├── ControllerUI.cs       # Player hand & actions
│           └── NetworkUI.cs          # Connection menu
├── INTEGRATION_GUIDE.md              # Detailed setup & testing guide
└── README.md                         # This file
```

## 🚀 Quick Start

### Prerequisites

1. **Unity 2021.3+** (LTS recommended)
2. **Packages** (install via Package Manager):
   - Netcode for GameObjects (1.5.0+)
   - Unity Transport (2.0.0+)
   - TextMeshPro (usually pre-installed)

### Setup (5 Minutes)

1. **Open project in Unity**
2. **Install required packages** (see Prerequisites)
3. **Follow INTEGRATION_GUIDE.md** for detailed setup
4. **Create scene objects**:
   - NetworkManager (with CardGameManager, NetworkGameManager)
   - BoardCanvas (with BoardUI)
   - ControllerCanvas (with ControllerUI)
   - NetworkCanvas (with NetworkUI)
5. **Assign references** in Inspector
6. **Build & Test**: File → Build Settings → Build

### Testing

**Host (Unity Editor):**
```
1. Press Play
2. Click "Start as Host"
3. Select game type (War/Go Fish/Hearts)
4. Click "Start Game" when clients connected
```

**Client (Built Executable):**
```
1. Run .exe
2. Enter host IP (127.0.0.1 for local)
3. Click "Join as Client"
4. Wait for game to start
```

See **INTEGRATION_GUIDE.md** for comprehensive testing instructions.

## 🎯 Game Rules Summary

### War
- Cards dealt evenly to all players
- Each turn, players flip top card simultaneously
- Highest card wins all flipped cards
- Winner is player with most cards at end

### Go Fish
- Each player gets 5 cards
- Ask opponents for specific ranks
- If they have it, take all cards of that rank
- If not, "Go Fish" (draw a card)
- Collect 4 of a kind to make a "book" (+1 point)
- Most books wins

### Hearts
- Each player gets 13 cards (4 players)
- Trick-taking: must follow suit if possible
- Hearts = 1 point, Queen of Spades = 13 points
- **Lowest score wins**

## 🔧 Architecture

### Network Flow

```
Client Action Request
    ↓
[ServerRpc] PerformActionServerRpc
    ↓
Server: Validate action
    ↓
Server: Update game state
    ↓
[ClientRpc] Notify all clients
    ↓
Clients: Update UI
```

### Key Components

- **CardGameManager**: Server-authoritative game logic
- **NetworkGameManager**: Player connection/seat management
- **IGameRules**: Interface for game-specific rules
- **BoardUI**: Displays game state for all players
- **ControllerUI**: Individual player interface

### Deterministic Gameplay

All games use a shared random seed for shuffling, ensuring:
- Same deck order on all clients
- Reproducible games (same seed = same shuffle)
- Server can validate client actions
- Easy debugging and replay functionality

## 🔌 Extending with New Games

1. **Create new rules class** implementing `IGameRules`:
   ```csharp
   public class BlackjackRules : IGameRules { ... }
   ```

2. **Add to GameType enum**:
   ```csharp
   public enum GameType { ..., Blackjack = 4 }
   ```

3. **Register in CardGameManager**:
   ```csharp
   GameType.Blackjack => new BlackjackRules()
   ```

4. **Add UI support** in ControllerUI if needed

See `GameRules.cs` for reference implementations.

## 📊 Network Architecture

### Authority Model
- **Server**: Owns all game state, validates actions
- **Clients**: Send action requests, display UI updates

### Data Flow
- **NetworkVariable<T>**: Game state, turn index, scores
- **ServerRpc**: Client → Server action requests
- **ClientRpc**: Server → Client notifications

### Synchronization
- **Hand Updates**: Only sent to owning player
- **Public Events**: Broadcast to all clients
- **Scores**: Synchronized after round ends

## 🛠️ Technical Details

### Card Representation
```csharp
struct Card {
    Suit suit;    // Hearts, Diamonds, Clubs, Spades
    Rank rank;    // Ace (1) through King (13)
}
```

- 52 cards total
- Lightweight struct (8 bytes)
- INetworkSerializable for efficient transmission
- CardId (0-51) for compact serialization

### Deck Management
```csharp
class Deck {
    void Shuffle(int seed);    // Deterministic Fisher-Yates
    Card Draw();               // Pop from top
    void ReturnToBottom(Card); // Add to bottom
}
```

### Turn Management
- Server maintains `activeSeatIndex`
- Only active player can perform actions
- Server validates and advances turn
- NetworkVariable syncs to all clients

## 📝 Files Overview

| File | Purpose | Network Role |
|------|---------|--------------|
| `GameTypes.cs` | Core data structures | Shared |
| `CardGameManager.cs` | Main game logic | Server authority |
| `NetworkGameManager.cs` | Connection handling | Server authority |
| `GameRules.cs` | Game-specific rules | Server-side |
| `BoardUI.cs` | Table/host display | Client UI |
| `ControllerUI.cs` | Player hand/actions | Client UI |
| `NetworkUI.cs` | Connection menu | Client UI |

## 🧪 Testing Checklist

- [ ] Host starts successfully
- [ ] Clients connect to host
- [ ] Game starts with selected type
- [ ] Cards dealt correctly
- [ ] Turn system works
- [ ] Actions validated properly
- [ ] Scores update
- [ ] Game ends correctly
- [ ] Winner declared
- [ ] Disconnect handled gracefully

See **INTEGRATION_GUIDE.md** for detailed test scenarios.

## 🐛 Common Issues

### "NetworkManager not found"
→ Ensure NetworkManager GameObject exists with NetworkManager component

### "Clients can't connect"
→ Check firewall, verify IP address, ensure port 7777 is open

### "Cards not showing"
→ Verify CardUIPrefab assigned in ControllerUI Inspector

### "Game won't start"
→ Need at least 2 players, ensure game type selected

See INTEGRATION_GUIDE.md Troubleshooting section for more.

## 📚 Documentation

- **INTEGRATION_GUIDE.md**: Complete setup and testing guide
- **Code Comments**: All scripts heavily commented
- **Interface Documentation**: See `IGameRules` interface

## 🎓 Learning Resources

- [Unity Netcode Docs](https://docs-multiplayer.unity3d.com/)
- [Unity Transport Guide](https://docs.unity3d.com/Packages/com.unity.transport@latest)
- [Card Game Design Patterns](https://gameprogrammingpatterns.com/)

## 🤝 Contributing

To add new games:
1. Implement `IGameRules` interface
2. Add game type to enum
3. Register in `CardGameManager.CreateGameRules()`
4. Test with at least 2 players
5. Document rules in code comments

## 📄 License

This project is part of the CardGameBuilder Unity project.

## 🎯 Roadmap

Future enhancements:
- [ ] Advanced Hearts rules (passing cards, shooting the moon)
- [ ] Poker variants (Texas Hold'em, Five Card Draw)
- [ ] Blackjack
- [ ] Solitaire variants
- [ ] Card animations
- [ ] Sound effects
- [ ] Player avatars
- [ ] Chat system
- [ ] Replay functionality
- [ ] Tournament mode

## 📞 Support

For setup help, see **INTEGRATION_GUIDE.md**.

For bugs/issues, check Unity Console logs - all scripts include detailed Debug.Log output.

---

**Built with Unity Netcode for GameObjects** 🎮

**Designed for extensibility and clean architecture** 🏗️

**Ready for LAN multiplayer gaming** 🌐
