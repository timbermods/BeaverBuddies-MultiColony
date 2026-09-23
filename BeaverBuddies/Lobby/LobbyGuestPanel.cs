using BeaverBuddies.Colonies;
using BeaverBuddies.Connect;
using BeaverBuddies.Factions;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UIFormatters;
using TimberNet;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// A guest's page in a host's waiting room: the same page as the host's (LobbyPage), titled with the host's name,
    /// with I'm ready / Not ready and Leave (D7). Opened when the host's welcome arrives on a join from the main menu
    /// (ClientConnectionService keeps the connection); closed when the save arrives (the scene changes), when the guest
    /// leaves, or when the host ends the room, which says why in the game's own box. Main menu only (D20).
    /// </summary>
    public class LobbyGuestPanel : RegisteredSingleton, IPanelController, IUpdatableSingleton
    {
        private readonly VisualElementLoader _loader;
        private readonly VisualElementInitializer _initializer;
        private readonly PanelStack _panelStack;
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly ITooltipRegistrar _tooltipRegistrar;
        private readonly FactionSpecService _factionSpecService;
        private readonly ClientConnectionService _clientConnectionService;
        private readonly TimestampFormatter _timestampFormatter;

        private LobbyPage page;
        private ClientEventIO io;
        private TimberClient net;
        private FactionSpec faction;
        private string hostName = "";
        private int shownVersion = -1;
        private bool ready;
        private bool shown;
        private bool popWhenOnTop;
        private bool asking;
        private double watchdogFromMs;
        // A mixed-factions room: this player's faction switcher, shown while they may pick.
        private LobbyFactionPicker picker;
        private bool pickerShown;

        public LobbyGuestPanel(VisualElementLoader loader, VisualElementInitializer initializer, PanelStack panelStack,
            DialogBoxShower dialogBoxShower, ITooltipRegistrar tooltipRegistrar, FactionSpecService factionSpecService,
            ClientConnectionService clientConnectionService, TimestampFormatter timestampFormatter)
        {
            _timestampFormatter = timestampFormatter;
            _loader = loader;
            _initializer = initializer;
            _panelStack = panelStack;
            _dialogBoxShower = dialogBoxShower;
            _tooltipRegistrar = tooltipRegistrar;
            _factionSpecService = factionSpecService;
            _clientConnectionService = clientConnectionService;
        }

        public VisualElement GetPanel() => page?.Root;

        // Enter toggles ready, as the page's Next does.
        public bool OnUIConfirmed()
        {
            if (net == null || net.Lobby.View().Stage != LobbyStage.Open) return false;
            SetReady(!ready);
            return true;
        }

        // Esc: leave, after asking.
        public void OnUICancelled() => AskToLeave();

        public void UpdateSingleton()
        {
            try { Pump(); }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not update the waiting room: " + error.Message); }
        }

        private void Pump()
        {
            if (popWhenOnTop && _panelStack.IsPanelOnTop(this))
            {
                popWhenOnTop = false;
                _panelStack.Pop(this);
            }
            if (!shown)
            {
                // A page still waiting to come off the stack is not opened again on top of itself.
                if (popWhenOnTop) return;
                if (!(EventIO.Get() is ClientEventIO current) || current.NetBase == null || current.NetBase.IsStopped) return;
                LobbyView welcome = current.NetBase.Lobby.View();
                if (!welcome.Welcomed || welcome.Ended) return;
                // The Connecting box goes first; then the page takes the place of the page under it (the main menu).
                _clientConnectionService.CloseConnectingBox();
                if (!PageOnTop()) return;
                Open(current, welcome);
                return;
            }

            LobbyView view = net.Lobby.View();
            if (view.Ended)
            {
                Close();
                ShowEnd(view);
                return;
            }
            // The connection is gone without a word from the host: the join's own error box says so.
            if (!ReferenceEquals(EventIO.Get(), io) || net.IsStopped)
            {
                Close();
                return;
            }
            if (view.Version != shownVersion)
            {
                shownVersion = view.Version;
                bool open = view.Stage == LobbyStage.Open;
                page.SetPlayers(view.Players, view.You, hostPage: false, canChange: open, faction?.Logo.Asset);
                RefreshPicker(view, open);
                page.SetStatus(LobbyRules.GuestStatus(ready, view.Stage, hostName));
                page.Next.SetEnabled(open);
                page.Next.text = RegisteredLocalizationService.T(ready ? "BeaverBuddies.Lobby.Button.Unready" : "BeaverBuddies.Lobby.Button.Ready");
            }
            double now = RttTracker.NowMs;
            if (!asking && LobbyRules.WatchdogDue(Math.Max(view.LastFrameAtMs, watchdogFromMs), now, view.Stage)) AskToKeepWaiting();
        }

        /// <summary>
        /// Whether a page (not an overlay or a dialog) is on top of the main menu's panel stack. HideAndPush hides only the
        /// top panel: an invite accepted in the Steam overlay leaves the game's SteamOverlayInputBlocker on top while the
        /// overlay is open, and a page pushed then hid the blocker, left the main menu showing, and shared the screen with
        /// it (each half its height, the page's buttons cut off: the first playtest). The game pops the blocker as the
        /// overlay closes, if it is still on top; the page waits for that.
        /// </summary>
        private bool PageOnTop() => _panelStack._stack.Count == 0 || !_panelStack.TopPanel.IsOverlay;

        private void Open(ClientEventIO current, LobbyView view)
        {
            io = current;
            net = current.NetBase;
            hostName = view.Summary?.HostName ?? "";
            ready = false;
            shownVersion = -1;
            asking = false;
            watchdogFromMs = RttTracker.NowMs;
            _clientConnectionService.CloseConnectingBox();
            net.SendLobbyHello(LocalPlayerIdentity.Id, LocalPlayerIdentity.Name);

            // The room's faction (a new game's, or a save's own): the logo of a row whose player has none of their own.
            faction = view.Summary == null || string.IsNullOrEmpty(view.Summary.FactionId) ? null : FactionOrNull(view.Summary.FactionId);
            LocalFactionPick.Clear();
            picker = null;
            pickerShown = false;
            page = new LobbyPage(_loader, _initializer, _tooltipRegistrar, "BeaverBuddies.Lobby.Header.Guest");
            page.SetHeader(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Header.Guest", hostName));
            page.Back.text = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Button.Leave");
            page.Next.text = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Button.Ready");
            page.Back.clicked += AskToLeave;
            page.Next.clicked += () => SetReady(!ready);
            page.Invite.ToggleDisplayStyle(false);
            page.DirectIp.ToggleDisplayStyle(false);
            LobbySummary summary = view.Summary;
            page.SetSummary(Summary(summary), summary != null && summary.IsSave
                ? LobbyPage.SaveLine(_timestampFormatter, summary.SaveName, summary.Cycle, summary.Day)
                : summary?.Settlement);
            page.SetFactions(FactionOrNull);
            if (summary != null && summary.Mixed)
                page.SetFactionNote(RegisteredLocalizationService.T(summary.IsSave ? "BeaverBuddies.Lobby.Faction.MixedSave" : "BeaverBuddies.Lobby.Faction.Mixed"));
            shown = true;
            popWhenOnTop = false;
            Plugin.Log($"[Lobby] In {hostName}'s waiting room as player {view.You}");
            _panelStack.HideAndPush(this);
            Pump();
        }

        // The Game Mode page's summary, as this player's game words it; a save's settlement for a save.
        private string Summary(LobbySummary summary)
        {
            if (summary == null) return "";
            if (summary.IsSave) return summary.Settlement;
            string mode = RegisteredLocalizationService.T(summary.ModeLocKey ?? "NewGameConfigurationPanel.Custom");
            // A mixed game has no one faction: each player picks theirs.
            if (summary.Mixed) return summary.MapName + " - " + mode;
            string factionName = faction?.DisplayName.Value ?? summary.FactionId;
            return factionName + " - " + summary.MapName + " - " + mode;
        }

        private FactionSpec FactionOrNull(string id)
        {
            try { return string.IsNullOrEmpty(id) ? null : _factionSpecService.GetFaction(id); }
            catch (Exception) { return null; }
        }

        /// <summary>
        /// A mixed room: the faction page's switcher while this player may pick (a new game; a save's player whose colony
        /// has none yet), showing their own pick, else their row's faction. A pick goes to the host at once and is kept on
        /// this computer for their founding (LocalFactionPick).
        /// </summary>
        private void RefreshPicker(LobbyView view, bool open)
        {
            LobbySummary summary = view.Summary;
            LobbyPlayer own = view.Players.FirstOrDefault(p => p.Number == view.You);
            bool mayPick = summary != null && summary.Mixed && own != null && own.MayPick;
            if (!mayPick)
            {
                if (pickerShown) page.SetFactionPicker(null);
                pickerShown = false;
                return;
            }
            if (picker == null)
            {
                picker = new LobbyFactionPicker(_initializer);
                picker.Changed += id =>
                {
                    LocalFactionPick.Set(id);
                    net?.SendLobbyFaction(id);
                    Plugin.Log($"[Lobby] Picked {id}");
                };
            }
            if (!pickerShown) page.SetFactionPicker(picker);
            pickerShown = true;
            string shown = LocalFactionPick.Mine ?? own.Faction ?? summary.FactionId;
            picker.Set(summary.Factions.Select(FactionOrNull).Where(f => f != null), shown, open);
            // Not picked yet: the faction the host has for this player follows the host's own changes.
            if (LocalFactionPick.Mine == null) picker.Select(shown);
        }

        private void SetReady(bool value)
        {
            if (net == null || net.Lobby.View().Stage != LobbyStage.Open) return;
            ready = value;
            net.SendLobbyReady(ready);
            shownVersion = -1;
        }

        private void AskToLeave()
        {
            if (!shown) return;
            _dialogBoxShower.Create()
                .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Leave", hostName))
                .SetConfirmButton(Leave, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Button.Leave"))
                .SetCancelButton(() => { }, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.Stay"))
                .Show();
        }

        private void AskToKeepWaiting()
        {
            asking = true;
            _dialogBoxShower.Create()
                .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Watchdog", hostName))
                .SetConfirmButton(() =>
                {
                    asking = false;
                    watchdogFromMs = RttTracker.NowMs;
                }, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Confirm.KeepWaiting"))
                .SetCancelButton(() =>
                {
                    asking = false;
                    Leave();
                }, RegisteredLocalizationService.T("BeaverBuddies.Lobby.Button.Leave"))
                .Show();
        }

        private void Leave()
        {
            if (!shown) return;
            Plugin.Log($"[Lobby] Left {hostName}'s waiting room");
            ClientEventIO leaving = io;
            Close();
            EventIO.ResetIf(leaving);
        }

        // The connection whose room's end this page last showed.
        private TimberClient endShownFor;

        /// <summary>
        /// A room that ended this join (ClientConnectionService): this page says why, unless it is showing that room (its
        /// update says it) or already has. A room whose welcome and end were read in one frame never opened the page, and
        /// nothing else would say anything.
        /// </summary>
        public void ShowEndIfUnseen(TimberClient ended)
        {
            if (ended == null) return;
            bool pageSaysIt = ReferenceEquals(endShownFor, ended) || (shown && ReferenceEquals(net, ended));
            if (!BeaverBuddies.Connect.JoinFlowRules.ReportJoinError(waitingRoomEnded: true, pageShowedEnd: pageSaysIt)) return;
            LobbyView view = ended.Lobby.View();
            hostName = view.Summary?.HostName ?? "";
            endShownFor = ended;
            ShowEndBox(view);
        }

        private void ShowEnd(LobbyView view)
        {
            // Said once: the join's error report (ClientConnectionService) may reach this room's end as well.
            if (ReferenceEquals(endShownFor, net)) { EventIO.ResetIf(io); return; }
            endShownFor = net;
            EventIO.ResetIf(io);
            ShowEndBox(view);
        }

        private void ShowEndBox(LobbyView view)
        {
            string key;
            switch (view.EndReason)
            {
                case LobbyEndReason.Removed: key = "BeaverBuddies.Lobby.End.Removed"; break;
                case LobbyEndReason.Failed: key = "BeaverBuddies.Lobby.End.Failed"; break;
                default: key = "BeaverBuddies.Lobby.End.Cancelled"; break;
            }
            Plugin.Log($"[Lobby] {hostName}'s waiting room ended: {view.EndReason} {view.EndDetail}");
            _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T(key, hostName, view.EndDetail ?? "")).Show();
        }

        // The page goes as soon as it is on top (a box over it closes first).
        private void Close()
        {
            shown = false;
            if (_panelStack.IsPanelOnTop(this)) _panelStack.Pop(this);
            else popWhenOnTop = true;
        }
    }
}
