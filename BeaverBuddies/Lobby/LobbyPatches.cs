using BeaverBuddies.Connect;
using BeaverBuddies.Util;
using HarmonyLib;
using Timberborn.MainMenuPanels;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// Host co-op game beside the Game Mode page's Start: a copy of Start's own button (LocalizableButton, menu-button
    /// menu-button--large-text), as Host co-op game is a copy of Load on the Load Game box. It opens a waiting room for the
    /// game the page is set up for (LobbyHostPanel).
    /// </summary>
    [HarmonyPatch(typeof(NewGameModePanel), nameof(NewGameModePanel.GetPanel))]
    public class NewGameModePanelHostButtonPatcher
    {
        public const string ButtonName = "HostCoopButton";

        public static void Postfix(NewGameModePanel __instance, VisualElement __result)
        {
            if (__result == null) return;
            Button host = ButtonInserter.DuplicateOrGetButton(__result, "NextButton", ButtonName, button =>
            {
                button.text = RegisteredLocalizationService.T("BeaverBuddies.Saving.HostCoopGame");
                button.clicked += () => SingletonManager.GetSingleton<LobbyHostPanel>()?.OpenFrom(__instance);
            });
            host?.SetEnabled(__instance._nextButton?.enabledSelf ?? true);
        }
    }

    [ManualMethodOverwrite]
    /*
     * 2026-09-22, Timberborn 1.1.2.4, MainMenuPanels: NewGameModePanel.UpdateNextButton
        _nextButton.SetEnabled(TryGetValidatedGameMode(out var _));
     */
    /// <summary>Host co-op game is available exactly when Start is (an invalid custom mode greys both, natively).</summary>
    [HarmonyPatch(typeof(NewGameModePanel), "UpdateNextButton")]
    public class NewGameModePanelUpdateNextButtonPatcher
    {
        public static void Postfix(NewGameModePanel __instance)
        {
            __instance._root?.Q<Button>(NewGameModePanelHostButtonPatcher.ButtonName)?.SetEnabled(__instance._nextButton.enabledSelf);
        }
    }
}
