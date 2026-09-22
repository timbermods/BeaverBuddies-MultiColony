using HarmonyLib;
using Timberborn.BlockObjectTools;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The Trading Post's toolbar button shows only in a separate-colonies game: elsewhere there is nobody to trade with.
    /// The game asks every tool disabler whenever it draws a tool's button (and when its group opens), so the button
    /// follows the mode.
    /// </summary>
    public class TradingPostToolDisabler : IToolDisabler
    {
        public bool IsEnabled(ITool tool) => ColonyModeService.IsSeparateColonies || !IsTradingPostTool(tool);

        internal static bool IsTradingPostTool(ITool tool) =>
            tool is BlockObjectTool blockObjectTool && blockObjectTool.Template?.HasSpec<MultiColonyTradingPostSpec>() == true;
    }

    /*
     * 9/22/2026 (Timberborn 1.1.2.4), ToolButton.ToolEnabled: dev mode and the map editor turn every tool on, and the
     * disablers are only asked otherwise:
        if (!_devModeManager.Enabled && !_mapEditorMode.IsMapEditor)
            return !DevModeTool && _toolDisablers.FastAll(disabler => disabler.IsEnabled(Tool));
        return true;
     */
    // So in dev mode, too, a shared game's toolbar has no Trading Post: it stays the Stability Fork's. The map editor is
    // never a separate-colonies game, so a map gets none either (a Trading Post belongs to two colonies' players).
    [HarmonyPatch(typeof(ToolButton), nameof(ToolButton.ToolEnabled), MethodType.Getter)]
    static class TradingPostDevModeToolPatcher
    {
        static void Postfix(ToolButton __instance, ref bool __result)
        {
            if (__result && !ColonyModeService.IsSeparateColonies && TradingPostToolDisabler.IsTradingPostTool(__instance.Tool))
                __result = false;
        }
    }
}
