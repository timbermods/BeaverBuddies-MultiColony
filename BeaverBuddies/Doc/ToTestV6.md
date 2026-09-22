# What is owed to in-game testing

(This file was the original project's to-do list for a Timberborn update. It now says what this fork has and has
not seen in a game, so nobody mistakes an automated check for a playtest.)

**Played:** the Stability Fork underneath (1.1.11, whose player colors were played on top of 1.1.10), for more than an hour over Steam invites with two players and
large colonies; and the land-split alphas (1.2.0-two-colony-alpha1 to 5): hosting, joining, founding a second
colony and building in it.

**Not played:** everything from 1.4.0-alpha1 on (beta2's looking after a colony, wishlists, reserves, offering
again, the food and water days, the Home key and go-to-player, and the host's start prompt included): ownership and land, per-colony marks and work, the Trading Post and
its exchanges and panel, separate science, hand-over, and the desync review's fixes (alpha11 to alpha14). All of it
is covered by StabilityTests (headless) and RuntimeChecks (against the game's assemblies), which cannot start Unity
or prove multiplayer determinism.

**Run first:** ALPHA-TEST-SCRIPTS.md, Script B line 8a (the every-tick colony digest, alpha13). A digest that
disagrees between two healthy computers stops the guest with the desync dialog; the log line
`Colony state differs from the host's at tick …` with both change counts is what is needed to fix it. Then Script A
lines 15 to 19 (joined roads, a refused Trading Post half, gates, automation, the crossing panel).

**Watch for, in the first sessions:**
- a `Colony state differs` line that is not preceded by any real difference (a false positive of the digest);
- a gate that stays shut, or shows a conflict it should not (alpha14's real-graph walk);
- a placement the host accepts that throws on a guest (`A multiplayer action could not be completed`): the guest
  now trusts the host's verdict, so this would mean the two games already differed;
- founding refused with *the game has not started yet* after the host has unpaused (the first-tick wait);
- an *Editing* label on another colony's building for a change that was refused (known, presentation only);
- the speed boost row at the top of the chat (beta5, Script B line 8k): whether the game's small - and + draw in the
  dark panel, whether the box gives the keyboard back after Enter or Esc, and where the tick rate settles above
  speed 7 on real computers;
- the *You, in the chat* card at the top of Player cursors (beta6, Script B line 8l): whether it draws above the
  players' cards, and whether your own chat name follows a pick there on your screen only.
