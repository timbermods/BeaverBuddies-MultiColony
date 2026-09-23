using BeaverBuddies.Colonies;
using BeaverBuddies.Events;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.FactionSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeaverBuddies.Factions
{
    /// <summary>The game's own ways of showing a faction's logo, in the game (a diamond) and in the menu (a ring).</summary>
    public static class FactionIcons
    {
        /// <summary>The in-game faction icon: the logo on the game's diamond (the top left statistics panel's).</summary>
        public static VisualElement Diamond(FactionSpec faction, int size)
        {
            var diamond = new VisualElement { pickingMode = PickingMode.Ignore };
            diamond.style.width = size;
            diamond.style.height = size;
            diamond.style.minWidth = size;
            diamond.style.flexShrink = 0;
            diamond.style.alignItems = Align.Center;
            diamond.style.justifyContent = Justify.Center;
            // PopulationStyle's bg-diamond-1 is only attached by the population templates: set the sprite itself.
            Sprite background = UnityEngine.Resources.Load<Sprite>("UI/Images/Backgrounds/bg-diamond-1");
            if (background != null) diamond.style.backgroundImage = new StyleBackground(background);
            var logo = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            logo.style.width = size * 0.8f;
            logo.style.height = size * 0.8f;
            if (faction != null) logo.sprite = faction.Logo.Asset;
            diamond.Add(logo);
            return diamond;
        }
    }

    /// <summary>
    /// The faction this computer's player picked in a waiting room, kept on this computer only until their colony is
    /// founded with it (D15). Display and choice only: it reaches the game solely through a founding event the host judges.
    /// </summary>
    public static class LocalFactionPick
    {
        public static string Mine { get; private set; }

        public static void Set(string faction)
        {
            Mine = string.IsNullOrEmpty(faction) ? null : faction;
        }

        public static void Clear() => Mine = null;
    }

    /// <summary>
    /// Choosing a faction in the game (D14, D15): the founding dialog, which knows the waiting room's pick or offers each
    /// faction as a card; and the offer, to a player whose colony was made in a faction they did not pick, to switch
    /// while it is untouched. Display only; the choice becomes a founding or a switch event the host judges.
    /// </summary>
    public static class FactionChoice
    {
        /// <summary>The factions this game lets a colony take: the host's unlocked ones (D1), in the game's order.</summary>
        public static List<FactionSpec> Available() =>
            MixedFactions.AllFactions.Where(f => ColonySession.HostHasFaction(f.Id)).ToList();

        /// <summary>
        /// The founding dialog in a mixed game. With a faction picked in the waiting room (and still available): "Place
        /// your Iron Teeth colony's district center", with a way to choose another. Otherwise one card per faction.
        /// </summary>
        public static void ShowFoundingDialog(DialogBoxShower shower, Action<string> place, bool chooseAgain = false)
        {
            List<FactionSpec> factions = Available();
            if (factions.Count == 0) factions = MixedFactions.AllFactions.ToList();
            FactionSpec picked = chooseAgain ? null : factions.FirstOrDefault(f => f.Id == LocalFactionPick.Mine);
            if (factions.Count == 1) picked = factions[0];
            if (picked != null)
            {
                var builder = shower.Create()
                    .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.PlacePicked", picked.DisplayName.Value))
                    .AddContent(Centered(Card(picked, null)))
                    .SetConfirmButton(() => place(picked.Id), RegisteredLocalizationService.T("BeaverBuddies.Colony.Founding.PlaceButton"))
                    .SetDefaultCancelButton();
                if (factions.Count > 1)
                    builder.SetInfoButton(() => ShowFoundingDialog(shower, place, chooseAgain: true),
                        RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.ChooseAnother"));
                builder.Show();
                return;
            }
            DialogBox dialog = null;
            VisualElement cards = NativeElements.Row(Align.FlexStart);
            cards.style.justifyContent = Justify.Center;
            cards.style.marginTop = 6;
            foreach (FactionSpec faction in factions)
            {
                cards.Add(Card(faction, () =>
                {
                    dialog?.Close();
                    place(faction.Id);
                }));
            }
            dialog = shower.Create()
                .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.Choose"))
                .AddContent(cards)
                .SetDefaultCancelButton()
                .Show();
        }

        /// <summary>
        /// A faction's card: its logo on the diamond, its name and what it is like (the game's own words), and a wooden
        /// button to take it (none when <paramref name="onChoose"/> is null).
        /// </summary>
        public static VisualElement Card(FactionSpec faction, Action onChoose)
        {
            NineSliceVisualElement card = NativeElements.Box("bg-sub-box--green");
            card.style.width = 230;
            card.style.marginLeft = 6;
            card.style.marginRight = 6;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.alignItems = Align.Center;
            card.Add(FactionIcons.Diamond(faction, 64));
            Label name = NativeElements.Text(faction.DisplayName.Value, 16, bold: true);
            name.style.marginTop = 6;
            name.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(name);
            string description = "";
            try { description = RegisteredLocalizationService.T(faction.DescriptionLocKey); }
            catch (Exception) { /* Only a description. */ }
            if (!string.IsNullOrEmpty(description))
            {
                Label about = NativeElements.MutedText(description, 12);
                about.style.whiteSpace = WhiteSpace.Normal;
                about.style.unityTextAlign = TextAnchor.UpperCenter;
                about.style.marginTop = 4;
                card.Add(about);
            }
            if (onChoose != null)
            {
                Button choose = NativeElements.WoodenButton(RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.Found",
                    faction.DisplayName.Value), onChoose);
                choose.style.marginTop = 8;
                card.Add(choose);
            }
            return card;
        }

        private static VisualElement Centered(VisualElement element)
        {
            VisualElement row = NativeElements.Row();
            row.style.justifyContent = Justify.Center;
            row.Add(element);
            return row;
        }

        /// <summary>
        /// Offered once to a seated player whose colony already exists (a multi-start game's start) when it plays another
        /// faction than they picked, or they picked none: they may switch while it is untouched.
        /// </summary>
        public static void OfferSwitch(DialogBoxShower shower, int slot, UntouchedFacts facts)
        {
            if (!MixedFactions.IsOn || slot < 0 || !facts.IsUntouched || facts.DistrictCenters <= 0) return;
            string current = ColonyFactionService.FactionOfSlot(slot);
            if (LocalFactionPick.Mine != null && LocalFactionPick.Mine == current) return;
            List<FactionSpec> others = Available().Where(f => f.Id != current).ToList();
            if (others.Count == 0) return;
            FactionSpec now = MixedFactions.Spec(current);
            FactionSpec wanted = others.FirstOrDefault(f => f.Id == LocalFactionPick.Mine) ?? others[0];
            shower.Create()
                .SetMessage(RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.StartPrompt", now?.DisplayName.Value ?? current,
                    wanted.DisplayName.Value))
                .AddContent(Centered(Card(now, null)))
                .SetConfirmButton(() => RequestSwitch(wanted.Id),
                    RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.Switch", wanted.DisplayName.Value))
                .SetCancelButton(() => { }, RegisteredLocalizationService.T("BeaverBuddies.Colony.Faction.Keep", now?.DisplayName.Value ?? current))
                .Show();
        }

        /// <summary>Asks for the local colony to become <paramref name="faction"/> (a synced action the host judges).</summary>
        public static void RequestSwitch(string faction)
        {
            if (!MixedFactions.IsOn || string.IsNullOrEmpty(faction)) return;
            ReplayEvent.DoPrefix(() => new ColonyFactionSwitchEvent { faction = faction });
        }

        /// <summary>The host's word on a switch (D14): the colony's own seated player, an available faction, untouched.</summary>
        public static FactionSwitchVerdict HostJudgeSwitch(ColonyFactionSwitchEvent switching)
        {
            ColonyFoundingService founding = SingletonManager.GetSingleton<ColonyFoundingService>();
            if (founding == null) return FactionSwitchVerdict.NotMixed;
            int slot = switching.slot;
            bool isSeatOwner = slot >= 0 && ColonySession.SeatOfPlayer(switching.player) == slot;
            return FactionRules.JudgeSwitch(MixedFactions.IsOn, isSeatOwner, ColonyFactionService.FactionOfSlot(slot), switching.faction,
                ColonyFactionService.FactionIds.ToList(), Available().Select(f => f.Id).ToList(), founding.UntouchedFacts(slot));
        }
    }

    /// <summary>
    /// An untouched colony becomes another faction (D14): its district center is replaced in place by the other
    /// faction's, and its beavers by that faction's. Judged by the host, played on every computer.
    /// </summary>
    [Serializable]
    public class ColonyFactionSwitchEvent : ReplayEvent
    {
        public string faction;
        /// <summary>The host's starting numbers (up to which beavers are remade), written when it allows the switch.</summary>
        public ColonyStartingSettings startingSettings;

        public override ColonyScope GetColonyScope() => ColonyScope.Global;

        public override void Replay(IReplayContext context)
        {
            ColonyFoundingService founding = SingletonManager.GetSingleton<ColonyFoundingService>();
            if (founding == null)
            {
                Plugin.LogWarning("[Factions] Cannot switch a colony's faction: the founding service is missing");
                return;
            }
            founding.SwitchFaction(slot, faction, startingSettings);
        }

        public override string ToActionString() => $"Colony {slot + 1} switches to {faction}";
    }
}
