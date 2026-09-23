using BeaverBuddies.Colonies;
using HarmonyLib;
using System;
using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.Beavers;
using Timberborn.BeaversUI;
using Timberborn.BotsUI;
using Timberborn.BotUpkeep;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.Reproduction;
using Timberborn.WorldPersistence;
using Timberborn.WorldSerialization;

namespace BeaverBuddies.Factions
{
    /// <summary>
    /// A beaver's faction in a mixed game (D12): the Folktails and Iron Teeth beavers share one template, so the faction
    /// is kept on the beaver itself, set when it is made (D13) and saved. Needs are built in Awake, before Load, inside
    /// the call that makes the beaver, so the faction is read from <see cref="FactionCreationContext"/> the first time it
    /// is asked for; a loaded beaver's context holds what its save says (WorldEntitiesLoaderFactionPatcher).
    /// </summary>
    public class CharacterFaction : BaseComponent, IPersistentEntity
    {
        private static readonly ComponentKey Key = new ComponentKey("BeaverBuddies.CharacterFaction");
        private static readonly PropertyKey<string> FactionKey = new PropertyKey<string>("Faction");

        private string factionId;

        public string FactionId => factionId ??= FactionCreationContext.Current ?? Unknown();

        private static string Unknown()
        {
            if (MixedFactions.IsOn) FactionCreationContext.WarnOnce("A beaver");
            return MixedFactions.BaseFaction;
        }

        public void Save(IEntitySaver entitySaver)
        {
            if (!MixedFactions.IsOn || FactionId == null) return;
            entitySaver.GetComponent(Key).Set(FactionKey, FactionId);
        }

        public void Load(IEntityLoader entityLoader)
        {
            if (!MixedFactions.IsOn || !entityLoader.TryGetComponent(Key, out IObjectLoader loader) || !loader.Has(FactionKey)) return;
            string saved = loader.Get(FactionKey);
            if (factionId != null && factionId != saved)
                Plugin.LogWarning($"[Factions] A beaver was made as {factionId} but its save says {saved}; keeping the save's");
            factionId = saved;
        }

        /// <summary>What a loaded beaver's save says its faction is (read before it exists), or null.</summary>
        internal static string SavedFaction(SerializedEntity serializedEntity)
        {
            var loader = new EntityLoader(serializedEntity);
            return loader.TryGetComponent(Key, out IObjectLoader component) && component.Has(FactionKey) ? component.Get(FactionKey) : null;
        }
    }

    /// <summary>
    /// Which faction the character being made right now is (D13): pushed around each place the game makes one, popped in
    /// a finally. A stack, so a creation inside another is safe. Main thread only, like every entity creation.
    /// </summary>
    public static class FactionCreationContext
    {
        [ThreadStatic] private static Stack<string> stack;
        [ThreadStatic] private static bool warned;

        public static string Current => stack != null && stack.Count > 0 ? stack.Peek() : null;

        public static Scope Push(string faction)
        {
            stack ??= new Stack<string>();
            stack.Push(faction);
            return new Scope(true);
        }

        /// <summary>A new scene (main thread): nothing pushed is left, and the warning may be given again.</summary>
        internal static void Reset()
        {
            stack?.Clear();
            warned = false;
        }

        internal static void Pop()
        {
            if (stack != null && stack.Count > 0) stack.Pop();
        }

        /// <summary>A character was made in a mixed game with no faction in hand (the base faction was used).</summary>
        internal static void WarnOnce(string what)
        {
            if (warned) return;
            warned = true;
            Plugin.LogWarning($"[Factions] {what} was made with no faction in hand; it takes the base faction");
        }

        public readonly struct Scope : IDisposable
        {
            private readonly bool pushed;
            internal Scope(bool pushed) => this.pushed = pushed;
            public void Dispose()
            {
                if (pushed) Pop();
            }
        }
    }

    // ---- Where the game makes characters (D13) ----

