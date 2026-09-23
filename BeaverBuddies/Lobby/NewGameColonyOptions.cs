using BeaverBuddies.Factions;
using BeaverBuddies.Util;
using System;
using Timberborn.CoreUI;
using Timberborn.SettingsSystem;
using Timberborn.TooltipSystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// What a new game's colonies are (1.4.0-rc3), chosen on the Game Mode page: Separate colonies, and under it Separate
    /// science and unlocks and Mixed factions. Plain static state, as the page leaves it: the game scene reads it once, as a
    /// new world is made (MultiStartPatches, MixedFactions). A waiting room's world reads the room's own copy instead
    /// (LobbySetup, taken when Host co-op game was pressed). A save keeps the mode it was made with, for good.
    /// </summary>
    public static class NewGameColonyChoice
    {
        /// <summary>Each player builds their own colony. Off: everyone plays one shared colony.</summary>
        public static bool Separate { get; internal set; } = true;

        /// <summary>A separate-colonies game gives each colony its own science and unlocks.</summary>
        public static bool SeparateScience { get; internal set; } = true;

        /// <summary>A separate-colonies game lets each colony play its own faction (when every faction is unlocked here).</summary>
        public static bool MixedRequested { get; internal set; }

        /// <summary>
        /// The choice for the world being made now: a waiting room's (its setup), else the Game Mode page's. Separate
        /// science only in a separate-colonies game.
        /// </summary>
        public static (bool separate, bool separateScience) ForNewWorld()
        {
            LobbySession lobby = LobbySession.Current;
            if (lobby != null && lobby.State == LobbySessionState.CreatingWorld && !lobby.Setup.IsSave)
                return (lobby.Setup.Separate, lobby.Setup.Separate && lobby.Setup.SeparateScience);
            return (Separate, Separate && SeparateScience);
        }
    }

    /// <summary>
    /// The Game Mode page's colony checkboxes, made as the page makes its own Tutorial checkbox (NewGameModePanel.uxml:
    /// a new-game-mode-panel__setting-wrapper row holding a new-game-mode-panel__setting-toggle and a
    /// new-game-mode-panel__tutorial-label). They go in one column with the game's Tutorial row, so every checkbox lines
    /// up; the two choices that only mean something with separate colonies sit indented under it, shown only while it is
    /// ticked. Remembered on this computer, as the Tutorial checkbox is.
    /// </summary>
    public class NewGameColonyOptions : RegisteredSingleton
    {
        public const string BlockName = "BeaverBuddiesColonyOptions";
        private const string SeparateKey = "BeaverBuddies.NewGame.SeparateColonies";
        private const string ScienceKey = "BeaverBuddies.NewGame.SeparateScience";
        private const string MixedKey = "BeaverBuddies.NewGame.MixedFactions";
        // A checkbox's width (25 px) and its right padding (3 px): an indented row's box sits under its parent's label.
        private const float Indent = 28;

        /// <summary>The main-menu classes the rows use (all in MainMenuMiscStyle, which the menu loads).</summary>
        public static readonly string[] ClassesUsed =
        {
            "new-game-mode-panel__setting-wrapper", "new-game-mode-panel__setting-toggle", "new-game-mode-panel__tutorial-label",
        };

        private readonly ISettings _settings;
        private readonly VisualElementInitializer _initializer;
        private readonly ITooltipRegistrar _tooltipRegistrar;

        private VisualElement block;
        private Toggle separate, science, mixed;
        private VisualElement scienceRow, mixedRow;
        private Label mixedLabel;
        private string mixedLocked;
        private bool mixedNotTwo;

        public static NewGameColonyOptions Instance => SingletonManager.GetSingleton<NewGameColonyOptions>();

        public NewGameColonyOptions(ISettings settings, VisualElementInitializer initializer, ITooltipRegistrar tooltipRegistrar)
        {
            _settings = settings;
            _initializer = initializer;
            _tooltipRegistrar = tooltipRegistrar;
            try
            {
                NewGameColonyChoice.Separate = settings.GetSafeBool(SeparateKey, true);
                NewGameColonyChoice.SeparateScience = settings.GetSafeBool(ScienceKey, true);
                NewGameColonyChoice.MixedRequested = settings.GetSafeBool(MixedKey, false);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Lobby] Could not read the new-game colony choices; using the defaults: " + error.Message);
            }
        }

        /// <summary>The Game Mode page is shown: its colony rows are added the first time, then set from the choice.</summary>
        public void Attach(VisualElement root)
        {
            if (root == null) return;
            try
            {
                if (block == null || root.Q(BlockName) == null) Build(root);
                Refresh();
            }
            catch (Exception error)
            {
                // The page must still start a game if its layout differs from what this expects (a game update).
                Plugin.LogWarning("[Lobby] Could not add the colony choices to the Game Mode page: " + error);
            }
        }

        private void Build(VisualElement root)
        {
            VisualElement tutorial = root.Q("TutorialToggleWrapper");
            VisualElement details = tutorial?.parent ?? root.Q("ModeDetails");
            if (details == null) throw new InvalidOperationException("the Game Mode page has no ModeDetails");
            block = new VisualElement { name = BlockName };
            // One column, centred as a whole and left-aligned inside: the page centres each of its rows on its own, so rows of
            // different lengths would not line up.
            block.style.flexDirection = FlexDirection.Column;
            block.style.alignItems = Align.FlexStart;
            block.style.alignSelf = Align.Center;
            VisualElement custom = details.Q("CustomModeSettings");
            int at = tutorial != null ? details.IndexOf(tutorial) : custom != null && custom.parent == details ? details.IndexOf(custom) : details.childCount;
            details.Insert(at, block);
            // The game's own Tutorial row first (the game still shows and hides it: its controller holds the element).
            if (tutorial != null) block.Add(tutorial);

            separate = Row("BeaverBuddies.NewGame.SeparateColonies", indent: false, out VisualElement separateRow, out _);
            science = Row("BeaverBuddies.NewGame.SeparateScience", indent: true, out scienceRow, out _);
            mixed = Row("BeaverBuddies.NewGame.MixedFactions", indent: true, out mixedRow, out mixedLabel);
            _tooltipRegistrar.Register(separateRow, RegisteredLocalizationService.T("BeaverBuddies.NewGame.SeparateColonies.Tooltip"));
            _tooltipRegistrar.Register(scienceRow, RegisteredLocalizationService.T("BeaverBuddies.NewGame.SeparateScience.Tooltip"));
            _tooltipRegistrar.Register(mixedRow, MixedTooltip);

            separate.RegisterValueChangedCallback(change =>
            {
                NewGameColonyChoice.Separate = change.newValue;
                Remember(SeparateKey, change.newValue);
                Refresh();
            });
            science.RegisterValueChangedCallback(change =>
            {
                NewGameColonyChoice.SeparateScience = change.newValue;
                Remember(ScienceKey, change.newValue);
            });
            mixed.RegisterValueChangedCallback(change =>
            {
                NewGameColonyChoice.MixedRequested = change.newValue;
                Remember(MixedKey, change.newValue);
            });
        }

        // A row as the page's Tutorial row: the checkbox, then its label.
        private Toggle Row(string textKey, bool indent, out VisualElement row, out Label label)
        {
            row = new VisualElement();
            row.AddToClassList("new-game-mode-panel__setting-wrapper");
            if (indent) row.style.marginLeft = Indent;
            var toggle = new Toggle();
            toggle.AddToClassList("new-game-mode-panel__setting-toggle");
            label = new Label(RegisteredLocalizationService.T(textKey));
            label.AddToClassList("new-game-mode-panel__tutorial-label");
            row.Add(toggle);
            row.Add(label);
            block.Add(row);
            // Each row alone (never the column): the game's Tutorial row in it is already initialised, and a second pass
            // would give its checkbox a second click sound.
            _initializer.InitializeVisualElement(row);
            return toggle;
        }

        private void Refresh()
        {
            if (block == null) return;
            bool separated = NewGameColonyChoice.Separate;
            separate.SetValueWithoutNotify(separated);
            science.SetValueWithoutNotify(NewGameColonyChoice.SeparateScience);
            scienceRow.ToggleDisplayStyle(separated);
            // Mixed factions needs both factions unlocked here, and exactly the game's two (D1, C-C1). Otherwise the box is
            // greyed and its tooltip says why; the row itself stays hoverable.
            bool possible = NewGameFactionCapture.Instance?.MixedPossible(out mixedLocked, out mixedNotTwo) ?? false;
            mixedRow.ToggleDisplayStyle(separated);
            mixed.SetEnabled(possible);
            mixedLabel.style.opacity = possible ? 1f : 0.5f;
            mixed.SetValueWithoutNotify(possible && NewGameColonyChoice.MixedRequested);
        }

        private string MixedTooltip()
        {
            if (mixedLocked != null) return RegisteredLocalizationService.T("BeaverBuddies.NewGame.MixedFactions.Locked", mixedLocked);
            if (mixedNotTwo) return RegisteredLocalizationService.T("BeaverBuddies.NewGame.MixedFactions.NotTwo");
            return RegisteredLocalizationService.T("BeaverBuddies.NewGame.MixedFactions.Tooltip");
        }

        private void Remember(string key, bool value)
        {
            try { _settings.SetBool(key, value); }
            catch (Exception error) { Plugin.LogWarning("[Lobby] Could not remember a new-game colony choice: " + error.Message); }
        }
    }
}
