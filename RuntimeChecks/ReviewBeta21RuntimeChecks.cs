#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// The review of 1.4.0-beta18 to beta20 (design/REVIEW-FINDINGS-1.4.0-beta18-20.md), against the compiled mod and the
// game: the facts the faction switch's fixes rest on (B-1, B-2), read from the game's IL, and that the switch uses them.
internal static class ReviewBeta21RuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");
        bool Calls(MethodBase method, string type, string name) =>
            IlScan.Instructions(method).Any(i => i.Calls && i.Is(type, name));

        test("B-1: the game lets go of a character only when it is killed, and DestroyCharacter kills it before deleting it", () =>
        {
            Type character = Game("Timberborn.Characters", "Timberborn.Characters.Character");
            var destroy = IlScan.Instructions(Only(character, "DestroyCharacter"));
            int kill = destroy.FindIndex(i => i.Calls && i.Is("Timberborn.Characters.Character", "KillCharacter"));
            int delete = destroy.FindIndex(i => i.Calls && i.Member?.Name == "Delete" && i.Member.DeclaringType?.Name == "EntityService");
            if (kill < 0 || delete < 0 || kill > delete) throw new Exception("Character.DestroyCharacter no longer kills, then deletes");
            if (!IlScan.Instructions(Only(character, "KillCharacter")).Any(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "CharacterKilledEvent"))
                throw new Exception("Character.KillCharacter no longer posts CharacterKilledEvent");
            Type population = Game("Timberborn.Characters", "Timberborn.Characters.CharacterPopulation");
            if (!population.GetMethods(All).Any(m => m.GetParameters().Any(p => p.ParameterType.Name == "CharacterKilledEvent")))
                throw new Exception("CharacterPopulation no longer listens for CharacterKilledEvent");
            if (population.GetMethods(All).Any(m => m.GetParameters().Any(p => p.ParameterType.Name == "EntityDeletedEvent")))
                throw new Exception("CharacterPopulation now lets go of deleted entities too: B-1's premise changed (harmless, but re-check)");
            // The switch removes the old beavers the game's way.
            MethodInfo remove = mod.GetType("BeaverBuddies.Colonies.ColonyFoundingService", true)!.GetMethod("RemoveBeaver", All)
                ?? throw new Exception("ColonyFoundingService.RemoveBeaver is gone");
            if (!Calls(remove, "Timberborn.Characters.Character", "DestroyCharacter"))
                throw new Exception("the faction switch no longer removes beavers with Character.DestroyCharacter");
        });

        test("B-2: a new game's beavers have no district until a tick runs, and the switch counts those still waiting", () =>
        {
            Type citizen = Game("Timberborn.GameDistricts", "Timberborn.GameDistricts.Citizen");
            var init = IlScan.Instructions(Only(citizen, "InitializeEntity"));
            if (!init.Any(i => i.Calls && i.Member?.DeclaringType?.Name == "UnassignedCitizenRegistry"))
                throw new Exception("Citizen.InitializeEntity no longer waits in UnassignedCitizenRegistry: B-2's premise changed");
            MethodInfo switchFaction = mod.GetType("BeaverBuddies.Colonies.ColonyFoundingService", true)!.GetMethod("SwitchFaction", All)
                ?? throw new Exception("ColonyFoundingService.SwitchFaction is gone");
            if (!Calls(switchFaction, "Timberborn.GameDistricts.Citizen", "get_HasAssignedDistrict"))
                throw new Exception("the faction switch no longer counts the colony's beavers still waiting for a district");
        });
    }
}
