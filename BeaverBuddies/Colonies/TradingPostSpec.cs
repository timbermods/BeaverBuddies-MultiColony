using Timberborn.BlueprintSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// Marks the Trading Post building (Buildings/DistrictManagement/MultiColonyTradingPost): a District Crossing's
    /// model and workings that trades between two colonies instead of balancing goods between districts. The game finds
    /// spec types by their class name across every loaded assembly, and two with one name would stop its specs loading,
    /// so the name is this mod's own.
    /// </summary>
    public record MultiColonyTradingPostSpec : ComponentSpec
    {
    }
}
