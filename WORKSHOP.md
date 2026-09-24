# Publishing on the Steam Workshop

How to publish Timber Together on the Steam Workshop, and the text for its page. Nothing here has been uploaded.

## Before the first upload

- The mod's id is `timbermods.TimberTogether`, and there is no `workshop_data.json` in the mod folder, so Timberborn's
  uploader creates a **new** Workshop item. After the first upload, Timberborn writes a `workshop_data.json` into the
  uploaded folder: keep that one for later updates.
- Upload the built mod folder (`TimberTogether`, as in the release zip) from Timberborn's mod manager. It carries
  `License.txt` and `CREDITS.md` next to `thumbnail.png`: the GPL-3.0 asks for the license and the credits to travel
  with every copy.
- Title: **Timber Together**. Tags: Multiplayer, Gameplay. Required items: Harmony, Mod Settings.
- Preview image: `thumbnail.png` (800 x 450, Timber Together's own art).
- Description: paste [`BeaverBuddies/Doc/WorkshopDescription.txt`](BeaverBuddies/Doc/WorkshopDescription.txt) (Steam's
  BBCode). It ends with the credits to BeaverBuddies; keep them. The same text in Markdown is below.
- Links: the original BeaverBuddies item (https://steamcommunity.com/sharedfiles/filedetails/?id=3293380223) is linked
  from the Credits section. Don't list it under Required items: the two cannot run together.

## Description

### Build apart. Thrive together.

Co-op Timberborn with a colony each: every player runs their own districts, beavers and science on one shared map,
and the colonies trade at Trading Posts. Co-op, not a race.

**A colony each:** your own districts, beavers, stock, working hours and (if the host chooses) your own science and
unlocks. Your screen shows your colony, your beavers work for your colony only, and nobody can change anyone else's
buildings. The river, the weather and the droughts are shared by everyone.

**Trading Posts:** the one place two colonies' roads meet. Offer "100 logs for 25 gears, 4 rounds" (or science, or
beavers, or a gift), the other player accepts, and both colonies' beavers carry it out: each side's goods wait on its
own half and cross together once both are in. Standing deals repeat by themselves, with a reserve so they never
starve you.

**Start together:** choose **Host co-op game** on the New Game page and a Co-op Game room opens. Invite Steam friends
or give your IP address, everyone readies up, and the world loads for everyone at once. Any save can be hosted the
same way. On a standard map the other players found their colony wherever they like; a multi-start map
gives each player a start.

**Folktails and Iron Teeth together:** with *Mixed factions* ticked, each player picks their own faction in the room.

**Away for the evening?** Ask a friend to look after your colony (Ctrl+T); it is kept for you.

**Out of step? It stops at once:** every computer checks its colonies against the host's as the game runs, so a game
that drifts stops the moment it happens instead of much later.

**Or one shared colony:** untick *Separate colonies* on the New Game page and everyone builds one colony together, as
in ordinary BeaverBuddies co-op.

- Up to four colonies; more players join as helpers of the host's colony.
- Steam friend invites (no port forwarding), or direct IP.
- A connection panel with chat, teammates' cursors and selections, and map pings.

#### Before you play

- Requires **Harmony** and **Mod Settings**.
- Every player needs the same version of Timber Together and the same game version.
- Do not enable another BeaverBuddies at the same time (the original or the Stability Fork): they change the same
  parts of the game and cannot run together. The main menu tells you if one is enabled.
- Play on a copy of your save, and report problems (with every player's Player.log) at
  https://github.com/timbermods/TimberTogether/issues

Guides: https://timbermods.github.io/TimberTogether/

#### Credits

Timber Together is a modified version of **BeaverBuddies**, created by **Thomas Price (thomaswp)** with contributions
from Robin, Slide, Phil Lehmkuhl, SamuZad, Joe Stead, Zibo Ye, Dasker and Tarensaror. The multiplayer at its heart is
theirs (keeping every player's game in step, the connections, the desync checks), and so is much of the code. This
mod would not exist without their work.

- BeaverBuddies on the Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3293380223
- BeaverBuddies source: https://github.com/thomaswp/BeaverBuddies
- The BeaverBuddies Stability Fork, which Timber Together is built on:
  https://github.com/timbermods/BeaverBuddies-Stability-Fork

Free software under the GPL-3.0, like the original. The license and the full credits (License.txt, CREDITS.md) are
in the mod folder, and the complete source is at https://github.com/timbermods/TimberTogether. An unofficial community
mod, not affiliated with or endorsed by Mechanistry or by the authors of BeaverBuddies.
