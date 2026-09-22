using HarmonyLib;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;
using Timberborn.GameDistrictsMigrationBatchControl;
using UnityEngine.UIElements;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The Migration tab (F7) lists another colony's district once it is chosen in the window's district list, and a
    /// Trading Post links the two colonies' districts, so its manual migration panel can show one of each. Every change
    /// there to another colony's district is refused (ColonyRules: the Migration and Entities scopes, on the player's own
    /// computer and on the host), but the game still let its buttons be clicked, and its automatic migration toggles went
    /// on showing the click: it sets them only when the row is made, so a refused toggle looked changed. Here those
    /// controls are greyed out on a district that is not this player's colony's, whose toggles then show the district's
    /// real setting (its owner's changes too). This player's own rows stay the game's: set from the toggle at once,
    /// played a tick or a round trip later. Display only: what may be changed is still decided by the rules.
    /// </summary>
    static class ColonyMigrationControls
    {
        /// <summary>This player's colony's district (or one nobody owns): its migration may be changed from here.</summary>
        internal static bool MayChange(DistrictCenter district) => !district || ColonyViewService.IsOwnDistrict(district);

        internal static void ShowSetting(VisualElement row, string toggleName, bool value)
        {
            Toggle toggle = row.Q<Toggle>(toggleName);
            if (toggle != null && toggle.value != value) toggle.SetValueWithoutNotify(value);
        }

        internal static void Enable(VisualElement element, bool enabled)
        {
            if (element != null && element.enabledSelf != enabled) element.SetEnabled(enabled);
        }
    }

    // Automatic migration: a district's minimum (the field, − and +) and whether it takes in or sends out beavers.
    [HarmonyPatch(typeof(PopulationDistributorBatchControlRowItem), nameof(PopulationDistributorBatchControlRowItem.UpdateRowItem))]
    static class ColonyMigrationSettingsRowPatcher
    {
        static void Postfix(PopulationDistributorBatchControlRowItem __instance)
        {
            if (!ColonyViewService.Active) return;
            PopulationDistributor distributor = __instance._populationDistributor;
            if (distributor == null) return;
            bool mayChange = ColonyMigrationControls.MayChange(distributor.DistrictCenter);
            ColonyMigrationControls.Enable(__instance.Root, mayChange);
            if (mayChange) return;
            ColonyMigrationControls.ShowSetting(__instance.Root, "ImmigrationToggle", distributor.AllowImmigration);
            ColonyMigrationControls.ShowSetting(__instance.Root, "EmigrationToggle", distributor.AllowEmigration);
        }
    }

    // Manual migration: the 1, 10 and all buttons between the two districts the panel shows.
    [HarmonyPatch(typeof(ManualMigrationPopulationRow), nameof(ManualMigrationPopulationRow.SetButtonsEnabledState))]
    static class ColonyManualMigrationButtonsPatcher
    {
        static void Postfix(ManualMigrationPopulationRow __instance)
        {
            if (!ColonyViewService.Active || __instance._populationDistributor == null) return;
            if (ColonyMigrationControls.MayChange(__instance._populationDistributor.DistrictCenter)
                && ColonyMigrationControls.MayChange(__instance._target)) return;
            ColonyMigrationControls.Enable(__instance._buttonOne, false);
            ColonyMigrationControls.Enable(__instance._buttonTen, false);
            ColonyMigrationControls.Enable(__instance._buttonAll, false);
        }
    }
}
