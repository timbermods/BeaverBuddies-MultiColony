using Bindito.Core;
using Timberborn.BlockSystem;

namespace BeaverBuddies.Colonies
{
    public static class ColonyConfigurator
    {
        /// <summary>
        /// Bound in every game, co-op or not: a separate-colonies game is created before it is hosted, and a save
        /// opened alone and saved again must keep its colonies. Everything here does nothing in a shared-colony game.
        /// </summary>
        public static void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<ColonyModeService>().AsSingleton();
            containerDefinition.Bind<ColonyRulesService>().AsSingleton();
            containerDefinition.Bind<ColonyFoundingService>().AsSingleton();
            containerDefinition.Bind<ColonyViewService>().AsSingleton();
            containerDefinition.Bind<ColonyBorderOverlay>().AsSingleton();
            containerDefinition.MultiBind<IBlockObjectValidator>().To<ColonyPlacementValidator>().AsSingleton();
        }
    }
}
