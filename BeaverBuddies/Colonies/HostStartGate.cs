using BeaverBuddies.Events;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using Timberborn.CoreUI;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// While the host waits paused at the start for players to join, its first change to the game (a path, a mark, a
    /// founding) closes joining for good: a player joining later would be sent the save without it. That used to happen
    /// silently. Now the host is asked first: the action is held, a dialog says what it would mean, and the host either
    /// starts the game (every held action is then played, in order) or keeps waiting (the held actions are dropped).
    /// Once the host has said yes, or once joining has closed for any other reason, nothing is held again this session.
    /// A guest's change while joining is open is refused by the host instead (see ColonyRulesService).
    /// </summary>
    public class HostStartGate : RegisteredSingleton, ILoadableSingleton, IResettableSingleton
    {
        private readonly DialogBoxShower _dialogBoxShower;
        private readonly ColonyRulesService _colonyRulesService;
        private readonly List<ReplayEvent> held = new List<ReplayEvent>();
        private bool confirmed, asking;

        public static HostStartGate Instance => SingletonManager.GetSingleton<HostStartGate>();

        public HostStartGate(DialogBoxShower dialogBoxShower, ColonyRulesService colonyRulesService)
        {
            _dialogBoxShower = dialogBoxShower;
            _colonyRulesService = colonyRulesService;
        }

        public void Load() { }

        public void Reset()
        {
            confirmed = false;
            asking = false;
            held.Clear();
        }

        /// <summary>Whether the host has said to start (or was never asked): read by the connection panel.</summary>
        public bool Confirmed => confirmed;

        /// <summary>
        /// Before the host's own action is recorded: true when it is held for the host's word (the caller then does not
        /// record it). Never holds anything on a guest, after the first tick, or once joining has closed.
        /// </summary>
        public static bool TryHold(ReplayEvent message)
        {
            HostStartGate gate = Instance;
            if (gate == null || message == null) return false;
            ServerEventIO server = EventIO.Get() as ServerEventIO;
            int ticks = SingletonManager.GetSingleton<ReplayService>()?.TicksSinceLoad ?? 1;
            if (!HostStartRules.ShouldHold(server != null, server?.IsAcceptingClients ?? false, ticks, message.ChangesGame(), gate.confirmed))
                return false;
            gate.held.Add(message);
            if (!gate.asking) gate.Ask();
            return true;
        }

        private void Ask()
        {
            asking = true;
            try
            {
                _dialogBoxShower.Create()
                    .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Start.Prompt"))
                    .SetConfirmButton(Confirm, RegisteredLocalizationService.T("BeaverBuddies.Colony.Start.Confirm"))
                    .SetCancelButton(Discard, RegisteredLocalizationService.T("BeaverBuddies.Colony.Start.Cancel"))
                    .Show();
            }
            catch (Exception error)
            {
                // Without a dialog the action goes through as before (closing joining), rather than being lost.
                Plugin.LogError("[Colony] Could not ask the host whether to start: " + error);
                Confirm();
            }
        }

        private void Confirm()
        {
            asking = false;
            confirmed = true;
            ReplayService replayService = ReplayEvent.GetReplayServiceIfReady();
            Plugin.Log($"[Colony] The host starts the game: playing {held.Count} held action(s); joining closes");
            foreach (ReplayEvent message in held)
            {
                try { replayService?.RecordEvent(message); }
                catch (Exception error) { Plugin.LogWarning($"[Colony] A held action could not be played: {error.Message}"); }
            }
            held.Clear();
        }

        private void Discard()
        {
            asking = false;
            Plugin.Log($"[Colony] The host keeps waiting: {held.Count} held action(s) dropped; players can still join");
            held.Clear();
            try { _colonyRulesService.ShowNotice(RegisteredLocalizationService.T("BeaverBuddies.Colony.Start.Kept"), warning: false); }
            catch (Exception error) { Plugin.LogWarning("[Colony] Could not show the waiting notice: " + error.Message); }
        }
    }
}
