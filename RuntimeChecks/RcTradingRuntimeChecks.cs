#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json.Nodes;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: reviewer C: Trading Posts at scale.
internal static class RcTradingRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");
        MethodInfo With(Type type, string name, params string[] parameters) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name && m.GetParameters().Select(p => p.ParameterType.Name).SequenceEqual(parameters))
            ?? throw new Exception($"{type.FullName}.{name}({string.Join(", ", parameters)}) is gone");
        bool Calls(MethodBase method, string type, string name) =>
            IlScan.Instructions(method).Any(i => i.Calls && i.Member?.Name == name && i.Member.DeclaringType?.Name == type);
        int FirstCall(List<IlScan.Instruction> ins, string type, string name) =>
            ins.FindIndex(i => i.Calls && i.Member?.Name == name && i.Member.DeclaringType?.Name == type);
        void Need(bool value, string message) { if (!value) throw new Exception(message); }

        Type service = mod.GetType("BeaverBuddies.Colonies.ColonyExchangeService", true)!;
        Type side = mod.GetType("BeaverBuddies.Colonies.CrossingExchange", true)!;
        Type terms = mod.GetType("BeaverBuddies.Colonies.ExchangeTerms", true)!;

        test("C1: the Trading Posts' check decides every ending with ExchangeTerms.Ending, which judges the factions only while a post trades", () =>
        {
            MethodInfo check = Only(service, "CheckTradingPosts");
            Need(Calls(check, "ExchangeTerms", "Ending"), "CheckTradingPosts no longer decides endings with ExchangeTerms.Ending");
            Need(Calls(check, "ExchangeTerms", "RoundMayCross"), "CheckTradingPosts no longer asks ExchangeTerms.RoundMayCross");
            // The rule itself, as compiled: a paused post (a half in no colony) whose factions "disagree" goes on.
            MethodInfo ending = Only(terms, "Ending");
            object Ending(int owner, int partnerOwner, bool trading) =>
                ending.Invoke(null, new object[] { true, true, 0, owner, 1, partnerOwner, trading, false })!;
            Need(Ending(-1, 1, false).ToString() == "GoesOn" && Ending(0, -1, false).ToString() == "GoesOn",
                "a paused Trading Post's exchange ends for its factions");
            Need(Ending(0, 1, true).ToString() == "EndFactions", "a trading post's terms the factions refuse no longer end it");
        });

        test("C2: a round of beavers is judged in by exactly the adults it moves: free to go, carrying nothing, able to walk there", () =>
        {
            MethodInfo movable = Only(service, "Movable");
            foreach (var (type, name) in new[] { ("GoodCarrier", "get_IsCarrying"), ("DistrictCenter", "IsGloballyReachableFromCitizen"),
                ("MigrationService", "IsNotContaminated"), ("FactionTrade", "BeaverMayJoin") })
                Need(Calls(movable, type, name), $"Movable no longer asks {type}.{name}");
            MethodInfo spare = With(service, "BeaversToSpare", "DistrictCrossing");
            Need(Calls(spare, service.Name, "Movable"), "BeaversToSpare (which judges a round in) no longer counts the adults Movable finds");
            Need(!Calls(spare, "Enumerable", "Count"), "BeaversToSpare counts with LINQ again");
            MethodInfo move = Only(service, "MoveBeavers");
            Need(Calls(move, service.Name, "Movable"), "MoveBeavers no longer moves the adults Movable finds");
            Need(!Calls(move, "Enumerable", "Where"), "MoveBeavers filters the adults its own way again");
            // What moved is what the ledgers record: Cross keeps what MoveSpecial returns.
            MethodInfo special = Only(service, "MoveSpecial");
            Need(special.ReturnType == typeof(int), "MoveSpecial no longer says how much moved");
            var cross = IlScan.Instructions(Only(service, "Cross"));
            var calls = Enumerable.Range(0, cross.Count).Where(k => cross[k].Calls && cross[k].Member?.Name == "MoveSpecial").ToList();
            Need(calls.Count == 2, "Cross no longer moves science and beavers both ways");
            foreach (int k in calls)
                Need(cross[k + 1].Op != OpCodes.Pop, "Cross throws away how many beavers moved, and records what was agreed");
        });

        test("C3: the panel and the report say why a round waits: paused or flooded, no workers, no room, nothing left", () =>
        {
            MethodInfo why = Only(service, "WhyWaiting");
            foreach (var (type, name) in new[] { ("BlockableObject", "get_IsUnblocked"), ("Workplace", "get_NumberOfAssignedWorkers"),
                ("Inventory", "UnreservedCapacity"), ("DistrictCrossingInventory", "IncomingStock"), ("ExchangeTerms", "WhyGoodsWait") })
                Need(Calls(why, type, name), $"WhyWaiting no longer reads {type}.{name}");
            Type fragment = mod.GetType("BeaverBuddies.Colonies.TradingPostFragment", true)!;
            var status = IlScan.Instructions(Only(fragment, "StatusLine"));
            Need(status.Count(i => i.Calls && i.Member?.Name == "WhyWaiting") == 2, "the panel no longer says why either side waits");
            Need(Calls(Only(fragment, "Status"), fragment.Name, "StatusLine"), "the panel's status is no longer its StatusLine");
            foreach (string key in new[] { "StatusYourHalfBlocked", "StatusNoRoom", "StatusNoStock", "StatusTheirHalfBlocked", "StatusTheirNoWorkers",
                "StatusTheirNoStock", "StatusHaulAway" })
                Need(status.Any(i => i.Text == "BeaverBuddies.Colony.Trade." + key), "the panel no longer shows " + key);
            Type diagnostics = mod.GetType("BeaverBuddies.Colonies.ColonyDiagnostics", true)!;
            Need(Calls(Only(diagnostics, "Stall"), service.Name, "WhyNotIn"), "the diagnostics report no longer says why a round waits");
            // The game: pausing (and flooding) a building blocks it, which is what the panel reads.
            Type pausable = Game("Timberborn.Buildings", "Timberborn.Buildings.PausableBuilding");
            Need(Calls(Only(pausable, "Pause"), "BlockableObject", "Block"), "the game's pause no longer blocks the building");
        });

        test("C8: the Ctrl+T window says why a post's round is held up, and shows a paused exchange as paused", () =>
        {
            Type fragment = mod.GetType("BeaverBuddies.Colonies.TradingPostFragment", true)!;
            Type window = mod.GetType("BeaverBuddies.Colonies.TradeOverviewPanel", true)!;
            var describe = IlScan.Instructions(Only(window, "Describe"));
            Need(describe.Any(i => i.Calls && i.Member?.Name == "StatusLine" && i.Member.DeclaringType == fragment),
                "the trading window no longer says why a post's round waits");
            Need(describe.Any(i => i.Text == "BeaverBuddies.Colony.Trade.PausedTitle"), "the trading window no longer shows a paused exchange as paused");
        });

        test("C4: the Trading Posts' check holds what already waits on a giving half before it judges the round", () =>
        {
            var check = IlScan.Instructions(Only(service, "CheckTradingPosts"));
            int hold = FirstCall(check, service.Name, "HoldWaiting");
            int judge = check.FindIndex(i => i.Calls && i.Member?.Name == "IsIn");
            Need(hold >= 0 && judge > hold, "CheckTradingPosts no longer holds what waits on the halves before it judges the round");
            MethodInfo waiting = Only(service, "HoldWaiting");
            foreach (var (type, name) in new[] { ("Inventory", "UnreservedAmountInStock"), ("Inventory", "ReserveStock"),
                ("CrossingExchange", "Hold"), ("ExchangeTerms", "ToHoldWaiting") })
                Need(Calls(waiting, type, name), $"HoldWaiting no longer calls {type}.{name}");
        });

        test("C5: a Trading Post removed with an exchange open says so, before the game leaves its halves' stock as recovered goods", () =>
        {
            Type deletable = Game("Timberborn.EntitySystem", "Timberborn.EntitySystem.IDeletableEntity");
            Need(deletable.IsAssignableFrom(side), "CrossingExchange no longer hears its Trading Post being removed");
            Need(Calls(Only(side, "DeleteEntity"), service.Name, "OnPostRemoved"), "a removed post's exchange no longer says it ended");
            // The notice's text is built in the lambda OnPostRemoved hands to Tell (shown only where it is read).
            var notice = service.GetNestedTypes(All).Append(service).SelectMany(t => t.GetMethods(All))
                .Where(m => m.Name.Contains("OnPostRemoved") && m.GetMethodBody() != null);
            Need(notice.Any(m => IlScan.Instructions(m).Any(i => i.Text == "BeaverBuddies.Colony.Trade.Notice.PostRemoved")),
                "a removed post's exchange no longer tells the two colonies");
            // The game tells every IDeletableEntity before it posts the deletion that recovers the building's goods.
            Type entity = Game("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityComponent");
            var delete = IlScan.Instructions(Only(entity, "InternalDelete"));
            int told = FirstCall(delete, "IDeletableEntity", "DeleteEntity");
            int posted = delete.FindIndex(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "EntityDeletedEvent");
            Need(told >= 0 && posted > told, "the game no longer tells a component of its deletion before it posts it");
            Type provider = Game("Timberborn.RecoverableGoodSystem", "Timberborn.RecoverableGoodSystem.RecoverableGoodProvider");
            Need(Calls(Only(provider, "AddGoodsFromInventory"), "Inventory", "get_Stock"), "a removed building's recovered goods are no longer its whole stock");
        });

        test("C6: once a day the trade checks the goods each exchange holds and each post's room, and logs its line with detailed logging", () =>
        {
            Need(Calls(Only(service, "Tick"), service.Name, "DailyCheck"), "the daily trade check no longer runs");
            var daily = IlScan.Instructions(Only(service, "DailyCheck"));
            Need(daily.Any(i => i.Loads && i.Member?.Name == "_reservedStock"), "the daily trade check no longer reads what is reserved on a half");
            Need(daily.Any(i => i.Calls && i.Member?.Name == "AmountInStock"), "the daily trade check no longer reads a half's stock");
            Need(daily.Any(i => i.Text != null && i.Text.Contains("Trade check")), "the daily trade check no longer warns");
            Need(daily.Any(i => i.Loads && i.Member?.Name == "Debug") || daily.Any(i => i.Calls && i.Member?.Name == "get_Debug"),
                "the daily trade line is no longer only for detailed logging");
        });

        test("C7: the Trading Posts' check every 8 ticks copies no list", () =>
        {
            MethodInfo check = Only(service, "CheckTradingPosts");
            Need(!Calls(check, "Enumerable", "ToList"), "CheckTradingPosts copies the crossings into a new list every 8 ticks");
            Need(service.GetField("halves", All)?.FieldType.Name == "List`1", "the check's kept list is gone");
            // Every load held on a half notes the digest: by a name given whole, not built per load.
            Need(!Calls(Only(side, "Changed"), "String", "Concat"), "CrossingExchange builds a string for each change it notes");
        });

        test("T2: every good of both factions can be offered, carried to a half, held there, and stored by each faction that may receive it", () =>
        {
            string managed = Path.GetDirectoryName(Assembly.Load("Timberborn.Goods").Location)!;
            string zipPath = Path.GetFullPath(Path.Combine(managed, "..", "StreamingAssets", "Modding", "Blueprints.zip"));
            using ZipArchive zip = ZipFile.OpenRead(zipPath);
            JsonNode Read(string path)
            {
                ZipArchiveEntry entry = zip.GetEntry(path) ?? throw new Exception("the game has no " + path);
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!;
            }
            List<string> Strings(JsonNode? array) => array?.AsArray().Select(n => n!.GetValue<string>()).ToList() ?? new List<string>();
            var goods = zip.Entries.Where(e => e.FullName.StartsWith("Goods/") && e.FullName.EndsWith(".json"))
                .Select(e => Read(e.FullName)["GoodSpec"]!).ToDictionary(g => g["Id"]!.GetValue<string>(), g => g);
            Need(goods.Count >= 60, "the game's goods were not found: " + goods.Count);
            var collections = zip.Entries.Where(e => e.FullName.StartsWith("GoodCollections/") && e.FullName.EndsWith(".json"))
                .Select(e => Read(e.FullName)["GoodCollectionSpec"]!).ToDictionary(s => s["CollectionId"]!.GetValue<string>(), s => Strings(s["Goods"]));
            string[] factions = { "Folktails", "IronTeeth" };
            var goodsOf = factions.ToDictionary(f => f, f => Strings(Read($"Factions/Faction.{f}.blueprint.json")["FactionSpec"]!["GoodCollectionIds"])
                .Append("Common").SelectMany(id => collections[id]).ToHashSet());
            // What each faction can store: its stockpiles' good types.
            var stores = factions.ToDictionary(f => f, f =>
            {
                var ids = Strings(Read($"Factions/Faction.{f}.blueprint.json")["FactionSpec"]!["TemplateCollectionIds"]);
                var paths = zip.Entries.Where(e => e.FullName.StartsWith("TemplateCollections/") && e.FullName.EndsWith(".json"))
                    .Select(e => Read(e.FullName)["TemplateCollectionSpec"]!).Where(s => ids.Contains(s["CollectionId"]!.GetValue<string>()))
                    .SelectMany(s => Strings(s["Blueprints"])).Distinct();
                return paths.Select(p => zip.GetEntry(p.EndsWith(".json") ? p : p + ".json") == null ? null
                        : Read(p.EndsWith(".json") ? p : p + ".json")["StockpileSpec"]?["WhitelistedGoodType"]?.GetValue<string>())
                    .Where(t => t != null).ToHashSet();
            });
            int lifting = Read("Characters/Beaver/BeaverAdult.blueprint.json")["GoodCarrierSpec"]!["BaseLiftingCapacity"]!.GetValue<int>();
            Type rules = mod.GetType("BeaverBuddies.Factions.FactionRules", true)!;
            MethodInfo allows = Only(rules, "FactionAllows");
            Type form = mod.GetType("BeaverBuddies.Colonies.TradeOfferForm", true)!;
            MethodInfo judge = Only(form, "Judge");
            string science = (string)terms.GetField("Science")!.GetValue(null)!, beavers = (string)terms.GetField("Beavers")!.GetValue(null)!;
            int max = (int)terms.GetField("MaxAmount")!.GetValue(null)!;
            Need(max == 100, "a half's room for a good is no longer 100");
            var problems = new List<string>();
            foreach (var (id, spec) in goods)
            {
                int weight = spec["Weight"]!.GetValue<int>();
                string type = spec["GoodType"]!.GetValue<string>();
                if (weight > lifting) problems.Add($"{id} weighs {weight}, more than a beaver lifts ({lifting}): no worker can bring it to a half");
                foreach (string giver in factions)
                    foreach (string receiver in factions)
                    {
                        bool has = goodsOf[receiver].Contains(id);
                        Func<string, bool> receiverStores = good => goodsOf[receiver].Contains(good);
                        bool allowed = (bool)allows.Invoke(null, new object?[] { id, receiverStores, giver, receiver, science, beavers })!;
                        if (allowed != has) problems.Add($"{id} from {giver} to {receiver}: allowed {allowed}, but {receiver} has it {has}");
                        if (allowed && !stores[receiver].Contains(type)) problems.Add($"{receiver} may receive {id} ({type}) but has nowhere to store it");
                        // The offer form offers exactly what may cross, 100 of it for one of something else.
                        Func<string, bool> giveAllowed = item => (bool)allows.Invoke(null, new object?[] { item, receiverStores, giver, receiver, science, beavers })!;
                        object?[] args = { id, "100", "Log" == id ? "Plank" : "Log", "1", "1", false, 0, 0, 0, giveAllowed, null };
                        string verdict = judge.Invoke(null, args)!.ToString()!;
                        if (verdict != (allowed ? "Exchange" : "GiveNotAllowed")) problems.Add($"the form says {verdict} for 100 {id} from {giver} to {receiver}");
                    }
            }
            // Science goes between any colonies; beavers only within a faction.
            foreach (string giver in factions)
                foreach (string receiver in factions)
                {
                    Func<string, bool> none = _ => false;
                    if (!(bool)allows.Invoke(null, new object?[] { science, none, giver, receiver, science, beavers })!) problems.Add($"science from {giver} to {receiver}");
                    if ((bool)allows.Invoke(null, new object?[] { beavers, none, giver, receiver, science, beavers })! != (giver == receiver))
                        problems.Add($"beavers from {giver} to {receiver}");
                }
            Need(problems.Count == 0, string.Join("; ", problems));
            // Every good is allowed on a half, as many as the post's room: the game's crossing lists every good.
            Type initializer = Game("Timberborn.DistributionSystem", "Timberborn.DistributionSystem.DistrictCrossingInventoryInitializer");
            Need(Calls(Only(initializer, "AllowEveryGoodAsTakeable"), "IGoodService", "get_Goods"), "a crossing half no longer takes every good");
        });
    }
}
