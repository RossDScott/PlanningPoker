# Plan: Consensus Mini-Game

## Overview

When all voters reach consensus on a vote, a mini-game modal pops up for everyone in the room. After play, the winner is shown to all players.

## Recommended Game: Flappy Bird Clone

Pure Blazor implementation — no canvas, no extra JS libraries. The bird is an absolutely-positioned div, pipes are CSS-animated divs, collision is handled in C# with a timer-based game loop.

**Why Flappy Bird:**
- Single input (spacebar or click) — works on desktop and mobile
- Easy to implement collision in pure C#
- Fixed-duration play (30s) means everyone finishes at the same time
- Score is clear (pipes passed)

**Alternative (simpler): Reaction Time Challenge**
- Server sends a "GO" signal after a random delay (1–5s)
- Players click the moment the screen turns green
- Fastest reaction time wins
- One click, no game loop needed

---

## Architecture

### New SignalR Messages (HubMessages.cs)

```
StartMinigame     → server → all clients (triggers game popup + countdown)
SubmitGameScore   → client → server (player submits their score)
MinigameResults   → server → all clients (winner + all scores)
```

### Server Side (PlanningPokerHub.cs)

Add in-memory state:
```csharp
// Per-room active games
Dictionary<string, GameSession> _activeSessions

record GameSession(
    string RoomId,
    HashSet<string> ExpectedPlayers,
    Dictionary<string, int> Scores,
    CancellationTokenSource TimeoutCts
)
```

New methods:
- `StartMinigame(roomId, voterIds)` — called internally when consensus is detected at reveal time, broadcasts `StartMinigame` to room group
- `SubmitGameScore(roomId, score)` — client calls this; when all scores in (or 60s timeout), broadcast `MinigameResults` with winner

### Client Side

**PokerRoom.razor changes:**
- On `OnRevealEstimates`: if `stats.IsConsensus` → call hub `StartMinigame`
- On `StartMinigame` hub message → show `MinigameModal`
- On `MinigameResults` hub message → show winner banner

**New: MinigameModal.razor**
- MudBlazor Dialog/Overlay component
- Contains the Flappy Bird game
- States: `Countdown → Playing → Submitting → WaitingForOthers → Done`
- Game loop via `PeriodicTimer` at 50ms intervals (~20fps, enough for this)

---

## Flappy Bird Implementation Detail

### Game State (all C# fields)

```csharp
double _birdY = 250;        // px from top
double _birdVelocity = 0;
bool _gameOver = false;
int _score = 0;
List<Pipe> _pipes = new();
PeriodicTimer _gameTimer;

record Pipe(double X, double GapTop); // GapTop = top of the gap
const double GapHeight = 150;         // px
const double BirdX = 80;              // fixed horizontal position
const double BirdSize = 30;
const double PipeWidth = 60;
const double Gravity = 0.8;
const double FlapStrength = -10;
const int GameDurationSeconds = 30;
```

### Rendering (pure CSS div layout)

```html
<div class="game-canvas" style="position:relative; width:400px; height:500px; overflow:hidden; background:#87CEEB">
  <!-- Bird -->
  <div style="position:absolute; left:80px; top:@(_birdY)px; width:30px; height:30px">🐦</div>

  <!-- Pipes (foreach) -->
  @foreach (var pipe in _pipes)
  {
    <!-- Top pipe -->
    <div style="position:absolute; left:@(pipe.X)px; top:0; width:60px; height:@(pipe.GapTop)px; background:green"/>
    <!-- Bottom pipe -->
    <div style="position:absolute; left:@(pipe.X)px; top:@(pipe.GapTop + 150)px; width:60px; height:500px; background:green"/>
  }
</div>
```

### Game Loop (per tick at 50ms)

1. Apply gravity: `_birdVelocity += Gravity; _birdY += _birdVelocity`
2. Move pipes left: `pipe.X -= 4`
3. Spawn new pipe every 90 ticks (4.5s)
4. Remove pipes that have left screen
5. Check collision: bird vs pipes, floor (500px), ceiling (0px)
6. Check score: increment when pipe center passes BirdX
7. Decrement countdown timer; end game at 0 or on death
8. `await InvokeAsync(StateHasChanged)`

### Input

```csharp
// On spacebar or click anywhere on game canvas:
void Flap()
{
    if (!_gameOver && _playing)
        _birdVelocity = FlapStrength;
}
```

Attach `@onkeydown` on a focused div, `@onclick` on the canvas div.

---

## File Changes Summary

| File | Change |
|------|--------|
| `HubMessages.cs` | Add 3 new constants |
| `PlanningPokerHub.cs` | Add in-memory game sessions + 2 new hub methods |
| `PokerRoom.razor` | Trigger game on consensus reveal, handle result messages |
| `MinigameModal.razor` | **New** — full Flappy Bird game component |

---

## User Experience Flow

```
1. All players vote → consensus detected
2. Estimates revealed → "Consensus! Everyone voted 5"
3. ★ Mini-game modal opens for all players simultaneously
4. 3... 2... 1... GO!
5. Each player plays their own Flappy Bird for 30 seconds
6. Score submitted automatically when game ends
7. Waiting indicator while others finish
8. 🏆 "Alice wins with 12 pipes!" shown to everyone
9. Modal can be dismissed, normal reset flow continues
```

---

## Rough Scope

- ~200 lines for MinigameModal.razor (game logic + UI)
- ~50 lines added to PlanningPokerHub.cs
- ~30 lines added to PokerRoom.razor
- ~10 lines added to HubMessages.cs

No new npm packages, no JS canvas — pure Blazor/C#.
