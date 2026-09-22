using BeaverBuddies.Colonies;
using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
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

        public LobbyGuestPanel(VisualElementLoader loader, VisualElementInitializer initializer, PanelStack panelStack,
            DialogBoxShower dialogBoxShower, ITooltipRegistrar tooltipRegistrar, FactionSpecService factionSpecService,
            ClientConnectionService clientConnectionService)
        {
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
                if (welcome.Welcomed && !welcome.Ended) Open(current, welcome);
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
                page.SetStatus(LobbyRules.GuestStatus(ready, view.Stage, hostName));
                page.Next.SetEnabled(open);
                page.Next.text = RegisteredLocalizationService.T(ready ? "BeaverBuddies.Lobby.Button.Unready" : "BeaverBuddies.Lobby.Button.Ready");
            }
            double now = RttTracker.NowMs;
            if (!asking && LobbyRules.WatchdogDue(Math.Max(view.LastFrameAtMs, watchdogFromMs), now, view.Stage)) AskToKeepWaiting();
        }

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

            try { faction = view.Summary == null ? null : _factionSpecService.GetFaction(view.Summary.FactionId); }
            catch (Exception) { faction = null; }
            page = new LobbyPage(_loader, _initializer, _tooltipRegistrar, "BeaverBuddies.Lobby.Header.Guest");
            page.SetHeader(RegisteredLocalizationService.T("BeaverBuddies.Lobby.Header.Guest", hostName));
            page.Back.text = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Button.Leave");
            page.Next.text = RegisteredLocalizationService.T("BeaverBuddies.Lobby.Button.Ready");
            page.Back.clicked += AskToLeave;
            page.Next.clicked += () => SetReady(!ready);
            page.OwnReadyToggled += SetReady;
            page.Invite.ToggleDisplayStyle(false);
            page.DirectIp.ToggleDisplayStyle(false);
            page.SetSummary(Summary(view.Summary), faction, view.Summary?.Settlement);
            shown = true;
            popWhenOnTop = false;
            Plugin.Log($"[Lobby] In {hostName}'s waiting room as player {view.You}");
            _panelStack.HideAndPush(this);
            Pump();
        }

        // The Game Mode page's summary, as this player's game words it.
        private string Summary(LobbySummary summary)
        {
            if (summary == null) return "";
            string factionName = faction?.DisplayName.Value ?? summary.FactionId;
            string mode = RegisteredLocalizationService.T(summary.ModeLocKey ?? "NewGameConfigurationPanel.Custom");
            return factionName + " - " + summary.MapName + " - " + mode;
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

        private void ShowEnd(LobbyView view)
        {
            string key;
            switch (view.EndReason)
            {
                case LobbyEndReason.Removed: key = "BeaverBuddies.Lobby.End.Removed"; break;
                case LobbyEndReason.Failed: key = "BeaverBuddies.Lobby.End.Failed"; break;
                default: key = "BeaverBuddies.Lobby.End.Cancelled"; break;
            }
            Plugin.Log($"[Lobby] {hostName}'s waiting room ended: {view.EndReason} {view.EndDetail}");
            EventIO.ResetIf(io);
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
