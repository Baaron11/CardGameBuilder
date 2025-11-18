# M4 Rule Nodes Reference Card

Quick reference for all available rule node types in the Custom Game Editor.

---

## 🔵 EVENT NODES (Blue)

Entry points that trigger rule execution. Every rule graph must have at least one event node.

### OnGameStart
**When:** Game initialization, before first round
**Use for:** Setup actions, initial deals
**Example:** Deal initial cards to all players

### OnRoundStart
**When:** Beginning of each round
**Use for:** Round setup, reset temporary state
**Example:** Shuffle deck, clear trick pile

### OnTurnStart
**When:** When player's turn begins
**Use for:** Turn-based automatic actions
**Example:** Auto-draw 1 card, display turn message

### OnTurnEnd
**When:** After player completes their action
**Use for:** Cleanup, win condition checks
**Example:** Check if player won, advance to next player

### OnCardPlayed
**When:** After a card is played from hand
**Use for:** React to card plays
**Example:** Score points based on card value

### OnCardDrawn
**When:** After a card is drawn from deck
**Use for:** React to draws
**Example:** Trigger special effect if Ace drawn

### OnRoundEnd
**When:** Round completes (all players acted)
**Use for:** Round scoring, tallying
**Example:** Calculate round winner

### OnGameEnd
**When:** Game completes
**Use for:** Final scoring, winner announcement
**Example:** Display final scores

---

## 🟠 CONDITION NODES (Orange)

Branching logic with **true** and **false** outputs. Connect both branches!

### CompareCardValue
**Parameters:**
- `operator`: >, >=, <, <=, ==, !=
- `value`: Number to compare against

**True if:** Card value matches condition
**Example:** Card value > 10 → High card bonus

### CheckHandEmpty
**Parameters:**
- `playerIndex`: Player to check ("current" or 0-7)

**True if:** Player has no cards in hand
**Example:** Hand empty → Player loses

### CheckHandCount
**Parameters:**
- `operator`: >, >=, <, <=, ==, !=
- `count`: Number of cards
- `playerIndex`: Player to check

**True if:** Hand count matches condition
**Example:** Hand count >= 5 → Must discard

### CheckDeckEmpty
**Parameters:**
- `deckIndex`: Deck to check (default: 0)

**True if:** No cards remaining in deck
**Example:** Deck empty → End game

### CheckScore
**Parameters:**
- `playerIndex`: Player to check ("current" or 0-7)
- `operator`: >, >=, <, <=, ==, !=
- `value`: Score threshold

**True if:** Player score matches condition
**Example:** Score >= 10 → Player wins

### CheckPlayerCount
**Parameters:**
- `operator`: >, >=, <, <=, ==, !=
- `count`: Number of active players

**True if:** Active player count matches
**Example:** Players == 1 → Last player standing wins

### CheckSuit
**Parameters:**
- `suit`: Hearts, Diamonds, Clubs, Spades
- `cardSource`: Which card to check

**True if:** Card suit matches
**Example:** Suit == Hearts → Heart penalty

### CheckRank
**Parameters:**
- `rank`: Ace, 2-10, Jack, Queen, King
- `cardSource`: Which card to check

**True if:** Card rank matches
**Example:** Rank == Ace → Special action

---

## 🟢 ACTION NODES (Green)

Modify game state. Most actions auto-advance to next connected node.

### DrawCard
**Parameters:**
- `playerIndex`: "current" or 0-7
- `count`: Number of cards (default: 1)
- `deckIndex`: Which deck (default: 0)

**Effect:** Player draws card(s) from deck
**Example:** Current player draws 2 cards

### PlayCard
**Parameters:**
- `playerIndex`: "current" or 0-7
- `cardIndex`: Which card from hand (default: 0 = first)

**Effect:** Plays card from hand to play area
**Example:** Play first card in hand

### DiscardCard
**Parameters:**
- `playerIndex`: "current" or 0-7
- `cardIndex`: Which card from hand

**Effect:** Moves card from hand to discard pile
**Example:** Discard card at index 2

### AddScore
**Parameters:**
- `playerIndex`: "current" or 0-7
- `points`: Amount to add

**Effect:** Increases player score
**Example:** Add 5 points to current player

### SubtractScore
**Parameters:**
- `playerIndex`: "current" or 0-7
- `points`: Amount to subtract

**Effect:** Decreases player score
**Example:** Penalty: subtract 3 points

### SetScore
**Parameters:**
- `playerIndex`: "current" or 0-7
- `score`: New score value

**Effect:** Sets score to specific value
**Example:** Reset score to 0

### TransferCard
**Parameters:**
- `fromPlayerIndex`: Source player
- `toPlayerIndex`: Destination player
- `cardIndex`: Which card

**Effect:** Moves card between players
**Example:** Transfer card 0 from player 1 to player 2

### ShuffleDeck
**Parameters:**
- `deckIndex`: Which deck (default: 0)

**Effect:** Randomizes deck order
**Example:** Shuffle main deck

### NextTurn
**Parameters:** None

**Effect:** Advances to next player's turn
**Example:** End current turn, next player goes

### EndRound
**Parameters:** None

