using BeaverBuddies.Connect;
using BeaverBuddies.IO;
using BeaverBuddies.Util;
using HarmonyLib;
using System;
using Timberborn.CoreUI;
using Timberborn.OptionsGame;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A shared game's one-time split (1.4.0-rc3): a guest who wants a colony of their own finds Found your own colony in
    /// the game menu (Esc), below Settings, and nowhere else. It asks first, since the split turns the game into a
    /// separate-colonies game for every player, for good; then the guest places a district center (the founding tool, as
    /// in a separate-colonies game), and only the founding, played on every computer, makes the split. Leaving the tool
    /// changes nothing. The button is shown only to a guest without a colony in a shared game (ColonyRules.SplitOffered):
    /// never to the host, who plays the shared colony, and to nobody once the game is separate, where a player without a
    /// colony is offered one the ordinary way.
    /// </summary>
    public class SharedColonySplit : RegisteredSingleton
    {
        public const string ButtonName = "FoundOwnColonyButton";

        /// <summary>This session's guest has confirmed the split: the founding tool (and Ctrl+K) may now place their colony.</summary>
        public static bool Confirmed { get; internal set; }

        private readonly DialogBoxShower _dialogBoxShower;

        public SharedColonySplit(DialogBoxShower dialogBoxShower)
        {
            _dialogBoxShower = dialogBoxShower;
        }

        public static SharedColonySplit Instance => SingletonManager.GetSingleton<SharedColonySplit>();

        /// <summary>Whether this computer's player is offered the split now (display: the host judges the founding).</summary>
        public static bool Offered
        {
            get
            {
                if (!(EventIO.Get() is ClientEventIO)) return false;
                int seat = ColonySession.LocalSeat;
                var founding = SingletonManager.GetSingleton<ColonyFoundingService>();
                return ColonyRules.SplitOffered(isGuest: true, ColonyModeService.IsSeparateColonies, seated: seat >= 0,
                    ownsDistrict: founding != null && seat >= 0 && founding.SlotOwnsDistrict(seat));
            }
        }

        /// <summary>The game menu is shown: the button is added once, below Settings, and shown only while it is offered.</summary>
        public void AddButton(VisualElement root, GameOptionsBox box)
        {
            try
            {
                // Below Settings, and below the player cursors button if it is there, whichever of the two is added first.
                string after = root.Q<Button>("PlayerCursorsButton") != null ? "PlayerCursorsButton" : "SettingsButton";
                Button button = ButtonInserter.DuplicateOrGetButton(root, after, ButtonName, created =>
                {
                    created.text = RegisteredLocalizationService.T("BeaverBuddies.Colony.Split.Button");
                    created.clicked += () => Ask(box);
                }, box._visualElementLoader?._visualElementInitializer);
                button.ToggleDisplayStyle(Offered);
            }
            catch (Exception error)
            {
                // The menu must still open if the game's layout differs from what this expects.
                Plugin.LogWarning("[Colony] Could not add Found your own colony to the game menu: " + error.Message);
            }
        }

        private void Ask(GameOptionsBox box)
        {
            var founding = SingletonManager.GetSingleton<ColonyFoundingService>();
            if (founding == null) return;
            if (!Offered)
            {
                // Another player's split was played while this menu was open: the button is gone the next time it opens,
                // and now it says where founding is (1.4.0-rc5 review, B6).
                if (ColonyModeService.IsSeparateColonies)
                    _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Split.AlreadySeparate")).Show();
                return;
            }
            // Before the host has unpaused the game, other players can still join: the split waits for that.
            if (!founding.SplitCanBeginNow(out string whyNot))
            {
                _dialogBoxShower.Create().SetMessage(RegisteredLocalizationService.T(whyNot)).Show();
                return;
            }
            _dialogBoxShower.Create()
                .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Split.Confirm"))
                .SetConfirmButton(() =>
                {
                    // Back to the game (this computer's menu), with the district center in hand.
                    box.OnUICancelled();
                    founding.BeginSplit();
                }, RegisteredLocalizationService.T("BeaverBuddies.Colony.Split.ConfirmButton"))
                .SetDefaultCancelButton()
                .Show();
        }
    }

    [HarmonyPatch(typeof(GameOptionsBox), nameof(GameOptionsBox.GetPanel))]
    public class GameOptionsBoxSplitButtonPatcher
    {
        public static void Postfix(GameOptionsBox __instance, VisualElement __result)
        {
            if (__result == null) return;
            // Null while the registry is empty (LoadMap resets it a frame before the scene goes): the menu still shows.
            SharedColonySplit.Instance?.AddButton(__result, __instance);
        }
    }
}
