# Steam invites

Invite Steam friends into your game from Steam's overlay: no port forwarding, no Hamachi. Joining by IP works
alongside it.

## Host

1. Host a game: **Host co-op game** beside **Start** for a new game, right of **Load** in the Load game box for a
   save, or in the game menu (Esc) for the game you're in.
2. In the **Co-op Game** room, choose **Invite Friends** and pick your friend in Steam's overlay. The button is
   greyed for a moment, until the Steam lobby is ready.
3. Your friend appears in the room. When everyone is **Ready**, press **Start Game**.

Nobody can join after **Start Game**. To let someone in later, choose Esc → **Save and Rehost** and invite them.
Players already in the game come along by themselves. Friends without Steam join by IP (port 25565).
**Enable Steam Networking** (Mod Settings, on by default) must be on.

## Friend

- Accept the invite from Steam's notification or overlay. A *Connecting to …* box shows, then the host's room:
  press **Ready**.
- If Timberborn is closed, Steam starts it and joins for you.
- Playing alone? The room opens over your game, which waits, paused, until **Start Game**. **Leave** takes you
  back to it.
- In a co-op game, leave it first, then accept the invite again. Hosting a room of your own? Close it first.
- With the host's **Allow Friends to Join Directly via Steam** on (the default), their game also shows in **Join
  co-op game**, and **Join Game** works from your Steam friends list.
- An invite to a game that has started says so. Ask the host to rehost and send a new one.

## Good to know

- Everyone must be online in Steam and own Timberborn there, with the same mod build and game version.
- **Your colony follows your Steam account.** The save remembers you by your Steam ID, whoever hosts. If your Steam
  account changes, you join as a new player, and the host can hand your old colony to you from the Ctrl+T window.
- **Over Steam, nobody can take your colony** by claiming your ID: the host checks each guest's Steam ID. Direct IP
  can't check, and the direct-IP port is open whenever someone hosts, so forward it only for people you trust.
- Steam connects players directly when it can, and relays through its network otherwise. Either way, Valve keeps IP
  addresses hidden from other players.
- **After a desync**, the host chooses **Save and Rehost** and guests choose **Reconnect (wait for Rehost)**. Each
  guest waits in their game and joins the room when it opens.
- **If Steam won't connect**, the message says why, with Steam's own reason; so does `Player.log`. Host by direct
  IP or Hamachi instead: Steam problems never stop direct-IP hosting.
- Much of the text this mod adds is English only.

How the Steam connection is built: [DEVELOPING.md](DEVELOPING.md#steam-networking).
