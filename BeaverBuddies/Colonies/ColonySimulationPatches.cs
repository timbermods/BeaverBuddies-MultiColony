using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.DistributionSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.GameDistrictsMigration;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;

namespace BeaverBuddies.Colonies
{
    // These change the simulation, so they run the same way on every computer and read only saved state: the mode
    // (ColonyModeService), each district center's owner (DistrictOwner) and the colony a beaver without a district last
    // left (ColonyCitizens). With the mode off they do nothing.

    static class ColonyDistricts
    {
        /// <summary>Whether automatic migration may move beavers between these two districts: only within a colony.</summary>
        public static bool SameColony(DistrictCenter a, DistrictCenter b) =>
            ColonyModeState.SameOwner(DistrictOwner.OwnerOfDistrict(a), DistrictOwner.OwnerOfDistrict(b));
    }

    [ManualMethodOverwrite]
    /*
     * 9/20/2026 (Timberborn 1.1.2.4)
        PopulationDistributor populationDistributor2 = null;
        foreach (DistrictCenter item in _districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
        {
            PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
            if (other.CanEmigrate && (populationDistributor2 == null || populationDistributor2.Spare < other.Spare))
                populationDistributor2 = other;
        }
        return populationDistributor2;
     */
    // Districts joined by a District Crossing are connected, so the game would move beavers between two players'
    // colonies on its own. Same loop, same order; a district of another colony is skipped. (Moving beavers by hand
    // to another colony is refused too, by the migration rule: beavers change colony only through a Trading Post.)
    [HarmonyPatch(typeof(MigrationNeighbours), nameof(MigrationNeighbours.GetHighestSpareNeighbour))]
    static class MigrationNeighboursHighestSparePatcher
    {
        static bool Prefix(MigrationNeighbours __instance, PopulationDistributor populationDistributor, ref PopulationDistributor __result)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            PopulationDistributor best = null;
            foreach (DistrictCenter item in __instance._districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
            {
                if (!ColonyDistricts.SameColony(populationDistributor.DistrictCenter, item)) continue;
                PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
                if (other.CanEmigrate && (best == null || best.Spare < other.Spare))
                    best = other;
            }
            __result = best;
            return false;
        }
    }

