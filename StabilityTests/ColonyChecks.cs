using BeaverBuddies.IO;
using BeaverBuddies.Colonies;
using Newtonsoft.Json.Linq;
using TimberNet;

// Separate colonies: who sent an action, who owns which land, and what the host allows. The rules are written
// against small interfaces so they run here with a fake world; the real server stamps events over fake sockets.
static class ColonyChecks
{
    static void Check(bool value, string message = "assertion failed") { if (!value) throw new Exception(message); }
    static void Equal<T>(T expected, T actual) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"expected [{expected}], got [{actual}]");

    static ColonyTerritory TwoColonies() => new ColonyTerritory(new[] { new ColonyTile(10, 10), new ColonyTile(30, 10) });

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        // ---- 1. the host stamps who sent each event ----

        yield return ("Colony: a guest's event reaches the host stamped with the guest's number, whatever the guest wrote", () =>
        {
            using var s = new ActivityTransportChecks.Session(2);
            var honest = new JObject { [TimberNetBase.TYPE_KEY] = "Probe", [TimberNetBase.TICKS_KEY] = 0, ["n"] = 1 };
            var liar = new JObject { [TimberNetBase.TYPE_KEY] = "Probe", [TimberNetBase.TICKS_KEY] = 0, ["n"] = 2, [TimberNetBase.PLAYER_KEY] = 0 };
            s.Guests[0].DoUserInitiatedEvent(honest);
            s.Guests[1].DoUserInitiatedEvent(liar);
            var got = new List<JObject>();
            Check(SpinWait.SpinUntil(() =>
            {
                got.AddRange(s.Host.ReadEvents(0).Where(e => (string?)e[TimberNetBase.TYPE_KEY] == "Probe"));
                return got.Count >= 2;
            }, 3000), "the host never received both events");
            Equal(1, (int)got.Single(e => (int)e["n"]! == 1)[TimberNetBase.PLAYER_KEY]!);
            // The second guest claimed to be the host; the host's own numbering wins.
            Equal(2, (int)got.Single(e => (int)e["n"]! == 2)[TimberNetBase.PLAYER_KEY]!);
        });

        // ---- 2. a grouped event hands its stamp to every event inside it ----

        yield return ("Colony: a stamped group stamps every event inside it, replacing what the sender wrote", () =>
        {
            var group = new JObject
            {
                [TimberNetBase.TYPE_KEY] = "GroupedEvent",
                ["events"] = new JArray(
                    new JObject { [TimberNetBase.TYPE_KEY] = "A" },
                    new JObject { [TimberNetBase.TYPE_KEY] = "B", [TimberNetBase.PLAYER_KEY] = 0 }),
            };
            TimberNetBase.StampPlayer(group, 3);
            Equal(3, (int)group[TimberNetBase.PLAYER_KEY]!);
            foreach (JObject child in (JArray)group["events"]!) Equal(3, (int)child[TimberNetBase.PLAYER_KEY]!);
        });

        yield return ("Colony: a group in the mod's own JSON shape ($type and $values) stamps every event inside it", () =>
        {
            // The mod serializes with type names, which writes a list as {"$type": ..., "$values": [...]}.
            var group = TypedGroup(new JObject { ["$type"] = "A", [TimberNetBase.PLAYER_KEY] = 0 }, new JObject { ["$type"] = "B" });
            TimberNetBase.StampPlayer(group, 2);
            foreach (JObject child in (JArray)group["events"]!["$values"]!) Equal(2, (int)child[TimberNetBase.PLAYER_KEY]!);
        });

        yield return ("Colony: a group sent by a guest arrives at the host with its children stamped", () =>
        {
            using var s = new ActivityTransportChecks.Session(1);
            var group = TypedGroup(new JObject { ["$type"] = "A", [TimberNetBase.PLAYER_KEY] = 0 });
            s.Guests[0].DoUserInitiatedEvent(group);
            JObject? got = null;
            Check(SpinWait.SpinUntil(() =>
            {
                got ??= s.Host.ReadEvents(0).FirstOrDefault(e => (string?)e[TimberNetBase.TYPE_KEY] == "GroupedEvent");
                return got != null;
            }, 3000), "the host never received the group");
            Equal(1, (int)got![TimberNetBase.PLAYER_KEY]!);
            Equal(1, (int)got["events"]!["$values"]![0]![TimberNetBase.PLAYER_KEY]!);
        });

        // ---- land division (only used now to give alpha saves' district centers their owners) ----

        yield return ("Colony: each tile belongs to the nearest start, ties to the lower colony, height ignored", () =>
        {
            var t = TwoColonies();
            Equal(1, t.OwnerOf(10, 10));
            Equal(2, t.OwnerOf(30, 10));
            Equal(1, t.OwnerOf(19, 50));
            Equal(2, t.OwnerOf(21, -40));
            // x = 20 is exactly halfway: colony 1 keeps it.
            Equal(1, t.OwnerOf(20, 10));
            Equal(1, t.OwnerOf(20, 999));
            // Tiles carry no height, so every level of a column has one owner by construction.
            Equal(t.OwnerOf(new ColonyTile(25, 3)), t.OwnerOf(25, 3));
        });

        yield return ("Colony: three and four starts divide the map by nearest start", () =>
        {
            var three = new ColonyTerritory(new[] { new ColonyTile(0, 0), new ColonyTile(100, 0), new ColonyTile(50, 100) });
            Equal(3, three.ColonyCount);
            Equal(1, three.OwnerOf(10, 10));
            Equal(2, three.OwnerOf(90, 10));
            Equal(3, three.OwnerOf(50, 90));
            var four = new ColonyTerritory(new[] { new ColonyTile(0, 0), new ColonyTile(64, 0), new ColonyTile(0, 64), new ColonyTile(64, 64) });
            Equal(1, four.OwnerOf(5, 5));
            Equal(2, four.OwnerOf(60, 5));
            Equal(3, four.OwnerOf(5, 60));
            Equal(4, four.OwnerOf(60, 60));
            // The exact centre is equally near all four: the lowest colony wins.
            Equal(1, four.OwnerOf(32, 32));
        });

        yield return ("Colony: a start on the map edge still owns the tiles around it", () =>
        {
            var t = new ColonyTerritory(new[] { new ColonyTile(0, 0), new ColonyTile(63, 63) });
            Equal(1, t.OwnerOf(0, 0));
            Equal(1, t.OwnerOf(0, 1));
            Equal(2, t.OwnerOf(63, 62));
            // Tiles past the edge have an owner too, so the strip at the edge is well defined.
            Equal(1, t.OwnerOf(-1, 0));
        });

        yield return ("Colony: territory needs no floating point and survives extreme coordinates", () =>
        {
            var t = new ColonyTerritory(new[] { new ColonyTile(int.MinValue / 2, 0), new ColonyTile(int.MaxValue / 2, 0) });
            Equal(1, t.OwnerOf(-5, 0));
            Equal(2, t.OwnerOf(5, 0));
        });

        // ---- 5. the border strip ----

        yield return ("Colony: the strip is the tiles on both sides that touch the other colony", () =>
        {
            var t = TwoColonies();
            // Colony 1 owns x <= 20, colony 2 owns x >= 21.
            Check(t.IsStrip(20, 10));
            Check(t.IsStrip(21, 10));
            Check(!t.IsStrip(19, 10));
            Check(!t.IsStrip(22, 10));
            Check(!t.IsStrip(10, 10));
            Equal(2 * 40, t.StripTiles(40, 40).Count);
        });

        yield return ("Colony: two starts in any direction give a straight border where a three-wide crossing fits", () =>
        {
            // A District Crossing half is three tiles wide, so the pair needs three tiles in a row on one side with
            // the other colony directly behind each. A slanted border has no such place when the starts are diagonal.
            for (int angle = 0; angle < 360; angle += 5)
            {
                double radians = angle * Math.PI / 180;
                var a = new ColonyTile(64 - (int)Math.Round(25 * Math.Cos(radians)), 64 - (int)Math.Round(25 * Math.Sin(radians)));
                var b = new ColonyTile(64 + (int)Math.Round(25 * Math.Cos(radians)), 64 + (int)Math.Round(25 * Math.Sin(radians)));
                var t = new ColonyTerritory(new[] { a, b });
                Equal(1, t.OwnerOf(a)); Equal(2, t.OwnerOf(b));
                int spots = 0;
                for (int x = 0; x < 125; x++)
                for (int y = 0; y < 125; y++)
                {
                    int o = t.OwnerOf(x, y);
                    if (t.OwnerOf(x + 1, y) == o && t.OwnerOf(x + 2, y) == o && t.OwnerOf(x, y + 1) != o
                        && t.OwnerOf(x + 1, y + 1) != o && t.OwnerOf(x + 2, y + 1) != o) spots++;
                    if (t.OwnerOf(x, y + 1) == o && t.OwnerOf(x, y + 2) == o && t.OwnerOf(x + 1, y) != o
                        && t.OwnerOf(x + 1, y + 1) != o && t.OwnerOf(x + 1, y + 2) != o) spots++;
                }
                Check(spots >= 100, $"only {spots} crossing spots with starts {a} and {b}");
            }
            // Halfway goes to colony 1, whichever side it is on, and the split follows the longer axis.
            var right = new ColonyTerritory(new[] { new ColonyTile(10, 10), new ColonyTile(30, 20) });
            Equal(1, right.OwnerOf(20, 99)); Equal(2, right.OwnerOf(21, -5));
            var left = new ColonyTerritory(new[] { new ColonyTile(30, 10), new ColonyTile(10, 20) });
            Equal(1, left.OwnerOf(20, 0)); Equal(2, left.OwnerOf(19, 0));
            var up = new ColonyTerritory(new[] { new ColonyTile(10, 10), new ColonyTile(15, 40) });
            Equal(1, up.OwnerOf(99, 25)); Equal(2, up.OwnerOf(0, 26));
        });

        yield return ("Colony: a diagonal border leaves no gap a path could cross", () =>
        {
            foreach (var t in new[]
            {
                new ColonyTerritory(new[] { new ColonyTile(5, 5), new ColonyTile(40, 33) }),
                new ColonyTerritory(new[] { new ColonyTile(3, 60), new ColonyTile(50, 2) }),
                new ColonyTerritory(new[] { new ColonyTile(0, 0), new ColonyTile(64, 0), new ColonyTile(30, 60) }),
            })
            {
                for (int x = -2; x < 70; x++)
                for (int y = -2; y < 70; y++)
                {
                    if (t.IsStrip(x, y)) continue;
                    // Two tiles outside the strip that touch always belong to one colony.
                    Check(t.IsStrip(x + 1, y) || t.OwnerOf(x + 1, y) == t.OwnerOf(x, y), $"gap at {x},{y} east");
                    Check(t.IsStrip(x, y + 1) || t.OwnerOf(x, y + 1) == t.OwnerOf(x, y), $"gap at {x},{y} north");
                }
                // And across the border, a strip tile of one colony always touches a strip tile of the other,
                // which is where the two halves of a crossing go back to back.
                bool touching = false;
                for (int x = 0; x < 64 && !touching; x++)
                for (int y = 0; y < 64 && !touching; y++)
                    touching = t.IsStrip(x, y) && ((t.IsStrip(x + 1, y) && t.OwnerOf(x, y) != t.OwnerOf(x + 1, y))
                        || (t.IsStrip(x, y + 1) && t.OwnerOf(x, y) != t.OwnerOf(x, y + 1)));
                Check(touching, "no back-to-back strip tiles");
            }
        });

        // ---- player slots ----

        yield return ("Colony: a new player takes the lowest free slot and keeps it", () =>
        {
            var table = new ColonySlotTable();
            Equal<int?>(0, table.Resolve("steam:1", "Host"));
            Equal<int?>(1, table.Resolve("steam:2", "Friend"));
            // The same player, even under a new name, keeps their slot.
            Equal<int?>(1, table.Resolve("steam:2", "Friend2"));
            Equal("Friend2", table.NameOf(1));
            Equal<int?>(0, table.SlotOf("steam:1"));
            Equal<int?>(null, table.SlotOf("steam:3"));
        });

        yield return ("Colony: with every slot taken, a new player is a helper and is not recorded", () =>
        {
            var table = new ColonySlotTable();
            for (int i = 0; i < ColonySlotTable.MaxSlots; i++) table.Resolve("p" + i, "P" + i);
            Equal<int?>(null, table.Resolve("extra", "Extra"));
            Equal(ColonySlotTable.MaxSlots, table.Entries.Count);
            Equal<int?>(null, table.SlotOf("extra"));
        });

        yield return ("Colony: the slot table survives its text form, whoever hosts", () =>
        {
            var table = new ColonySlotTable();
            table.Resolve("steam:1", "Host | one");
            table.Resolve("local:abc", "Friend");
            var copy = new ColonySlotTable();
            copy.Set(ColonySlotTable.Decode(ColonySlotTable.Encode(table.Entries)));
            Equal<int?>(0, copy.SlotOf("steam:1"));
            Equal<int?>(1, copy.SlotOf("local:abc"));
            // The friend hosts the same save next time: they are still slot 1, the first host still slot 0.
            Equal<int?>(1, copy.Resolve("local:abc", "Friend"));
            Equal<int?>(0, copy.Resolve("steam:1", "Host"));
            // A slot freed in the table is reused for the next new player.
            copy.Set(new[] { new ColonySlotEntry("local:abc", 1, "Friend") });
            Equal<int?>(0, copy.Resolve("new", "New"));
        });

        yield return ("Colony: a damaged slot table keeps only sane, unique entries", () =>
        {
            var table = new ColonySlotTable();
            table.Set(new[]
            {
                new ColonySlotEntry("a", 0, "A"), new ColonySlotEntry("a", 1, "A again"),
                new ColonySlotEntry("b", 0, "B on a taken slot"), new ColonySlotEntry("c", 9, "C"),
                new ColonySlotEntry("", 2, "nobody"), new ColonySlotEntry("d", 3, "D"),
            });
            Equal(2, table.Entries.Count);
            Equal<int?>(0, table.SlotOf("a"));
            Equal<int?>(3, table.SlotOf("d"));
            Equal(0, ColonySlotTable.Decode("garbage\n|x").Count);
        });

        // ---- who may change what ----

        yield return ("Colony: your own things, and things in no district, are yours to change", () =>
        {
            var w = new FakeWorld().Own("mine", 1).Own("dc1", 1);
            Check(ColonyRules.Judge(ColonyScope.Entities("mine", "dc1"), 1, w, true).IsAllowed);
            // A tree, a building cut off from roads: nobody's.
            Check(ColonyRules.Judge(ColonyScope.Entities("loose"), 1, w, true).IsAllowed);
            // Missing and empty ids are left to the event.
            Check(ColonyRules.Judge(ColonyScope.Entities("gone", null!, ""), 1, w, true).IsAllowed);
        });

        yield return ("Colony: another colony's things are refused, whether or not its player is playing", () =>
        {
            var w = new FakeWorld().Own("theirs", 0).Own("mine", 1);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("theirs"), 1, w, true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Entities("mine"), 1, w, true).IsAllowed);
            // Things nobody owns (in no district, on nobody's land) stay free.
            Check(ColonyRules.Judge(ColonyScope.Entities("loose"), 1, w, true).IsAllowed);
            // One owned thing among several refuses the whole action.
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("mine", "theirs"), 1, w, true).Refusal);
            // A player not seated yet (slot -1) may change nothing that is owned.
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("theirs"), -1, w, true).Refusal);
            Check(!ColonyRules.MayChange(1, 0));
            Check(ColonyRules.MayChange(1, 1));
            Check(ColonyRules.MayChange(1, null));
        });

        yield return ("Colony: anyone may demolish a District Crossing, but not run the other side's half", () =>
        {
            var w = new FakeWorld().Own("crossingB", 0).Crossing("crossingB").Own("houseB", 0);
            Check(ColonyRules.Judge(ColonyScope.Demolish("crossingB"), 1, w, true).IsAllowed);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("crossingB"), 1, w, true).Refusal);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Demolish("houseB"), 1, w, true).Refusal);
        });

        yield return ("Colony: beavers move only between a colony's own districts", () =>
        {
            var w = new FakeWorld().Own("dcA", 0).Own("dcB", 1).Own("dcB2", 1);
            Check(ColonyRules.Judge(ColonyScope.Migration("dcB", "dcB2"), 1, w, true).IsAllowed);
            // Neither sent to another colony (it would have to feed them) nor taken from one.
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Migration("dcB", "dcA"), 1, w, true).Refusal);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Migration("dcA", "dcB"), 1, w, true).Refusal);
        });

        yield return ("Colony: building needs the unlock, and a spot off another colony's land and roads", () =>
        {
            var w = new FakeWorld().Locked(1, "Observatory")
                .Conflict("Dam", ColonyRefusal.OtherColonyArea).Conflict("Path", ColonyRefusal.TouchesOtherColony);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("House")), 1, w, true).IsAllowed);
            Equal(ColonyRefusal.Locked, ColonyRules.Judge(ColonyScope.Place(Place("Observatory")), 1, w, true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Observatory")), 0, w, true).IsAllowed);
            Equal(ColonyRefusal.OtherColonyArea, ColonyRules.Judge(ColonyScope.Place(Place("Dam")), 1, w, true).Refusal);
            Equal(ColonyRefusal.TouchesOtherColony, ColonyRules.Judge(ColonyScope.Place(Place("Path")), 1, w, true).Refusal);
        });

        yield return ("Colony: marking map tiles keeps only the tiles the colony may work", () =>
        {
            Func<(int x, int y, int z), ColonyTile> key = t => new ColonyTile(t.x, t.y);
            var w = new FakeWorld().OthersTile(new ColonyTile(99, 99));
            var tiles = new List<(int x, int y, int z)> { (1, 1, 1), (99, 99, 1), (2, 2, 1) };
            // Judged on the player's own computer: nothing changes, one tile would go.
            var local = ColonyRules.Judge(ColonyScope.Tiles(tiles, key), 1, w, false);
            Check(local.IsAllowed); Equal(1, local.Removed); Equal(3, tiles.Count);
            // Judged by the host: the tile on another colony's land is taken out, the rest keep their order.
            var host = ColonyRules.Judge(ColonyScope.Tiles(tiles, key), 1, w, true);
            Check(host.IsAllowed); Equal(1, host.Removed);
            Check(tiles.SequenceEqual(new[] { (1, 1, 1), (2, 2, 1) }));
            // Only another colony's land: nothing is marked.
            var theirs = new List<(int x, int y, int z)> { (99, 99, 1) };
            Equal(ColonyRefusal.NothingOwn, ColonyRules.Judge(ColonyScope.Tiles(theirs, key), 1, w, true).Refusal);
        });

        yield return ("Colony: a list of things is cut down to yours and nobody's, in order", () =>
        {
            var w = new FakeWorld().Own("a", 1).Own("b", 0).Own("c", 1);
            var ids = new List<string> { "b", "a", "loose", "c" };
            var judged = ColonyRules.Judge(ColonyScope.EntityList(ids, id => id), 1, w, false);
            Check(judged.IsAllowed); Equal(1, judged.Removed); Equal(4, ids.Count);
            var v = ColonyRules.Judge(ColonyScope.EntityList(ids, id => id), 1, w, true);
            Check(v.IsAllowed); Equal(1, v.Removed);
            Check(ids.SequenceEqual(new[] { "a", "loose", "c" }));
            Equal(ColonyRefusal.NothingOwn, ColonyRules.Judge(ColonyScope.EntityList(new List<string> { "b" }, id => id), 1, w, true).Refusal);
            // Demolition lists may include a crossing.
            var dem = new List<string> { "x" };
            Check(ColonyRules.Judge(ColonyScope.EntityList(dem, id => id, demolition: true), 1, new FakeWorld().Own("x", 0).Crossing("x"), true).IsAllowed);
        });

        yield return ("Colony: shared actions are always allowed", () =>
        {
            Check(ColonyRules.Judge(ColonyScope.Global, -1, new FakeWorld(), true).IsAllowed);
        });

        // ---- founding ----

        yield return ("Colony: the running colony digest is the same for the same changes, differs for others, and counts only inside the simulation", () =>
        {
            ColonyDigest.Gate = () => true;
            ColonyDigest.Reset();
            ulong start = ColonyDigest.Value;
            ColonyDigest.Note("stamp", 12345, 1); ColonyDigest.Note("science", 1, 40, 140);
            ulong one = ColonyDigest.Value; Equal(2, ColonyDigest.Changes);
            ColonyDigest.Reset(); Equal(start, ColonyDigest.Value); Equal(0, ColonyDigest.Changes);
            ColonyDigest.Note("stamp", 12345, 1); ColonyDigest.Note("science", 1, 40, 140);
            Equal(one, ColonyDigest.Value);
            // Another order, or another number, is another game.
            ColonyDigest.Reset(); ColonyDigest.Note("science", 1, 40, 140); ColonyDigest.Note("stamp", 12345, 1);
            Check(ColonyDigest.Value != one, "order");
            ColonyDigest.Reset(); ColonyDigest.Note("stamp", 12345, 2); ColonyDigest.Note("science", 1, 40, 140);
            Check(ColonyDigest.Value != one, "a different slot");
            // Outside the simulation (loading, display) nothing counts.
            ColonyDigest.Reset(); ColonyDigest.Gate = () => false;
            ColonyDigest.Note("stamp", 12345, 1);
            Equal(start, ColonyDigest.Value); Equal(0, ColonyDigest.Changes);
            ColonyDigest.Gate = () => true;
            // Names hash the same every run (not string.GetHashCode).
            Equal(ColonyDigest.Of("Carrot"), ColonyDigest.Of("Carrot")); Check(ColonyDigest.Of("Carrot") != ColonyDigest.Of("Potato"));
            Equal(0L, ColonyDigest.Of(null));
        });

        yield return ("Colony: a Trading Post is removed by either of its partners, and by nobody else", () =>
        {
            var w = new FakeWorld().Crossing("post", 0, 1).Own("post", 0);
            Check(ColonyRules.Judge(ColonyScope.Demolish("post"), 0, w, true).IsAllowed);
            Check(ColonyRules.Judge(ColonyScope.Demolish("post"), 1, w, true).IsAllowed, "the partner");
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Demolish("post"), 2, w, true).Refusal);
            // Running it (workers, priority) stays with its district's owner.
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("post"), 1, w, true).Refusal);
        });

        yield return ("Colony: founding and hand-over wait for the host's first tick, while players can still join", () =>
        {
            // Before the first tick a later joiner is sent the save without them (F1 of the alpha10 review).
            Check(ColonyRules.WaitsForStart(foundingOrHandover: true, hostTicksSinceLoad: 0));
            Check(!ColonyRules.WaitsForStart(true, 1));
            Check(!ColonyRules.WaitsForStart(true, 500));
            // Everything else at tick 0 closes joining instead (ReplayService), so it is never held back.
            Check(!ColonyRules.WaitsForStart(false, 0));
        });

        yield return ("Colony: the join check changes when a blueprint file changes, and matches between two copies", () =>
        {
            string root = Path.Combine(Path.GetTempPath(), "bb-digest-" + Guid.NewGuid().ToString("N"));
            try
            {
                Equal("none", BlueprintDigest.Of(null));
                Equal("none", BlueprintDigest.Of(Path.Combine(root, "missing")));
                Directory.CreateDirectory(Path.Combine(root, "a", "Buildings", "Post"));
                Directory.CreateDirectory(Path.Combine(root, "a", "TemplateCollections"));
                Check(BlueprintDigest.Of(Path.Combine(root, "a")) == "none", "empty folders");
                File.WriteAllText(Path.Combine(root, "a", "Buildings", "Post", "Post.json"), "{\"cost\": 10}");
                File.WriteAllText(Path.Combine(root, "a", "TemplateCollections", "T.json"), "[1]");
                string one = BlueprintDigest.Of(Path.Combine(root, "a"));
                Check(one != "none" && one != "error" && one.Length == 16, one);
                // A second install with the same files, under another path and separator style: the same check.
                Directory.CreateDirectory(Path.Combine(root, "b", "Buildings", "Post"));
                Directory.CreateDirectory(Path.Combine(root, "b", "TemplateCollections"));
                File.WriteAllText(Path.Combine(root, "b", "Buildings", "Post", "Post.json"), "{\"cost\": 10}");
                File.WriteAllText(Path.Combine(root, "b", "TemplateCollections", "T.json"), "[1]");
                Equal(one, BlueprintDigest.Of(Path.Combine(root, "b") + Path.DirectorySeparatorChar));
                // One byte changed (an edited price): refused at the join.
                File.WriteAllText(Path.Combine(root, "b", "Buildings", "Post", "Post.json"), "{\"cost\": 11}");
                Check(one != BlueprintDigest.Of(Path.Combine(root, "b")), "an edited blueprint gave the same check");
                // A file missing (only the DLL was copied into an old folder): refused too.
                File.Delete(Path.Combine(root, "a", "TemplateCollections", "T.json"));
                Check(one != BlueprintDigest.Of(Path.Combine(root, "a")), "a missing file gave the same check");
                // Files outside the blueprint folders (the DLLs, the docs) play no part.
                File.WriteAllText(Path.Combine(root, "b", "README.md"), "hello");
                File.WriteAllText(Path.Combine(root, "b", "Buildings", "Post", "Post.json"), "{\"cost\": 10}");
                File.WriteAllText(Path.Combine(root, "a", "TemplateCollections", "T.json"), "[1]");
                Equal(one, BlueprintDigest.Of(Path.Combine(root, "b")));
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        });

        yield return ("Colony: a player founds a colony once, where it joins no other colony's roads", () =>
        {
            Check(ColonyRules.JudgeFounding(actorHasSlot: true, actorOwnsDistrict: false, foundingAllowed: true, blocksValid: true, touchesOtherDistrict: false).IsAllowed);
            Equal(ColonyRefusal.CannotFound, ColonyRules.JudgeFounding(true, true, true, true, false).Refusal);
            Equal(ColonyRefusal.CannotFound, ColonyRules.JudgeFounding(false, false, true, true, false).Refusal);
            Equal(ColonyRefusal.CannotFound, ColonyRules.JudgeFounding(true, false, false, true, false).Refusal);
            Equal(ColonyRefusal.Blocked, ColonyRules.JudgeFounding(true, false, true, false, false).Refusal);
            Equal(ColonyRefusal.FoundingConflict, ColonyRules.JudgeFounding(true, false, true, true, true).Refusal);
            Equal(ColonyRefusal.OtherColonyArea, ColonyRules.JudgeFounding(true, false, true, true, false, onOtherColonyLand: true).Refusal);
            Equal(ColonyRefusal.TooCloseToColony, ColonyRules.JudgeFounding(true, false, true, true, false, tooCloseToColony: true).Refusal);
        });

        // ---- automatic migration ----

        yield return ("Colony: automatic migration only pairs districts of one owner", () =>
        {
            Check(ColonyModeState.SameOwner(0, 0));
            Check(!ColonyModeState.SameOwner(0, 1));
            Check(ColonyModeState.SameOwner(null, 1));
        });

        // ---- the land each colony works ----

        yield return ("Colony: a colony reaches every tile within 10 of its buildings, and no further", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (50, 50) }, +1);
            Check(grid.Reaches(0, 50, 50));
            Check(grid.Reaches(0, 60, 50), "10 tiles away");
            Check(grid.Reaches(0, 56, 58), "6 and 8 away: 10 as the crow flies");
            Check(!grid.Reaches(0, 61, 50), "11 tiles away");
            Check(!grid.Reaches(0, 58, 58), "8 and 8 away: more than 10");
            Check(!grid.Reaches(1, 50, 50), "only the building's own colony");
            // At the edge of the map nothing breaks, and nothing outside is reached.
            grid.Apply(1, new[] { (0, 0), (99, 99) }, +1);
            Check(grid.Reaches(1, 0, 10)); Check(!grid.Reaches(1, -1, 0)); Check(!grid.Reaches(1, 100, 99));
        });

        yield return ("Colony: a tile is the land of the colony that reached it first", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (20, 50) }, +1);
            grid.Apply(1, new[] { (35, 50) }, +1);
            // Both reach 25..30; colony 0 got there first, so it is colony 0's.
            Check(grid.Reaches(1, 28, 50));
            Equal<int?>(0, grid.Owner(28, 50));
            Check(grid.MayUse(0, 28, 50)); Check(!grid.MayUse(1, 28, 50));
            // Colony 1's own land, and land nobody holds.
            Equal<int?>(1, grid.Owner(40, 50));
            Check(!grid.MayUse(0, 40, 50)); Check(grid.MayUse(1, 40, 50));
            Equal<int?>(null, grid.Owner(80, 80));
            Check(grid.MayUse(0, 80, 80)); Check(grid.MayUse(1, 80, 80));
        });

        yield return ("Colony: a colony cannot build its way into another colony's land", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (20, 50) }, +1);
            // Colony 1 lays a path towards colony 0, one tile at a time, wherever it may.
            int x = 60;
            grid.Apply(1, new[] { (x, 50) }, +1);
            while (x > 0 && grid.MayUse(1, x - 1, 50))
            {
                x--;
                grid.Apply(1, new[] { (x, 50) }, +1);
            }
            Equal(31, x);
            // Colony 0's land is still all colony 0's, right up to its edge.
            for (int tx = 10; tx <= 30; tx++) Equal<int?>(0, grid.Owner(tx, 50));
        });

        yield return ("Colony: land passes on only when its colony no longer reaches it, and saved owners come back", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (20, 50) }, +1);
            grid.Apply(1, new[] { (35, 50) }, +1);
            // What saving keeps: the tiles both reach, with their owner.
            var contested = grid.ContestedTiles().ToList();
            Check(contested.Count > 0 && contested.All(t => t.slot == 0 && t.x >= 25 && t.x <= 30));
            // Built again in the other order, the shared tiles would be colony 1's; the saved owners put them back.
            var rebuilt = new ColonyReachGrid(100, 100);
            rebuilt.Apply(1, new[] { (35, 50) }, +1);
            rebuilt.Apply(0, new[] { (20, 50) }, +1);
            Equal<int?>(1, rebuilt.Owner(28, 50));
            foreach (var (tx, ty, slot) in contested) rebuilt.RestoreOwner(tx, ty, slot);
            Equal<int?>(0, rebuilt.Owner(28, 50));
            // Colony 0 takes its building down: the tiles colony 1 still reaches become colony 1's, the rest nobody's.
            grid.Apply(0, new[] { (20, 50) }, -1);
            Equal<int?>(1, grid.Owner(28, 50));
            Equal<int?>(null, grid.Owner(15, 50));
        });

        yield return ("Colony: a colony handed over gives its land and reach to the new owner", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (20, 50) }, +1);
            grid.Apply(1, new[] { (35, 50) }, +1);
            grid.Apply(2, new[] { (80, 80) }, +1);
            int before = grid.LandSize(1) + grid.LandSize(2);
            grid.Transfer(2, 1);
            // Colony 2's land and reach are colony 1's now; colony 0 keeps the shared tiles it got first.
            Equal<int?>(1, grid.Owner(80, 80));
            Check(grid.Reaches(1, 80, 80)); Check(!grid.Reaches(2, 80, 80));
            Equal(0, grid.LandSize(2));
            Equal(before, grid.LandSize(1));
            Equal<int?>(0, grid.Owner(28, 50));
            // Taking the building down later takes the reach from the new owner.
            grid.Apply(1, new[] { (80, 80) }, -1);
            Equal<int?>(null, grid.Owner(80, 80));
        });

        yield return ("Colony: the land's outline, and room to found a colony", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (50, 50) }, +1);
            var outline = grid.BorderTiles(0).ToList();
            Check(outline.Contains((60, 50)) && outline.Contains((40, 50)) && outline.Contains((50, 60)));
            Check(!outline.Contains((50, 50)), "the middle is not outline");
            Check(outline.All(t => grid.Owner(t.x, t.y) == 0));
            // A colony founded under 20 tiles from colony 0's building would have land touching colony 0's at once.
            Check(grid.OthersReachNear(1, new[] { (69, 50) }), "19 tiles from the building");
            Check(!grid.OthersReachNear(1, new[] { (71, 50) }), "21 tiles from the building");
            Check(!grid.OthersReachNear(0, new[] { (55, 50) }), "its own colony does not count");
        });

        yield return ("Colony: a building nobody stamped and no road reaches takes the land it stands on, if it is one colony's", () =>
        {
            var grid = new ColonyReachGrid(100, 100);
            grid.Apply(0, new[] { (20, 50) }, +1);
            grid.Apply(1, new[] { (35, 50) }, +1);
            // A 2 by 3 pump at the edge of colony 0's land, partly over land nobody holds: colony 0's.
            Equal<int?>(0, grid.SoleOwner(new[] { (20, 59), (21, 59), (20, 60), (21, 60), (20, 61), (21, 61) }));
            Equal<int?>(null, grid.Owner(20, 61));
            // Wholly on colony 1's land.
            Equal<int?>(1, grid.SoleOwner(new[] { (40, 50), (41, 50) }));
            // Across the line between the two: nobody's to decide, so it keeps waiting.
            Equal<int?>(null, grid.SoleOwner(new[] { (30, 50), (31, 50) }));
            // On nobody's land, or no tiles at all.
            Equal<int?>(null, grid.SoleOwner(new[] { (80, 80) }));
            Equal<int?>(null, grid.SoleOwner(Array.Empty<(int, int)>()));
        });

        yield return ("Colony: land stays quick at the size of a big game", () =>
        {
            // A 256 by 256 map, four colonies of 5000 building tiles each (paths and buildings).
            var grid = new ColonyReachGrid(256, 256);
            var random = new Random(4);
            var buildings = new List<(int slot, (int, int)[] tiles)>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int slot = 0; slot < 4; slot++)
            {
                int cx = 64 + (slot % 2) * 128, cy = 64 + (slot / 2) * 128;
                for (int i = 0; i < 5000; i++)
                {
                    var tile = new[] { (cx + random.Next(-50, 51), cy + random.Next(-50, 51)) };
                    grid.Apply(slot, tile, +1);
                    buildings.Add((slot, tile));
                }
            }
            long build = watch.ElapsedMilliseconds;
            watch.Restart();
            for (int i = 0; i < 500; i++) grid.Apply(buildings[i].slot, buildings[i].tiles, -1);
            for (int slot = 0; slot < 4; slot++) grid.BorderTiles(slot).Count();
            for (int i = 0; i < 1000; i++) grid.MayUse(i % 4, random.Next(256), random.Next(256));
            long work = watch.ElapsedMilliseconds;
            Console.WriteLine($"      Land for 20000 building tiles: {build} ms to build; removals, outlines and lookups {work} ms");
            Check(build < 5000, $"building the land took {build} ms");
            Check(work < 2000, $"changes and outlines took {work} ms");
        });

        yield return ("Colony: reach follows the buildings standing now, whatever order they came and went in", () =>
        {
            var a = new ColonyReachGrid(60, 60);
            a.Apply(0, new[] { (10, 10), (11, 10) }, +1);
            a.Apply(1, new[] { (30, 30) }, +1);
            a.Apply(0, new[] { (40, 40) }, +1);
            a.Apply(0, new[] { (10, 10), (11, 10) }, -1);
            var b = new ColonyReachGrid(60, 60);
            b.Apply(0, new[] { (40, 40) }, +1);
            b.Apply(1, new[] { (30, 30) }, +1);
            for (int x = 0; x < 60; x++)
            {
                for (int y = 0; y < 60; y++)
                {
                    for (int slot = 0; slot < 2; slot++)
                        Check(a.Reaches(slot, x, y) == b.Reaches(slot, x, y), $"slot {slot} at {x},{y}");
                }
            }
            // Two buildings next to each other: removing one keeps the other's land.
            var c = new ColonyReachGrid(60, 60);
            c.Apply(0, new[] { (20, 20) }, +1);
            c.Apply(0, new[] { (21, 20) }, +1);
            c.Apply(0, new[] { (21, 20) }, -1);
            Check(c.Reaches(0, 30, 20)); Check(!c.Reaches(0, 31, 20));
        });

        // ---- exchanges at a trading post ----

        yield return ("Colony: an exchange's terms name two different goods, up to 100 of each a round, not both 0", () =>
        {
            // Science and beavers are exchange items too; nobody carries them.
            Check(ExchangeTerms.IsSpecial(ExchangeTerms.Science)); Check(ExchangeTerms.IsSpecial(ExchangeTerms.Beavers));
            Check(!ExchangeTerms.IsSpecial("Log"));
            Equal(100, ExchangeTerms.MaxAmount);
            Check(ExchangeTerms.AreValid(ExchangeTerms.Science, 100, "Plank", 100));
            Check(ExchangeTerms.AreValid("Berries", 100, ExchangeTerms.Beavers, 3));
            Check(ExchangeTerms.AreValid("Log", 100, "Gear", 25));
            Check(!ExchangeTerms.AreValid("Log", 101, "Gear", 25), "more than a half holds");
            Check(!ExchangeTerms.AreValid("Log", 10, "Gear", 1000), "more than a half holds");
            Check(!ExchangeTerms.AreValid("Log", 100, "Log", 25), "the same good both ways");
            Check(!ExchangeTerms.AreValid("Log", 0, "Gear", 0), "nothing either way");
            Check(!ExchangeTerms.AreValid("Log", -1, "Gear", 5));
            Check(!ExchangeTerms.AreValid(null, 10, "Gear", 5), "an amount without a good");
            // A gift (asking for nothing) and a request (giving nothing) are exchanges too.
            Check(ExchangeTerms.AreValid("Log", 100, null, 0));
            Check(ExchangeTerms.AreValid("Log", 0, "Gear", 25));
            Check(ExchangeTerms.AreValid("Log", 30, "Log", 0), "a gift's unused good does not count");
            Equal(null, ExchangeTerms.GoodOf("Log", 0));
            Equal("Log", ExchangeTerms.GoodOf("Log", 1));
        });

        yield return ("Colony: an exchange runs 1 to 99 rounds, or round after round until both colonies end it", () =>
        {
            Check(!ExchangeTerms.AreValidRounds(0)); Check(ExchangeTerms.AreValidRounds(1));
            Check(ExchangeTerms.AreValidRounds(99)); Check(!ExchangeTerms.AreValidRounds(100));
            // After the first of three rounds crossed there are two more; after the third, none.
            Check(ExchangeTerms.HasAnotherRound(3, 1, repeat: false));
            Check(ExchangeTerms.HasAnotherRound(3, 2, repeat: false));
            Check(!ExchangeTerms.HasAnotherRound(3, 3, repeat: false));
            Check(!ExchangeTerms.HasAnotherRound(1, 1, repeat: false));
            Check(ExchangeTerms.HasAnotherRound(1, 500, repeat: true));
        });

        yield return ("Colony: a round's goods wait on their own half: workers bring what is missing, and no more is held", () =>
        {
            // Nothing on the half yet: bring all of it.
            Equal(100, ExchangeTerms.StillToBring(100, 0, 0));
            // Some already waits, some is on the way.
            Equal(40, ExchangeTerms.StillToBring(100, 50, 10));
            Equal(0, ExchangeTerms.StillToBring(100, 60, 40));
            Equal(0, ExchangeTerms.StillToBring(100, 100, 0));
            Check(ExchangeTerms.StillToBring(0, 0, 0) == 0, "a side giving nothing brings nothing");
            // What arrives is held only up to the round's amount; the rest goes home.
            Equal(15, ExchangeTerms.ToHold(100, 80, 15));
            Equal(20, ExchangeTerms.ToHold(100, 80, 35));
            Equal(0, ExchangeTerms.ToHold(100, 100, 5));
            Check(!ExchangeTerms.IsDelivered(100, 99)); Check(ExchangeTerms.IsDelivered(100, 100)); Check(ExchangeTerms.IsDelivered(0, 0));
        });

        yield return ("Colony: rounds with uneven loads always fill both halves, never past their amount", () =>
        {
            // Beavers carry uneven loads, one side at a time, in a random order, some still on the way when others
            // arrive. Every round must end with exactly each side's amount waiting on its half.
            var random = new Random(20260921);
            for (int round = 0; round < 500; round++)
            {
                int totalA = random.Next(0, 101), totalB = random.Next(0, 101);
                if (totalA == 0 && totalB == 0) totalB = 1;
                int heldA = 0, heldB = 0, wayA = 0, wayB = 0, steps = 0;
                while (!(ExchangeTerms.IsDelivered(totalA, heldA) && ExchangeTerms.IsDelivered(totalB, heldB)))
                {
                    Check(++steps < 100000, $"stuck at {heldA}/{totalA} and {heldB}/{totalB}");
                    bool sideA = random.Next(2) == 0;
                    int total = sideA ? totalA : totalB, held = sideA ? heldA : heldB, way = sideA ? wayA : wayB;
                    // Either a worker sets out with a load, or a load on the way arrives.
                    if (way > 0 && random.Next(2) == 0)
                    {
                        int arriving = Math.Min(way, random.Next(1, 16));
                        held += ExchangeTerms.ToHold(total, held, arriving);
                        way -= arriving;
                    }
                    else
                    {
                        int wanted = ExchangeTerms.StillToBring(total, held, way);
                        if (wanted > 0) way += Math.Min(wanted, random.Next(1, 16));
                    }
                    Check(held <= total, "a half held more than its side");
                    Check(held + way <= total, "workers set out with more than the round needs");
                    if (sideA) { heldA = held; wayA = way; } else { heldB = held; wayB = way; }
                }
                Equal(totalA, heldA);
                Equal(totalB, heldB);
            }
        });

        yield return ("Colony: a district gives only beavers able to move, and its last adult always stays", () =>
        {
            Equal(4, ExchangeTerms.BeaversToSpare(5, 5));
            Check(ExchangeTerms.BeaversToSpare(1, 1) == 0, "the last adult stays");
            Equal(0, ExchangeTerms.BeaversToSpare(0, 0));
            // Contaminated adults do not move, but they are adults: they can be the one who stays.
            Equal(2, ExchangeTerms.BeaversToSpare(5, 2));
            Equal(1, ExchangeTerms.BeaversToSpare(2, 1));
            Equal(0, ExchangeTerms.BeaversToSpare(3, 0));
        });

        // ---- the trading post's offer form ----

        yield return ("Colony: an amount box holds a whole number from 0 to 100, and empty means 0", () =>
        {
            foreach (var (text, amount) in new[] { ("", 0), ("  ", 0), (null, 0), ("0", 0), ("100", 100), (" 42 ", 42), ("050", 50) })
            {
                Check(TradeOfferForm.TryReadAmount(text, out int read), $"[{text}] should read");
                Equal(amount, read);
            }
            foreach (string text in new[] { "101", "9999", "-5", "+5", "1,000", "1.5", "12a", "1e3", "٣" })
                Check(!TradeOfferForm.TryReadAmount(text, out _), $"[{text}] should not read");
            foreach (var (text, rounds) in new[] { ("1", 1), (" 3 ", 3), ("99", 99), ("07", 7) })
            {
                Check(TradeOfferForm.TryReadRounds(text, out int read), $"[{text}] rounds should read");
                Equal(rounds, read);
            }
            foreach (string text in new[] { "", "0", "100", "-1", "x", null })
                Check(!TradeOfferForm.TryReadRounds(text, out _), $"[{text}] rounds should not read");
        });

        yield return ("Colony: the offer form says what an offer is, or what is wrong with it", () =>
        {
            TradeOfferForm.Verdict Judge(string giveItem, string giveText, string getItem, string getText, string roundsText = "1",
                bool repeat = false) => TradeOfferForm.Judge(giveItem, giveText, getItem, getText, roundsText, repeat, out _, out _, out _);
            Equal(TradeOfferForm.Verdict.Exchange, Judge("Log", "100", "Gear", "25"));
            Equal(TradeOfferForm.Verdict.Gift, Judge("Log", "30", "Gear", "0"));
            Equal(TradeOfferForm.Verdict.Gift, Judge("Log", "30", "Gear", ""));
            Equal(TradeOfferForm.Verdict.Request, Judge("Log", "0", "Gear", "25"));
            Equal(TradeOfferForm.Verdict.NothingEitherWay, Judge("Log", "0", "Gear", ""));
            Equal(TradeOfferForm.Verdict.SameItem, Judge("Log", "10", "Log", "10"));
            Equal(TradeOfferForm.Verdict.Gift, Judge("Log", "10", "Log", "0"));
            Equal(TradeOfferForm.Verdict.BadAmount, Judge("Log", "1000", "Gear", "5"));
            Equal(TradeOfferForm.Verdict.BadAmount, Judge("Log", "5", "Gear", "lots"));
            Equal(TradeOfferForm.Verdict.NoItem, Judge(null, "5", "Gear", "5"));
            Equal(TradeOfferForm.Verdict.Exchange, Judge(ExchangeTerms.Science, "100", ExchangeTerms.Beavers, "2"));
            Equal(TradeOfferForm.Verdict.BadRounds, Judge("Log", "100", "Gear", "25", "0"));
            Equal(TradeOfferForm.Verdict.BadRounds, Judge("Log", "100", "Gear", "25", "100"));
            Equal(TradeOfferForm.Verdict.Exchange, Judge("Log", "100", "Gear", "25", "99"));
            // A repeating offer ignores the rounds box.
            Equal(TradeOfferForm.Verdict.Exchange, Judge("Log", "100", "Gear", "25", "", repeat: true));
            TradeOfferForm.Judge("Log", "100", "Gear", "25", "7", false, out _, out _, out int seven);
            Equal(7, seven);

            // Whatever is typed, the form offers exactly what an exchange accepts, with the numbers it read.
            var random = new Random(20260921);
            string[] items = { "Log", "Gear", ExchangeTerms.Science, ExchangeTerms.Beavers, null, "" };
            string[] texts = { "", "0", "1", "10", "100", "101", "250", "-1", "x", " 7 " };
            string[] roundTexts = { "", "0", "1", "2", "50", "99", "100", "x", " 3 " };
            for (int i = 0; i < 5000; i++)
            {
                string giveItem = items[random.Next(items.Length)], getItem = items[random.Next(items.Length)];
                string giveText = random.Next(4) == 0 ? texts[random.Next(texts.Length)] : random.Next(0, 130).ToString();
                string getText = random.Next(4) == 0 ? texts[random.Next(texts.Length)] : random.Next(0, 130).ToString();
                string roundsText = roundTexts[random.Next(roundTexts.Length)];
                bool repeat = random.Next(3) == 0;
                var verdict = TradeOfferForm.Judge(giveItem, giveText, getItem, getText, roundsText, repeat, out int give, out int get, out int rounds);
                bool read = TradeOfferForm.TryReadAmount(giveText, out int g) & TradeOfferForm.TryReadAmount(getText, out int a);
                bool roundsRead = TradeOfferForm.TryReadRounds(roundsText, out int r);
                bool valid = read && ExchangeTerms.AreValid(giveItem, g, getItem, a) && (repeat || roundsRead);
                Check(TradeOfferForm.IsOffer(verdict) == valid,
                    $"{giveText} {giveItem} for {getText} {getItem} x[{roundsText}]{(repeat ? " repeating" : "")}: the form says {verdict}, an exchange says {(valid ? "valid" : "not valid")}");
                if (TradeOfferForm.IsOffer(verdict))
                {
                    Equal(g, give); Equal(a, get);
                    Equal(repeat ? 1 : r, rounds);
                    Check(repeat || ExchangeTerms.AreValidRounds(rounds));
                }
            }
        });

        yield return ("Colony: − and + go to the next whole step and stay within their box's range", () =>
        {
            Equal(10, TradeOfferForm.Step("Log", shift: false));
            Equal(1, TradeOfferForm.Step("Log", shift: true));
            Equal(10, TradeOfferForm.Step(ExchangeTerms.Science, shift: false));
            Equal(1, TradeOfferForm.Step(ExchangeTerms.Beavers, shift: false));
            Equal(10, TradeOfferForm.Step(ExchangeTerms.Beavers, shift: true));
            Equal(1, TradeOfferForm.RoundsStep(false));
            Equal(10, TradeOfferForm.RoundsStep(true));
            Equal(100, TradeOfferForm.Stepped(95, 10, up: true));
            Equal(90, TradeOfferForm.Stepped(95, 10, up: false));
            Check(TradeOfferForm.Stepped(100, 10, up: true) == 100, "no more than a half holds");
            Equal(90, TradeOfferForm.Stepped(100, 10, up: false));
            Equal(0, TradeOfferForm.Stepped(0, 10, up: false));
            Equal(1, TradeOfferForm.Stepped(0, 1, up: true));
            Equal(99, TradeOfferForm.Stepped(98, 1, up: true));
            // Rounds: from 1 to 99.
            Equal(1, TradeOfferForm.Stepped(1, 1, up: false, 1, ExchangeTerms.MaxRounds));
            Equal(10, TradeOfferForm.Stepped(1, 10, up: true, 1, ExchangeTerms.MaxRounds));
            Equal(99, TradeOfferForm.Stepped(95, 10, up: true, 1, ExchangeTerms.MaxRounds));
            Equal(1, TradeOfferForm.Stepped(5, 10, up: false, 1, ExchangeTerms.MaxRounds));
            for (int amount = 0; amount <= ExchangeTerms.MaxAmount; amount += 3)
            {
                foreach (int step in new[] { 1, 10 })
                {
                    int up = TradeOfferForm.Stepped(amount, step, up: true), down = TradeOfferForm.Stepped(amount, step, up: false);
                    Check(up > amount || amount == ExchangeTerms.MaxAmount, $"+ from {amount} by {step} gave {up}");
                    Check(down < amount || amount == 0, $"- from {amount} by {step} gave {down}");
                    Check(up - amount <= step && amount - down <= step, $"{amount} by {step} jumped to {down} or {up}");
                    Check(up % step == 0 || up == ExchangeTerms.MaxAmount, $"+ from {amount} by {step} is not a whole step: {up}");
                    Check(down % step == 0, $"- from {amount} by {step} is not a whole step: {down}");
                    Check(up <= ExchangeTerms.MaxAmount && down >= 0);
                }
            }
        });

        yield return ("Colony: every text the trading post shows has an English line", () =>
        {
            string root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "BeaverBuddies.sln"))) root = Path.GetDirectoryName(root);
            Check(root != null, "could not find the repository root");
            string mod = Path.Combine(root!, "BeaverBuddies");
            var lines = new HashSet<string>(File.ReadAllLines(Path.Combine(mod, "Localizations", "enUS_BeaverBuddie.csv"))
                .Select(line => line.Split(',')[0]));
            var keys = Directory.GetFiles(Path.Combine(mod, "Colonies"), "*.cs")
                .SelectMany(file => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), "\"(BeaverBuddies\\.Colony\\.[A-Za-z.]+)\"")
                    .Select(match => match.Groups[1].Value))
                .Where(key => !key.EndsWith(".")).Distinct().ToList();
            Check(keys.Count > 60, "the trading post's texts were not found: " + keys.Count);
            // The Trading Post building's own name, description and flavour line, named by its blueprints.
            var blueprintKeys = Directory.GetFiles(Path.Combine(mod, "Buildings"), "*.blueprint.json", SearchOption.AllDirectories)
                .SelectMany(file => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"""[A-Za-z]*LocKey""\s*:\s*""([^""]+)""")
                    .Select(match => match.Groups[1].Value)).Distinct().ToList();
            Check(blueprintKeys.Count == 3, "the Trading Post's blueprint texts were not found: " + blueprintKeys.Count);
            keys.AddRange(blueprintKeys);
            var missing = keys.Where(key => !lines.Contains(key)).ToList();
            Check(missing.Count == 0, "no English line for " + string.Join(", ", missing));
        });
    }

    static JObject TypedGroup(params JObject[] children) => new JObject
    {
        ["$type"] = "BeaverBuddies.GroupedEvent, BeaverBuddies",
        [TimberNetBase.TYPE_KEY] = "GroupedEvent",
        [TimberNetBase.TICKS_KEY] = 0,
        ["events"] = new JObject
        {
            ["$type"] = "System.Collections.Generic.List`1[[BeaverBuddies.Events.ReplayEvent, BeaverBuddies]], mscorlib",
            ["$values"] = new JArray(children),
        },
    };

    static ColonyPlacement Place(string template) => new ColonyPlacement { TemplateName = template };

    sealed class FakeWorld : IColonyWorld
    {
        readonly Dictionary<string, int> owners = new();
        readonly Dictionary<string, int[]> crossings = new();
        readonly HashSet<(int, string)> locked = new();
        readonly HashSet<ColonyTile> othersTiles = new();
        readonly Dictionary<string, ColonyRefusal> conflicts = new();

        public FakeWorld Own(string id, int slot) { owners[id] = slot; return this; }
        public FakeWorld Crossing(string id, params int[] partners) { crossings[id] = partners; return this; }
        public FakeWorld Locked(int slot, string template) { locked.Add((slot, template)); return this; }
        public FakeWorld OthersTile(ColonyTile tile) { othersTiles.Add(tile); return this; }
        public FakeWorld Conflict(string template, ColonyRefusal refusal) { conflicts[template] = refusal; return this; }

        public int? OwnerOf(string entityId) => owners.TryGetValue(entityId, out int slot) ? slot : null;
        public bool IsCrossingOf(int slot, string entityId) =>
            crossings.TryGetValue(entityId, out int[] partners) && (partners.Length == 0 || partners.Contains(slot));
        public bool IsUnlockedFor(int slot, string templateName) => !locked.Contains((slot, templateName));
        public bool MayUseTile(int slot, ColonyTile tile) => !othersTiles.Contains(tile);
        public ColonyRefusal PlacementConflict(int slot, ColonyPlacement placement, out string detail)
        {
            detail = null;
            return conflicts.TryGetValue(placement.TemplateName, out ColonyRefusal refusal) ? refusal : ColonyRefusal.None;
        }
    }
}
