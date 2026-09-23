using HarmonyLib;
using System.Runtime.CompilerServices;
using Timberborn.AutomationBuildings;
using Timberborn.Common;
using Timberborn.GameDistricts;
using Timberborn.Population;

namespace BeaverBuddies.Colonies
{
    /// <summary>
    /// A Population Counter set to count the whole map (its "global" toggle) counts its own colony in a separate-colonies
    /// game (1.4.0-rc1, A4): the districts its colony owns, added up from the same per-district figures the game samples
    /// for a counter that counts one district. A colony is its districts, and one colony's automation reads its own
    /// colony (as its Science Counter reads its own science and its Chronometer its own working hours); until 1.4.0-rc1 a
    /// colony's counter counted every colony's beavers, bots, beds and jobs. The same on every computer: the figures are
    /// those the automation tick sampled, and the districts' owners are part of the shared game. A shared-colony game, and
    /// a counter set to its district, are unchanged. A counter with no owner at all (a building from a save before the
    /// colonies were split, cut off from every road) keeps the game's count.
    /// </summary>
    [ManualMethodOverwrite]
    /*
     * 2026-09-23 (Timberborn 1.1.2.4, PopulationCounter.Sample)
        if (GlobalMode)
        {
            _sampledPopulationData = _samplingPopulationService.GlobalPopulationData;
        }
        else
        {
            DistrictCenter instantOrConstructionDistrict = _districtBuilding.GetInstantOrConstructionDistrict();
            _sampledPopulationData = (instantOrConstructionDistrict ? _samplingPopulationService.GetDistrictData(instantOrConstructionDistrict) : _emptyPopulationData);
        }
        UpdateOutputState();
     */
    [HarmonyPatch(typeof(PopulationCounter), nameof(PopulationCounter.Sample))]
    static class ColonyPopulationCounterPatcher
    {
        internal static readonly ColonyProfiler.Spot Spot = ColonyProfiler.Declare("Population counters (colony)");

        // Each counter's own colony figures, kept with the counter (and gone with it).
        private static readonly ConditionalWeakTable<PopulationCounter, PopulationData> colonyData =
            new ConditionalWeakTable<PopulationCounter, PopulationData>();

        [HarmonyPriority(Priority.Last)]
        static bool Prefix(PopulationCounter __instance)
        {
            if (!ColonyModeService.IsSeparateColonies || !__instance.GlobalMode) return true;
            int? slot = ColonySeparation.SimOwnerOf(__instance);
            if (slot == null) return true;
            long started = ColonyProfiler.Start();
            PopulationData data = colonyData.GetValue(__instance, _ => new PopulationData());
            SamplingPopulationService sampling = __instance._samplingPopulationService;
            AddUp(sampling, slot.Value, data);
            __instance._sampledPopulationData = data;
            __instance.UpdateOutputState();
            ColonyProfiler.Stop(Spot, started);
            return false;
        }

        /// <summary>
        /// Writes into <paramref name="total"/> the sum of the sampled figures of the district centers colony
        /// <paramref name="slot"/> owns. Integer sums: the districts' order changes nothing.
        /// </summary>
        internal static void AddUp(SamplingPopulationService sampling, int slot, PopulationData total)
        {
            ReadOnlyList<DistrictCenter> districtCenters = sampling._districtCenterRegistry.FinishedDistrictCenters;
            int adults = 0, children = 0, bots = 0;
            int beaverEmployable = 0, beaverUnemployable = 0, botEmployable = 0, botUnemployable = 0;
            int occupiedBeds = 0, freeBeds = 0, homeless = 0;
            int beaverOccupied = 0, beaverFree = 0, beaverUnemployed = 0, botOccupied = 0, botFree = 0, botUnemployed = 0;
            int contaminatedAdults = 0, contaminatedChildren = 0;
            for (int i = 0; i < districtCenters.Count; i++)
            {
                DistrictCenter districtCenter = districtCenters[i];
                if (DistrictOwner.OwnerOfDistrict(districtCenter) != slot) continue;
                PopulationData d = sampling.GetDistrictData(districtCenter);
                if (d == null) continue;
                adults += d.NumberOfAdults;
                children += d.NumberOfChildren;
                bots += d.NumberOfBots;
                beaverEmployable += d.BeaverWorkforceData.Employable;
                beaverUnemployable += d.BeaverWorkforceData.Unemployable;
                botEmployable += d.BotWorkforceData.Employable;
                botUnemployable += d.BotWorkforceData.Unemployable;
                occupiedBeds += d.BedData.OccupiedBeds;
                freeBeds += d.BedData.FreeBeds;
                homeless += d.BedData.Homeless;
                beaverOccupied += d.BeaverWorkplaceData.OccupiedWorkslots;
                beaverFree += d.BeaverWorkplaceData.FreeWorkslots;
                beaverUnemployed += d.BeaverWorkplaceData.Unemployed;
                botOccupied += d.BotWorkplaceData.OccupiedWorkslots;
                botFree += d.BotWorkplaceData.FreeWorkslots;
                botUnemployed += d.BotWorkplaceData.Unemployed;
                contaminatedAdults += d.ContaminationData.ContaminatedAdults;
                contaminatedChildren += d.ContaminationData.ContaminatedChildren;
            }
            total.Update(adults, children, bots,
                new WorkforceData(beaverEmployable, beaverUnemployable), new WorkforceData(botEmployable, botUnemployable),
                new BedData(occupiedBeds, freeBeds, homeless),
                new WorkplaceData(beaverOccupied, beaverFree, beaverUnemployed), new WorkplaceData(botOccupied, botFree, botUnemployed),
                new ContaminationData(contaminatedAdults, contaminatedChildren));
        }
    }
}