    /*
     * 2026-09-22, Timberborn 1.1.2.4, WorldPersistence: WorldEntitiesLoader.InstantiateEntity
        if (TryInstantiateEntity(templateName, serializedEntity.Id, out var instance)) ...
     * Instantiating runs Awake (NeedManager builds its needs there) before Load: the saved faction goes in first.
     */
    [HarmonyPatch(typeof(WorldEntitiesLoader), "InstantiateEntity")]
    static class WorldEntitiesLoaderFactionPatcher
    {
        static void Prefix(SerializedEntity serializedEntity, out bool __state)
        {
            __state = false;
            if (!MixedFactions.IsOn) return;
            string faction = CharacterFaction.SavedFaction(serializedEntity);
            if (faction == null) return;
            FactionCreationContext.Push(faction);
            __state = true;
        }

        static void Finalizer(bool __state)
        {
            if (__state && MixedFactions.IsOn) FactionCreationContext.Pop();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Reproduction: NewbornSpawner.SpawnAdult / SpawnChild(BaseComponent spawner)
        _beaverFactory.CreateNewbornAdult(valueOrDefault, CreateInitComponent(spawner));
     * A beaver born in a lodge or breeding pod is that building's faction. Both methods, through TargetMethods: two
     * [HarmonyPatch] attributes on one patch method merge into one target (the last name wins), not two.
     */
    [HarmonyPatch]
    static class NewbornSpawnerFactionPatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(NewbornSpawner), nameof(NewbornSpawner.SpawnAdult));
            yield return AccessTools.Method(typeof(NewbornSpawner), nameof(NewbornSpawner.SpawnChild));
        }

        static void Prefix(BaseComponent spawner, out bool __state)
        {
            __state = false;
            if (!MixedFactions.IsOn) return;
            FactionCreationContext.Push(ColonyFactionService.SimFactionOf(spawner) ?? MixedFactions.BaseFaction);
            __state = true;
        }

        static void Finalizer(bool __state)
        {
            if (__state && MixedFactions.IsOn) FactionCreationContext.Pop();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, Beavers: BeaverFactory.CreateAdultFromChild(Child child)
     * A child grows up into a beaver of its own faction.
     */
    [HarmonyPatch(typeof(BeaverFactory), nameof(BeaverFactory.CreateAdultFromChild))]
    static class BeaverGrowUpFactionPatcher
    {
        static void Prefix(Child child, out bool __state)
        {
            __state = false;
            if (!MixedFactions.IsOn) return;
            FactionCreationContext.Push(child?.GetComponent<CharacterFaction>()?.FactionId ?? MixedFactions.BaseFaction);
            __state = true;
        }

        static void Finalizer(bool __state)
        {
            if (__state && MixedFactions.IsOn) FactionCreationContext.Pop();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, BotsUpkeep: BotManufactory.OnProductionFinished
        _botFactory.Create(_buildingAccessible.CalculateAccessFromLocalAccess(), _enterable.ExitWorldSpaceRotation, initComponent);
     * A bot assembler makes bots of its own faction (the bot factory picks the template by the context).
     */
    [HarmonyPatch(typeof(BotManufactory), "OnProductionFinished")]
    static class BotManufactoryFactionPatcher
    {
        static void Prefix(BotManufactory __instance, out bool __state)
        {
            __state = false;
            if (!MixedFactions.IsOn) return;
            FactionCreationContext.Push(ColonyFactionService.SimFactionOf(__instance) ?? MixedFactions.BaseFaction);
            __state = true;
        }

        static void Finalizer(bool __state)
        {
            if (__state && MixedFactions.IsOn) FactionCreationContext.Pop();
        }
    }

    /*
     * 2026-09-22, Timberborn 1.1.2.4, BeaversUI: BeaverGeneratorTool.PlaceBeavers(bool isChild, int count), and BotsUI:
     * BotGeneratorTool.PlaceBots(int count) (dev mode): the beavers and bots are the local colony's faction.
     */
    [HarmonyPatch]
    static class DevCharacterFactionPatcher
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BeaverGeneratorTool), "PlaceBeavers");
            yield return AccessTools.Method(typeof(BotGeneratorTool), "PlaceBots");
        }

        static void Prefix(out bool __state)
        {
            __state = false;
            if (!MixedFactions.IsOn) return;
            FactionCreationContext.Push(ColonyFactionService.LocalFaction);
            __state = true;
        }

        static void Finalizer(bool __state)
        {
            if (__state && MixedFactions.IsOn) FactionCreationContext.Pop();
        }
    }
}
