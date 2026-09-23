using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using BeaverBuddies.Colonies;
using BeaverBuddies.Factions;
using BeaverBuddies.Lobby;

/// <summary>
/// Mixed factions (design/MIXED-FACTIONS-PLAN.md): the plain rules behind it (FactionSets, FactionTable, FactionRules,
/// SaveColonyReader, the waiting room's seating plan, the trade form's two new verdicts), and that every patch of the
/// feature leaves a game that is not mixed alone.
/// </summary>
static class FactionChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) => Check(EqualityComparer<T>.Default.Equals(expected, actual), $"expected {expected}, got {actual}");

    const string Folktails = "Folktails", IronTeeth = "IronTeeth";

    // The game's shape, cut down: Common goods, each faction's own, and two goods both factions list.
    static FactionSets Goods() => FactionSets.FromCollections(new[] { Folktails, IronTeeth }, new[] { "Common" },
        new Dictionary<string, IEnumerable<string>> { [Folktails] = new[] { "Folktails" }, [IronTeeth] = new[] { "IronTeeth" } },
        new Dictionary<string, IEnumerable<string>>
        {
            ["Common"] = new[] { "Log", "Plank", "Berries", "Water" },
            ["Folktails"] = new[] { "Carrot", "Bread", "Water", "BotChassis", "TreatedPlank" },
            ["IronTeeth"] = new[] { "Corn", "CornRation", "BotChassis", "TreatedPlank" },
        });

    static string Root()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root)!;
        Check(root != null, "could not find the repository root");
        return root!;
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("Factions: a colony table round-trips; bad rows are skipped and the first row for a slot wins", () =>
        {
            var table = new FactionTable();
            table.Set(0, Folktails);
            table.Set(2, IronTeeth);
            List<string> rows = table.Encode();
            Check(rows.SequenceEqual(new[] { "0|Folktails", "2|IronTeeth" }), string.Join(";", rows));
            FactionTable back = FactionTable.Decode(rows.Concat(new[] { "0|IronTeeth", "7|Folktails", "x|Folktails", "1|", "1", "" }));
            Equal(Folktails, back.Of(0));
            Equal<string?>(null, back.Of(1));
            Equal(IronTeeth, back.Of(2));
            Check(!back.Has(3) && back.Of(-1) == null && back.Of(9) == null);
            Equal("0Folktails,2IronTeeth", back.Fingerprint());
            FactionTable copy = back.Copy();
            copy.Set(1, IronTeeth);
            Check(!back.Has(1) && copy.Has(1), "a copy shares nothing");
        });

        yield return ("Factions: what each faction has, from the game's collections (common, own, shared by both)", () =>
        {
            FactionSets goods = Goods();
            Equal<string?>(null, goods.SoleFaction("Log"));
            Equal<string?>(null, goods.SoleFaction("Water"));
            Equal<string?>(null, goods.SoleFaction("BotChassis"));
            Equal<string?>(Folktails, goods.SoleFaction("Carrot"));
            Equal<string?>(IronTeeth, goods.SoleFaction("CornRation"));
            Check(goods.Has(Folktails, "Log") && goods.Has(Folktails, "Carrot") && !goods.Has(Folktails, "Corn"));
            Check(goods.Has(IronTeeth, "BotChassis") && !goods.Has(IronTeeth, "Bread"));
            Check(goods.IsKnown("Corn") && !goods.IsKnown("Gold"));
            List<string> shared = goods.Shared(Folktails, IronTeeth).OrderBy(g => g, StringComparer.Ordinal).ToList();
            Check(shared.SequenceEqual(new[] { "Berries", "BotChassis", "Log", "Plank", "TreatedPlank", "Water" }), string.Join(",", shared));
            Check(goods.ItemsOf(Folktails).Count() == goods.ItemsOf(Folktails).Distinct().Count(), "an item listed twice");
            Check(!goods.ItemsOf("Unknown").Contains("Carrot"), "an unknown faction has only the common items");
        });

        yield return ("Factions: a founding's faction is the base faction outside a mixed game, and must be known and unlocked in one", () =>
        {
            var known = new[] { Folktails, IronTeeth };
            Equal(FactionChoiceVerdict.Allowed, FactionRules.JudgeFoundingFaction(false, "Anything", known, known));
            Equal(FactionChoiceVerdict.Allowed, FactionRules.JudgeFoundingFaction(true, null, known, known));
            Equal(FactionChoiceVerdict.Unknown, FactionRules.JudgeFoundingFaction(true, "Otters", known, known));
            Equal(FactionChoiceVerdict.Unavailable, FactionRules.JudgeFoundingFaction(true, IronTeeth, known, new[] { Folktails }));
            Equal(FactionChoiceVerdict.Allowed, FactionRules.JudgeFoundingFaction(true, IronTeeth, known, null));
            Equal(Folktails, FactionRules.FoundingFaction(false, IronTeeth, Folktails));
            Equal(Folktails, FactionRules.FoundingFaction(true, null, Folktails));
            Equal(IronTeeth, FactionRules.FoundingFaction(true, IronTeeth, Folktails));
        });

        yield return ("Factions: an untouched colony's own player may switch it to another available faction, and nobody else", () =>
        {
            var known = new[] { Folktails, IronTeeth };
            var untouched = new UntouchedFacts(1, 0, 0, false, 0);
            FactionSwitchVerdict Judge(bool mixed = true, bool own = true, string wanted = IronTeeth, UntouchedFacts? facts = null,
                string[]? available = null) =>
                FactionRules.JudgeSwitch(mixed, own, Folktails, wanted, known, available ?? known, facts ?? untouched);
            Equal(FactionSwitchVerdict.Allowed, Judge());
            Equal(FactionSwitchVerdict.NotMixed, Judge(mixed: false));
            Equal(FactionSwitchVerdict.NotYours, Judge(own: false));
            Equal(FactionSwitchVerdict.NoColony, Judge(facts: new UntouchedFacts(0, 0, 0, false, 0)));
            Equal(FactionSwitchVerdict.SameFaction, Judge(wanted: Folktails));
            Equal(FactionSwitchVerdict.Unknown, Judge(wanted: "Otters"));
            Equal(FactionSwitchVerdict.Unavailable, Judge(available: new[] { Folktails }));
            Equal(FactionSwitchVerdict.Touched, Judge(facts: new UntouchedFacts(1, 1, 0, false, 0)));
            Equal(FactionSwitchVerdict.Touched, Judge(facts: new UntouchedFacts(1, 0, 3, false, 0)));
            Equal(FactionSwitchVerdict.Touched, Judge(facts: new UntouchedFacts(1, 0, 0, true, 0)));
            Equal(FactionSwitchVerdict.Allowed, Judge(facts: new UntouchedFacts(2, 0, 0, false, 0)));
        });

        yield return ("Factions: a colony places its own faction's buildings and common ones, and Trading Posts of any faction", () =>
        {
            Check(FactionRules.MayPlace(true, Folktails, Folktails, false));
            Check(FactionRules.MayPlace(true, null, IronTeeth, false), "a common building (a path)");
            Check(!FactionRules.MayPlace(true, IronTeeth, Folktails, false));
            Check(FactionRules.MayPlace(true, IronTeeth, Folktails, true), "a Trading Post half");
            Check(FactionRules.MayPlace(false, IronTeeth, Folktails, false), "not a mixed game");
        });

        yield return ("Factions: between factions only goods the receiver stores and science cross, never beavers", () =>
        {
            FactionSets goods = Goods();
            bool Allows(string item, string from, string to) =>
                FactionRules.FactionAllows(item, good => goods.Has(to, good), from, to, ExchangeTerms.Science, ExchangeTerms.Beavers);
            Check(Allows("Log", Folktails, IronTeeth) && Allows("BotChassis", IronTeeth, Folktails));
            Check(!Allows("Carrot", Folktails, IronTeeth) && !Allows("CornRation", IronTeeth, Folktails));
            Check(Allows(ExchangeTerms.Science, Folktails, IronTeeth));
            Check(!Allows(ExchangeTerms.Beavers, Folktails, IronTeeth) && Allows(ExchangeTerms.Beavers, IronTeeth, IronTeeth));
            // A colony that holds the other faction's goods (a handover) may pass them to a colony of that faction.
            Check(Allows("Corn", Folktails, IronTeeth));
            Check(Allows("Carrot", Folktails, Folktails));
            Check(FactionRules.FactionAllows("Anything", null, Folktails, Folktails, ExchangeTerms.Science, ExchangeTerms.Beavers));
            Check(FactionRules.MayBeaverCross(IronTeeth, IronTeeth) && !FactionRules.MayBeaverCross(Folktails, IronTeeth));
            Check(FactionRules.MayBeaverCross(null, IronTeeth), "a beaver of no known faction");
        });

        yield return ("Factions: the trade form names what the factions do not let cross, on either side", () =>
        {
            FactionSets goods = Goods();
            Func<string, bool> toIronTeeth = item => FactionRules.FactionAllows(item, g => goods.Has(IronTeeth, g), Folktails, IronTeeth,
                ExchangeTerms.Science, ExchangeTerms.Beavers);
            Func<string, bool> toFolktails = item => FactionRules.FactionAllows(item, g => goods.Has(Folktails, g), IronTeeth, Folktails,
                ExchangeTerms.Science, ExchangeTerms.Beavers);
            TradeOfferForm.Verdict Judge(string give, string get) =>
                TradeOfferForm.Judge(give, "10", get, "10", "1", false, out _, out _, out _, toIronTeeth, toFolktails);
            Equal(TradeOfferForm.Verdict.Exchange, Judge("Log", "Plank"));
            Equal(TradeOfferForm.Verdict.GiveNotAllowed, Judge("Carrot", "Log"));
            Equal(TradeOfferForm.Verdict.GetNotAllowed, Judge("Log", "Corn"));
            Equal(TradeOfferForm.Verdict.GiveNotAllowed, Judge(ExchangeTerms.Beavers, "Log"));
            Equal(TradeOfferForm.Verdict.Exchange, Judge(ExchangeTerms.Science, "Log"));
            // A side of nothing is not judged, and without the predicates everything may (as before mixed factions).
            Equal(TradeOfferForm.Verdict.Request, TradeOfferForm.Judge("Carrot", "0", "Log", "5", "1", false, out _, out _, out _, toIronTeeth, toFolktails));
            Equal(TradeOfferForm.Verdict.Exchange, TradeOfferForm.Judge("Carrot", "10", "Corn", "10", "1", false, out _, out _, out _));
            Check(!TradeOfferForm.IsOffer(TradeOfferForm.Verdict.GiveNotAllowed) && !TradeOfferForm.IsOffer(TradeOfferForm.Verdict.GetNotAllowed));
        });

        yield return ("Factions: a handover goes to the nearest colony of the same faction, else the nearest, the lower slot on a tie", () =>
        {
            string FactionOf(int slot) => slot % 2 == 0 ? Folktails : IronTeeth;
            var candidates = new List<(int, long)> { (1, 10), (2, 50), (3, 10) };
            Equal<int?>(2, FactionRules.PreferSameFaction(candidates, FactionOf, Folktails));
            Equal<int?>(1, FactionRules.PreferSameFaction(candidates, FactionOf, IronTeeth));
            Equal<int?>(1, FactionRules.PreferSameFaction(new List<(int, long)> { (3, 10), (1, 10) }, FactionOf, "Otters"));
            Equal<int?>(null, FactionRules.PreferSameFaction(new List<(int, long)>(), FactionOf, Folktails));
        });

        yield return ("Factions: the waiting room's switcher steps through the factions and wraps, and who may pick", () =>
        {
            var factions = new[] { Folktails, IronTeeth, "Otters" };
            Equal(IronTeeth, FactionRules.Step(factions, Folktails, 1));
            Equal("Otters", FactionRules.Step(factions, Folktails, -1));
            Equal(Folktails, FactionRules.Step(factions, "Otters", 1));
            Equal(Folktails, FactionRules.Step(factions, "Unknown", 1));
            Equal(Folktails, FactionRules.Step(new[] { Folktails }, Folktails, 1));
            Check(FactionRules.MayPickInRoom(true, false, true) && !FactionRules.MayPickInRoom(false, false, false));
            Check(FactionRules.MayPickInRoom(true, true, false) && !FactionRules.MayPickInRoom(true, true, true));
        });

        yield return ("Factions: the waiting room's seating plan is the order the world's slot table is filled in", () =>
        {
            var guests = new List<string> { "steam:2", "", "steam:3", "steam:4", "steam:5" };
            List<int?> plan = LobbyRules.SeatingPlan("steam:1", guests);
            Check(plan.SequenceEqual(new int?[] { 1, null, 2, 3, null }), string.Join(",", plan));
            // What LobbyWorldMaker.SeatInRoomOrder does: the host, then each guest with an id, on an empty table.
            var table = new ColonySlotTable();
            table.Resolve("steam:1", "Host");
            var seated = guests.Select(id => string.IsNullOrEmpty(id) ? null : table.Resolve(id, "")).ToList();
            Check(plan.SequenceEqual(seated), "the plan and the seating differ");
        });

        yield return ("Factions: a save's colonies are read from its world without the rest of it, and a bad save gives none", () =>
        {
            byte[] Save(string world)
            {
                using var memory = new MemoryStream();
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("world.json").Open(), new UTF8Encoding(true))) writer.Write(world);
                    using (var meta = new StreamWriter(zip.CreateEntry("save_metadata.json").Open())) meta.Write("{}");
                }
                return memory.ToArray();
            }
            string singletons = "\"Singletons\":{\"TerrainMap\":{\"Heights\":[1,2,3]},\"FactionService\":{\"Id\":\"Folktails\"},"
                + "\"BeaverBuddies.ColonyMode\":{\"Enabled\":true},\"BeaverBuddies.ColonySlots\":{\"Table\":\"0|steam:1|Kyler\\n1|steam:2|Anna\"},"
                + "\"BeaverBuddies.ColonyFactions\":{\"Mixed\":true,\"Base\":\"Folktails\",\"Colonies\":[\"0|Folktails\",\"1|IronTeeth\"]}}";
            SaveColonyInfo? info = SaveColonyReader.Read(Save("{\"GameVersion\":\"1.1\"," + singletons + ",\"Entities\":[{\"Id\":\"x\"}]}"));
            Check(info != null && info.BaseFaction == Folktails && info.SeparateColonies && info.Mixed);
            Equal(IronTeeth, info!.FactionOfSlot(1));
            Equal<string?>(null, info.FactionOfSlot(2));
            Check(info.SlotTable.Contains("steam:2"));
            // Entities first still works (skipped); a separate-colonies save without the factions singleton is not mixed.
            SaveColonyInfo? plain = SaveColonyReader.Read(Save("{\"Entities\":[{\"a\":1}],\"Singletons\":{\"FactionService\":{\"Id\":\"IronTeeth\"},"
                + "\"BeaverBuddies.ColonyMode\":{\"Enabled\":true}}}"));
            Check(plain != null && plain.BaseFaction == IronTeeth && !plain.Mixed);
            Equal(IronTeeth, plain!.FactionOfSlot(3));
            // A shared save is never mixed, whatever it says.
            SaveColonyInfo? shared = SaveColonyReader.Read(Save("{\"Singletons\":{\"BeaverBuddies.ColonyFactions\":{\"Mixed\":true}}}"));
            Check(shared != null && !shared.SeparateColonies && !shared.Mixed);
            Check(SaveColonyReader.Read(new byte[] { 1, 2, 3 }) == null && SaveColonyReader.Read(null!) == null);
        });

        yield return ("Factions: every patch of the feature does nothing outside a mixed game", () =>
        {
            string folder = Path.Combine(Root(), "BeaverBuddies", "Factions");
            // The two patches that decide whether a game is mixed, and so run in every game.
            var deciding = new HashSet<string> { "MixedFactionsDecidePatcher", "NewGameFactionCapturePatcher" };
            int patches = 0;
            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                string text = File.ReadAllText(file);
                var starts = Regex.Matches(text, @"\[HarmonyPatch[^\]]*\]\s*(?:\[[^\]]*\]\s*)*(?:static |public |internal )*class (\w+)");
                foreach (Match match in starts)
                {
                    string name = match.Groups[1].Value;
                    int start = match.Index;
                    int next = text.IndexOf("\n    [HarmonyPatch", start + match.Length, StringComparison.Ordinal);
                    int nextClass = text.IndexOf("\n    public ", start + match.Length, StringComparison.Ordinal);
                    int end = new[] { next, nextClass, text.Length }.Where(i => i > 0).Min();
                    string body = text.Substring(start, end - start);
                    patches++;
                    if (deciding.Contains(name)) continue;
                    Check(body.Contains("MixedFactions.IsOn"), $"{Path.GetFileName(file)}: {name} does not check MixedFactions.IsOn");
                }
            }
            Check(patches >= 30, $"found only {patches} patches; the check is not looking in the right place");
        });

        yield return ("Factions: every string the feature uses exists in the English file", () =>
        {
            string root = Root();
            string csv = File.ReadAllText(Path.Combine(root, "BeaverBuddies", "Localizations", "enUS_BeaverBuddie.csv"));
            var defined = new HashSet<string>(Regex.Matches(csv, "^([A-Za-z0-9.]+),", RegexOptions.Multiline).Select(m => m.Groups[1].Value));
            var files = Directory.GetFiles(Path.Combine(root, "BeaverBuddies", "Factions"), "*.cs");
            var missing = new List<string>();
            int used = 0;
            foreach (string file in files)
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(BeaverBuddies\\.[A-Za-z.]+)\""))
                {
                    // Strings only: save keys (BeaverBuddies.ColonyMode, .ColonyFactions, .CharacterFaction) are not.
                    string key = m.Groups[1].Value;
                    if (!key.StartsWith("BeaverBuddies.Colony.") && !key.StartsWith("BeaverBuddies.Lobby.") && !key.StartsWith("BeaverBuddies.Settings.")) continue;
                    used++;
                    if (!defined.Contains(m.Groups[1].Value)) missing.Add(m.Groups[1].Value);
                }
            }
            Check(used >= 8, "found only " + used + " keys");
            Check(missing.Count == 0, "missing from enUS_BeaverBuddie.csv: " + string.Join(", ", missing.Distinct()));
        });
    }
}
