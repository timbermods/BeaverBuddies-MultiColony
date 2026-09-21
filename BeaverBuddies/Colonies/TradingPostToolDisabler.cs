using Timberborn.BlockObjectTools;
using Timberborn.ToolSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// The Trading Post's toolbar button shows only in a separate-colonies game: elsewhere there is nobody to trade with.
    /// The game asks every tool disabler whenever it draws a tool's button (and when its group opens), so the button
    /// follows the mode. Dev mode shows every tool, as the game does.
    /// </summary>
    public class TradingPostToolDisabler : IToolDisabler
    {
        public bool IsEnabled(ITool tool) =>
            !(tool is BlockObjectTool blockObjectTool) || ColonyModeService.IsSeparateColonies
            || blockObjectTool.Template?.HasSpec<MultiColonyTradingPostSpec>() != true;
    }
}
