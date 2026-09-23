using System.Text.RegularExpressions;
using BeaverBuddies.Factions;

/// <summary>
/// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
/// that need no game: reviewer B: Folktails and Iron Teeth together in the late game, and both Wonders. One check per finding, named after it.
/// </summary>
static class RcFactionChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }

    static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root)!;
        Check(root != null, "could not find the repository root");
        return root!;
    }

    static string Source(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray()));

    /// <summary>The body of a method, from its signature to the matching closing brace.</summary>
    static string Body(string text, string signature)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        Check(start >= 0, "not found: " + signature);
        int open = text.IndexOf('{', start), depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text.Substring(start, i - start + 1);
        }
        throw new Exception("unbalanced braces after " + signature);
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        // F6. The daily check hashed each character's faction (chars:, beta21's B-4) but said nothing per colony, and no
        // check held the mixed line to it. A mixed game's line now also counts each colony's beavers and bots by faction.
        yield return ("F6: a mixed game's daily colony check keeps chars: and names each colony's beavers and bots by faction", () =>
        {
            string fingerprint = Body(Source("BeaverBuddies", "Colonies", "ColonyDiagnostics.cs"), "public string Fingerprint()");
            foreach (string part in new[] { "/mixed:", "/chars:", "/census:", "ColonyFactionService.Census(_districtCenterRegistry.AllDistrictCenters)" })
                Check(fingerprint.Contains(part), "the daily colony check lost " + part);
            Check(fingerprint.IndexOf("/census:", StringComparison.Ordinal) > fingerprint.IndexOf("bool mixed", StringComparison.Ordinal)
                && Regex.IsMatch(fingerprint, @"\(mixed \? \$""/census:"), "the census is not for mixed games only");
            string census = Body(Source("BeaverBuddies", "Factions", "ColonyFactionService.cs"), "public static string Census(");
            foreach (string part in new[] { "!MixedFactions.IsOn", "DistrictOwner.OwnerOfDistrict(center)", "population.Beavers", "population.Bots", "SimFactionOf(beaver)", "SimFactionOf(bot)" })
                Check(census.Contains(part), "the census no longer reads " + part);
            // Nothing of this computer's own: the line is compared between computers.
            foreach (string local in new[] { "LocalSlot", "DisplaySlot", "LocalFaction", "Time." })
                Check(!census.Contains(local), "the census reads " + local);
        });

        // S1 (mixed only). An item of the common collections is every faction's: the yield filter need not know whose
        // building asks (FactionYieldRemoverPatcher, FactionCatalog.IsCommonGood).
        yield return ("B3 (S1): a common good is every faction's, and only a common good skips the faction lookup", () =>
        {
            var goods = FactionSets.FromCollections(new[] { "Folktails", "IronTeeth" }, new[] { "Common" },
                new Dictionary<string, IEnumerable<string>> { ["Folktails"] = new[] { "Folktails" }, ["IronTeeth"] = new[] { "IronTeeth" } },
                new Dictionary<string, IEnumerable<string>>
                {
                    ["Common"] = new[] { "Log", "Berries", "ScrapMetal" },
                    ["Folktails"] = new[] { "Carrot", "BotChassis" },
                    ["IronTeeth"] = new[] { "Corn", "BotChassis" },
                });
            foreach (string item in new[] { "Log", "Berries", "ScrapMetal" })
                Check(goods.InCommon(item) && goods.Has("Folktails", item) && goods.Has("IronTeeth", item) && goods.Has(null!, item), item + " should be common");
            // Shared by both factions, one faction's, unknown, or empty: not common, so the building's faction still decides.
            foreach (string item in new[] { "BotChassis", "Carrot", "Corn", "Gold", "", null! })
                Check(!goods.InCommon(item), (item ?? "null") + " is not in the common collections");
        });

        // F8 (display, mixed only). The Ctrl+T window asks once a second whether the player's colony is still untouched.
        yield return ("B4 (F8): the untouched check looks first at the building that showed the colony touched, and forgets a deleted one", () =>
        {
            string text = Source("BeaverBuddies", "Colonies", "ColonyFoundingService.cs");
            string untouched = Body(text, "public bool IsUntouched(int slot)");
            int remembered = untouched.IndexOf("Touches(touchedBy[slot], slot, catalog)", StringComparison.Ordinal);
            int walk = untouched.IndexOf("foreach (EntityComponent entity in _entityRegistry.Entities)", StringComparison.Ordinal);
            Check(remembered > 0 && walk > remembered, "IsUntouched walks every entity before looking at the building it remembers");
            Check(untouched.Contains("touchedBy[slot] = entity") && untouched.Contains("touchedBy[slot] = null"), "IsUntouched does not keep its witness up to date");
            string touches = Body(text, "private static bool Touches(");
            Check(touches.Contains("entity.Deleted") && touches.Contains("stamp.Slot != slot") && touches.Contains("DistrictCenter"),
                "a remembered building is not checked again as the walk checks it");
            // The switch's own count (a replay, on every computer) keeps walking every entity: it must not depend on what
            // this computer's window happened to look at.
            string facts = Body(text, "public UntouchedFacts UntouchedFacts(int slot)");
            Check(!facts.Contains("touchedBy"), "the switch's check reads the window's witness");
        });

        // B1: the transpiler must not throw out of the mod's patching (a throw inside PatchAll stops every later patch).
        yield return ("B1: the pilot animation guard leaves an unexpected body as the game has it, logs, and never throws", () =>
        {
            string text = Source("BeaverBuddies", "Factions", "FactionCharacterPatches.cs");
            string transpiler = Body(text, "static IEnumerable<CodeInstruction> Transpiler(");
            Check(transpiler.Contains("if (calls != 1)") && transpiler.Contains("return code;") && !transpiler.Contains("throw"),
                "the pilot transpiler can throw or change a body it does not recognise");
            string guard = Body(text, "public static void SetBoolIfItHas(");
            int gate = guard.IndexOf("MixedFactions.IsOn", StringComparison.Ordinal);
            int has = guard.IndexOf("HasParameter", StringComparison.Ordinal);
            Check(gate > 0 && has > gate, "the guard does not read MixedFactions.IsOn before anything else");
        });
    }
}