    [ManualMethodOverwrite]
    /*
     * 9/20/2026 (Timberborn 1.1.2.4)
        PopulationDistributor populationDistributor2 = null;
        foreach (DistrictCenter item in _districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
        {
            PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
            if (other.CanImmigrate && (populationDistributor2 == null || populationDistributor2.Spare > other.Spare))
                populationDistributor2 = other;
        }
        return populationDistributor2;
     */
    [HarmonyPatch(typeof(MigrationNeighbours), nameof(MigrationNeighbours.GetLowestSpareNeighbour))]
    static class MigrationNeighboursLowestSparePatcher
    {
        static bool Prefix(MigrationNeighbours __instance, PopulationDistributor populationDistributor, ref PopulationDistributor __result)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            PopulationDistributor best = null;
            foreach (DistrictCenter item in __instance._districtConnections.GetDistrictsConnectedWith(populationDistributor.DistrictCenter))
            {
                if (!ColonyDistricts.SameColony(populationDistributor.DistrictCenter, item)) continue;
                PopulationDistributor other = populationDistributor.GetOtherDistrictPopulationDistributor(item);
                if (other.CanImmigrate && (best == null || best.Spare > other.Spare))
                    best = other;
            }
            __result = best;
            return false;
        }
    }

    /// <summary>
    /// The colony each beaver or bot was in when it last left a district, kept for when it has none. The game gives one
    /// with no district (its district center deleted, or cut off from it by a blast or a flood) to the nearest district
    /// center it can walk to, whoever's (DistrictCitizenAssigner): a colony that deleted a district center near another
    /// colony's gave its beavers away, across factions too, though beavers change colony only through a Trading Post.
    /// Recorded as it leaves (Citizen.UnassignDistrict, every way out of a district), in the simulation, and saved for
    /// those still without one. Separate colonies only. E-8 of the 1.4.0-rc1 review.
    /// </summary>
    public class ColonyCitizens : RegisteredSingleton, ISaveableSingleton, ILoadableSingleton
    {
        private static readonly SingletonKey CitizensKey = new SingletonKey("BeaverBuddies.ColonyCitizens");
        private static readonly PropertyKey<string> LastColonyKey = new PropertyKey<string>("LastColony");

        private readonly ISingletonLoader _singletonLoader;
        private readonly EntityRegistry _entityRegistry;

        // Citizen -> the colony of the district it last left. Read only while it has no district: it is written again
        // each time it leaves one, so an entry left from before is never read.
        private readonly Dictionary<Guid, int> lastColony = new Dictionary<Guid, int>();

        public static ColonyCitizens Instance => SingletonManager.GetSingleton<ColonyCitizens>();

        public ColonyCitizens(ISingletonLoader singletonLoader, EntityRegistry entityRegistry)
        {
            _singletonLoader = singletonLoader;
            _entityRegistry = entityRegistry;
        }

        public void Load()
        {
            if (!_singletonLoader.TryGetSingleton(CitizensKey, out IObjectLoader loader) || !loader.Has(LastColonyKey)) return;
            foreach (KeyValuePair<Guid, int> pair in JournalFilter.Decode(loader.Get(LastColonyKey))) lastColony[pair.Key] = pair.Value;
        }

        public void Save(ISingletonSaver singletonSaver)
        {
            // Separate colonies only: a shared game's save holds only what the Stability Fork's does.
            if (!ColonyModeService.IsSeparateColonies) return;
            // Only those still without a district, in a fixed order: every other one is recorded again when it leaves.
            var saved = new List<KeyValuePair<Guid, int>>();
            foreach (KeyValuePair<Guid, int> pair in lastColony.OrderBy(p => p.Key))
            {
                Citizen citizen = _entityRegistry.GetEntity(pair.Key)?.GetComponent<Citizen>();
                if (citizen != null && !citizen.HasAssignedDistrict) saved.Add(pair);
            }
            if (saved.Count > 0) singletonSaver.GetSingleton(CitizensKey).Set(LastColonyKey, JournalFilter.Encode(saved));
        }

        /// <summary>A citizen leaves a district of <paramref name="colony"/>'s (played on every computer, in the tick).</summary>
        internal void Left(Guid citizen, int? colony)
        {
            if (colony != null) lastColony[citizen] = colony.Value;
            else lastColony.Remove(citizen);
        }

        /// <summary>The colony a citizen was in when it last left a district, or null (it never had one).</summary>
        internal int? LastColonyOf(Guid citizen) => lastColony.TryGetValue(citizen, out int slot) ? slot : (int?)null;

        /// <summary>A colony handed over: its beavers without a district are the new owner's too.</summary>
        internal void Transfer(int from, int to)
        {
            foreach (Guid citizen in lastColony.Where(p => p.Value == from).Select(p => p.Key).ToList()) lastColony[citizen] = to;
        }
    }

    // Every way out of a district goes through the private Citizen.UnassignDistrict (a RuntimeCheck pins it): the colony
    // left is read there, from the district center itself, which still says whose it was as it is deleted. Read only: the
    // game's own method runs as it would. Inside the tick: whatever happens here, the game carries on.
    [HarmonyPatch(typeof(Citizen), nameof(Citizen.UnassignDistrict))]
    static class ColonyCitizenLeavePatcher
    {
        static void Prefix(Citizen __instance)
        {
            if (!ColonyModeService.IsSeparateColonies || __instance.AssignedDistrict is null) return;
            try
            {
                EntityComponent entity = __instance.GetComponent<EntityComponent>();
                if (entity != null) ColonyCitizens.Instance?.Left(entity.EntityId, __instance.AssignedDistrict.GetComponent<DistrictOwner>()?.Slot);
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Colony] Could not note the colony a beaver left: " + error.Message);
            }
        }
    }

    [ManualMethodOverwrite]
    /*
     * 9/23/2026 (Timberborn 1.1.2.4), DistrictCitizenAssigner.AssignToClosestDistrict
        DistrictCenter districtCenter = null;
        float num = float.PositiveInfinity;
        foreach (DistrictCenter finishedDistrictCenter in _districtCenterRegistry.FinishedDistrictCenters)
        {
            if (finishedDistrictCenter.IsGloballyReachableFromCitizen(citizen))
            {
                float num2 = finishedDistrictCenter.DistanceToCitizen(citizen);
                if (num2 < num)
                {
                    districtCenter = finishedDistrictCenter;
                    num = num2;
                }
            }
        }
        if ((bool)districtCenter)
        {
            citizen.AssignDistrict(districtCenter);
        }
     */
    // A beaver or bot with no district joins the nearest district center it can walk to: the same loop, in the same
    // order, over its own colony's only (the one it last left, ColonyCitizens). One that never had a district, as a new
    // game's start, joins the nearest, as in the game. With none of its colony's in reach it waits, as a beaver with no
    // district in reach does in the game: until one is (its player founds a colony again, Ctrl+K), or its colony is
    // handed over, and its beavers with it. E-8 of the 1.4.0-rc1 review.
    [HarmonyPatch(typeof(DistrictCitizenAssigner), nameof(DistrictCitizenAssigner.AssignToClosestDistrict))]
    static class ColonyCitizenAssignerPatcher
    {
        [HarmonyPriority(Priority.Last)]
        static bool Prefix(DistrictCitizenAssigner __instance, Citizen citizen)
        {
            if (!ColonyModeService.IsSeparateColonies) return true;
            EntityComponent entity = citizen.GetComponent<EntityComponent>();
            int? colony = entity == null ? null : ColonyCitizens.Instance?.LastColonyOf(entity.EntityId);
            if (colony == null) return true;
            DistrictCenter nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (DistrictCenter districtCenter in __instance._districtCenterRegistry.FinishedDistrictCenters)
            {
                if (!ColonyModeState.MayJoin(colony, DistrictOwner.OwnerOfDistrict(districtCenter))) continue;
                if (districtCenter.IsGloballyReachableFromCitizen(citizen))
                {
                    float distance = districtCenter.DistanceToCitizen(citizen);
                    if (distance < nearestDistance)
                    {
                        nearest = districtCenter;
                        nearestDistance = distance;
                    }
                }
            }
            if ((bool)nearest) citizen.AssignDistrict(nearest);
            return false;
        }
    }
}
