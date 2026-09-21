using Bindito.Core;
using Timberborn.BlockSystem;
using Timberborn.GameDistricts;
using Timberborn.TemplateInstantiation;

namespace BeaverBuddies.Colonies
{
    public static class ColonyConfigurator
    {
        // Every district center carries its owner's slot.
        private class TemplateModuleProvider : IProvider<TemplateModule>
        {
            public TemplateModule Get()
            {
                TemplateModule.Builder builder = new TemplateModule.Builder();
                builder.AddDecorator<DistrictCenter, DistrictOwner>();
                return builder.Build();
            }
        }

        /// <summary>
        /// Bound in every game, co-op or not: a separate-colonies game is created before it is hosted, and a save
        /// opened alone and saved again must keep its colonies. Everything here does nothing in a shared-colony game.
        /// </summary>
        public static void Configure(IContainerDefinition containerDefinition)
        {
            containerDefinition.Bind<DistrictOwner>().AsTransient();
            containerDefinition.MultiBind<TemplateModule>().ToProvider<TemplateModuleProvider>().AsSingleton();
            containerDefinition.Bind<ColonyModeService>().AsSingleton();
            containerDefinition.Bind<ColonySlotService>().AsSingleton();
            containerDefinition.Bind<ColonyRulesService>().AsSingleton();
            containerDefinition.Bind<ColonyFoundingService>().AsSingleton();
            containerDefinition.Bind<ColonyViewService>().AsSingleton();
            containerDefinition.Bind<ColonyScienceService>().AsSingleton();
            containerDefinition.Bind<ColonyRoadNetworks>().AsSingleton();
            containerDefinition.MultiBind<IBlockObjectValidator>().To<ColonyPlacementValidator>().AsSingleton();
        }
    }
}