**Effect:** Ends current round, triggers OnRoundEnd
**Example:** All players acted, round complete

### EndGame
**Parameters:**
- `winnerIndex`: Winning player (optional, "current" or 0-7)

**Effect:** Ends game, determines winner
**Example:** First to 10 points wins

### ShowMessage
**Parameters:**
- `message`: Text to display

**Effect:** Broadcasts event message to all players
**Example:** "Special event triggered!"

---

## 📊 COMMON PATTERNS

### Auto-Draw Each Turn
```
OnTurnStart → DrawCard(count=1) → NextTurn
```

### Win Condition Check
```
OnTurnEnd → CheckScore(value=10, operator=">=")
           ├─ [true] → EndGame
           └─ [false] → NextTurn
```

### Discard Extra Cards
```
OnTurnEnd → CheckHandCount(count=5, operator=">")
           ├─ [true] → ShowMessage("Too many cards!")
           │           → DiscardCard
           │           → SubtractScore(points=1)
           └─ [false] → NextTurn
```

### Empty Deck Handling
```
OnCardDrawn → CheckDeckEmpty
             ├─ [true] → ShowMessage("Deck empty!")
             │           → EndRound
             └─ [false] → (continue)
```

### Bonus for High Cards
```
OnCardPlayed → CompareCardValue(value=10, operator=">")
              ├─ [true] → AddScore(points=2)
              │           → ShowMessage("High card bonus!")
              └─ [false] → AddScore(points=1)
```

### Suit-Based Scoring
```
OnCardPlayed → CheckSuit(suit="Hearts")
              ├─ [true] → AddScore(points=1)
              └─ [false] → CheckSuit(suit="Spades")
                         ├─ [true] → SubtractScore(points=1)
                         └─ [false] → (no score change)
```

---

## 🔗 CONNECTION RULES

### Event Nodes
- **Outputs:** 1 (default "out")
- **Connect to:** Any node type
- **Entry Point:** Yes (can be unconnected)

### Condition Nodes
- **Outputs:** 2 ("true", "false")
- **Connect to:** Any node type
- **Must connect:** Both branches recommended
- **Entry Point:** No (must be triggered)

### Action Nodes
- **Outputs:** 1 (default "out")
- **Connect to:** Any node type
- **Auto-execute:** Yes (runs then follows connection)
- **Entry Point:** No (must be triggered)

---

## ⚠️ IMPORTANT NOTES

### Parameter Values
- **"current"**: Refers to active player this turn
- **0-7**: Specific player seat index
- **Operators**: >, >=, <, <=, ==, !=

### Execution Order
1. Event fires (e.g., OnTurnStart)
2. Follows connections in order created
3. Condition evaluates, picks branch
4. Action executes, auto-continues
5. Process repeats until no more connections

### Validation Requirements
- Must have at least 1 Event node
- Orphaned nodes (no incoming connections) are allowed but warned
- Self-referencing links are prohibited
- Infinite loops possible - test carefully!

### Performance
- Limit depth to ~20 nodes per event
- Avoid infinite loops (e.g., OnCardDrawn → DrawCard)
- Complex graphs may lag on mobile

---

## 📝 EXAMPLE GAMES

### Simple Draw Game
**Goal:** First to 10 points wins
**Rules:**
- Each turn: Draw 1 card, gain 1 point
- Check win condition after each turn

```
OnTurnStart → DrawCard(count=1)
             → AddScore(points=1)
             → CheckScore(value=10, operator=">=")
                ├─ [true] → EndGame
                └─ [false] → NextTurn
```

### High Card Wins
**Goal:** Highest card played each round scores
**Rules:**
- All players play 1 card
- Highest value scores 1 point
- First to 5 points wins

```
OnTurnStart → PlayCard
OnRoundEnd → DetermineHighestCard (custom logic)
            → AddScore(points=1, playerIndex=winner)
            → CheckScore(value=5, operator=">=")
               ├─ [true] → EndGame
               └─ [false] → NextRound
```

### Go Fish Clone
**Goal:** Collect sets of 4 matching cards
**Rules:**
- Start with 5 cards
- Ask opponents for ranks
- Get a set → Score 1 point
- First to 7 points wins

```
OnGameStart → DrawCard(count=5)
OnTurnStart → AskForCard (custom action)
             → CheckForSets
                ├─ [has set] → AddScore(points=1)
                └─ [no set] → NextTurn
OnTurnEnd → CheckScore(value=7, operator=">=")
           ├─ [true] → EndGame
           └─ [false] → (continue)
```

---

## 🚀 QUICK START

1. **Start with an Event** - Every game needs OnGameStart or OnTurnStart
2. **Add Core Actions** - DrawCard, PlayCard, AddScore
3. **Add Win Condition** - CheckScore + EndGame
4. **Connect the Flow** - Link nodes in logical order
5. **Test Early** - Export and test after basic graph complete
6. **Iterate** - Add conditions and complexity gradually

---

**Pro Tip:** Use ShowMessage liberally to debug rule execution!

**Warning:** Always provide a way to end the game (win condition or deck empty).

**Best Practice:** Keep initial games simple (5-10 nodes), expand later.
