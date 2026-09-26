# Player cursors

See your friends at work: their cursors, what they've selected, and which buildings they're changing. It works across
colonies too.

## What you see

- **Cursors** in each player's color, labelled with their name and tag, such as *Sam (Host)* or *Alex (P2)*. They
  follow the map, whatever your camera does. A cursor over the game's interface, outside the window or off the map
  is hidden.
- **Selections**: what another player selects gets the game's selection outline in their color. Your own selection
  color wins on your screen.
- **Viewing / Editing**: a building another player has selected shows **Viewing:** and their name (other things
  they select show **Selected:**). For three seconds after they change it (recipes, workers, names, priorities,
  water or automation settings), it shows **Editing:**. It's a notice, not a lock.
- **Finding a player**: click their row in the [connection panel](CONNECTION-PANEL.md) to go to their cursor or
  selection. Your own row, or **Home**, takes you back to your colony.
- **Pings** (bind **Ping location** under Options → Bindings) mark a spot on everyone's map, in your **Ping color**.

In a separate-colonies game, cursor colors are each player's own, while colony colors (each colony's roads under
Ctrl+L, and colony names in the trading window) are the colony's. The two needn't match.

## Restyling cursors

In a co-op game, open the game menu (Esc) → **Player cursors**. Each connected player gets a card:

| Control | Range | Default |
| --- | --- | --- |
| Color | **Their color**, 10 presets, or Red/Green/Blue sliders | Their color |
| Size | 50%–300% | 100% |
| Transparency | 0%–90% | 50% |

The color also applies to their name label, selection outline and chat name. **Reset** returns a card to the
defaults. The first card, **You, in the chat**, sets the color you see your own name in, in the chat.

These choices are yours alone: nothing is sent, so each player can style others differently. They're remembered by
display name, so a friend keeps their look next time.

## Settings

In Timber Together's settings (**Mods** → the settings button beside it):

- **Player activity indicators** (on): share your cursor, selection and edits, and show others'.
- **Ping display name** and **Ping color** (yellow): what others see for you. Left on "Player", the name becomes
  your Steam name. Left on yellow, you get a color by player number (the host orange, then blue, green, pink, purple,
  teal, red and lime), so players who never change it still look different.

A player who turns sharing off disappears from your screen until they turn it back on. Turning yours off also
hides everyone else's.

How cursors are sent without touching the game: [DEVELOPING.md](DEVELOPING.md#connection-panel-chat-and-player-cursors).
