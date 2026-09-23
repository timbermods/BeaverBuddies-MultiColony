using BeaverBuddies.Factions;
using Bindito.Core;
using Timberborn.Beavers;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.DistributionSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.GameDistricts;
using Timberborn.GoodCollectionSystem;
using Timberborn.NeedCollectionSystem;
using Timberborn.TemplateCollectionSystem;
using Timberborn.TemplateInstantiation;
using Timberborn.TimbermeshMaterials;
using Timberborn.ToolSystem;

namespace BeaverBuddies.Colonies
{
    public static class ColonyConfigurator
    {
        // Every district center carries its owner's slot, every building the colony that placed it, and every crossing
        // half its side of an exchange.
        private class TemplateModuleProvider : IProvider<TemplateModule>
        {
            public TemplateModule Get()
            {
                TemplateModule.Builder builder = new TemplateModule.Builder();
                builder.AddDecorator<DistrictCenter, DistrictOwner>();
                builder.AddDecorator<Building, ColonyStamp>();
                builder.AddDecorator<DistrictCrossing, CrossingExchange>();
                // A beaver's faction in a mixed game (both factions' beavers share one template).
                builder.AddDecorator<BeaverSpec, CharacterFaction>();
                return builder.Build();
            }
        }

        // The Trading Post's panel, at the bottom of its own (the crossing's district-distribution panels it would get are
        // hidden there: TradingPostPanelPatches.cs).
        private class EntityPanelModuleProvider : IProvider<EntityPanelModule>
        {
            private readonly TradingPostFragment _tradingPostFragment;

            public EntityPanelModuleProvider(TradingPostFragment tradingPostFragment)
            {
                _tradingPostFragment = tradingPostFragment;
            }

            public EntityPanelModule Get()
            {
                EntityPanelModule.Builder builder = new EntityPanelModule.Builder();
                builder.AddBottomFragment(_tradingPostFragment);
                return builder.Build();
            }
        }

        /// <summary>
        /// Bound in every game, co-op or not: a separate-colonies game is created before it is hosted, and a save
        /// opened alone and saved again must keep its colonies. Everything here does nothing in a shared-colony game.
        /// </summary>
        public static void Configure(IContainerDefinition containerDefinition)
        {
            MixedFactions.Reset();
            containerDefinition.Bind<DistrictOwner>().AsTransient();
            containerDefinition.Bind<CrossingExchange>().AsTransient();
            containerDefinition.Bind<ColonyStamp>().AsTransient();
            containerDefinition.MultiBind<TemplateModule>().ToProvider<TemplateModuleProvider>().AsSingleton();
            containerDefinition.Bind<ColonyModeService>().AsSingleton();
            containerDefinition.Bind<ColonySlotService>().AsSingleton();
            containerDefinition.Bind<ColonyRulesService>().AsSingleton();
            containerDefinition.Bind<ColonyFoundingService>().AsSingleton();
            containerDefinition.Bind<ColonyViewService>().AsSingleton();
            containerDefinition.Bind<ColonyJournal>().AsSingleton();
            containerDefinition.Bind<ColonyScienceService>().AsSingleton();
            containerDefinition.Bind<ColonyRoadNetworks>().AsSingleton();
            containerDefinition.Bind<ColonyTradeLedger>().AsSingleton();
            containerDefinition.Bind<ColonyExchangeService>().AsSingleton();
            containerDefinition.Bind<ColonyStamps>().AsSingleton();
            containerDefinition.Bind<ColonyMarks>().AsSingleton();
            containerDefinition.Bind<ColonyWorkingHours>().AsSingleton();
            containerDefinition.Bind<ColonyLifecycle>().AsSingleton();
            containerDefinition.Bind<ColonyRoadOverlay>().AsSingleton();
            containerDefinition.Bind<TradeOverviewPanel>().AsSingleton();
            containerDefinition.Bind<ColonyDiagnostics>().AsSingleton();
            containerDefinition.Bind<ColonyStewards>().AsSingleton();
            containerDefinition.Bind<ColonyWishlist>().AsSingleton();
            containerDefinition.Bind<ColonySupplies>().AsSingleton();
            containerDefinition.Bind<HostStartGate>().AsSingleton();
            containerDefinition.Bind<ColonyNavigation>().AsSingleton();
            containerDefinition.Bind<TradeItems>().AsSingleton();
            containerDefinition.Bind<TradingPostFragment>().AsSingleton();
            containerDefinition.MultiBind<EntityPanelModule>().ToProvider<EntityPanelModuleProvider>().AsSingleton();
            containerDefinition.MultiBind<IBlockObjectValidator>().To<ColonyPlacementValidator>().AsSingleton();
            containerDefinition.MultiBind<IToolDisabler>().To<TradingPostToolDisabler>().AsSingleton();

            // Mixed factions (a colony each of its own faction): nothing of it acts in any other game.
            containerDefinition.Bind<CharacterFaction>().AsTransient();
            containerDefinition.Bind<OtherFactionCollections>().AsSingleton();
            containerDefinition.MultiBind<ITemplateCollectionIdProvider>().ToExisting<OtherFactionCollections>();
            containerDefinition.MultiBind<IGoodCollectionIdsProvider>().ToExisting<OtherFactionCollections>();
            containerDefinition.MultiBind<INeedCollectionIdsProvider>().ToExisting<OtherFactionCollections>();
            containerDefinition.MultiBind<IMaterialCollectionIdsProvider>().ToExisting<OtherFactionCollections>();
            containerDefinition.Bind<FactionCatalog>().AsSingleton();
            containerDefinition.Bind<ColonyFactionService>().AsSingleton();
            containerDefinition.Bind<FactionToolbar>().AsSingleton();
            containerDefinition.Bind<FactionSelection>().AsSingleton();
            containerDefinition.MultiBind<IToolDisabler>().To<FactionToolDisabler>().AsSingleton();
        }
    }
}
