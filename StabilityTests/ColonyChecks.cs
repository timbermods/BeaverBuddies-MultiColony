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
            Check(ColonyRules.Judge(ColonyScope.Entities("mine", "dc1"), 1, w, Present(0, 1), true).IsAllowed);
            // A tree, a building cut off from roads: nobody's.
            Check(ColonyRules.Judge(ColonyScope.Entities("loose"), 1, w, Present(0, 1), true).IsAllowed);
            // Missing and empty ids are left to the event.
            Check(ColonyRules.Judge(ColonyScope.Entities("gone", null!, ""), 1, w, Present(0, 1), true).IsAllowed);
        });

        yield return ("Colony: another player's things are refused while they play, and editable while they are away", () =>
        {
            var w = new FakeWorld().Own("theirs", 0);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("theirs"), 1, w, Present(0, 1), true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Entities("theirs"), 1, w, Present(1), true).IsAllowed);
            // A player not seated yet (slot -1) may change nothing that is owned.
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("theirs"), -1, w, Present(0), true).Refusal);
        });

        yield return ("Colony: anyone may demolish a District Crossing, but not run the other side's half", () =>
        {
            var w = new FakeWorld().Own("crossingB", 0).Crossing("crossingB").Own("houseB", 0);
            Check(ColonyRules.Judge(ColonyScope.Demolish("crossingB"), 1, w, Present(0, 1), true).IsAllowed);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("crossingB"), 1, w, Present(0, 1), true).Refusal);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Demolish("houseB"), 1, w, Present(0, 1), true).Refusal);
        });

        yield return ("Colony: beavers may be sent to another colony, but not taken from it", () =>
        {
            var w = new FakeWorld().Own("dcA", 0).Own("dcB", 1);
            Check(ColonyRules.Judge(ColonyScope.Migration("dcB", "dcA"), 1, w, Present(0, 1), true).IsAllowed);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Migration("dcA", "dcB"), 1, w, Present(0, 1), true).Refusal);
        });

        yield return ("Colony: building is allowed anywhere, if your colony has it unlocked", () =>
        {
            var w = new FakeWorld().Locked(1, "Observatory");
            Check(ColonyRules.Judge(ColonyScope.Place(Place("House")), 1, w, Present(0, 1), true).IsAllowed);
            Equal(ColonyRefusal.Locked, ColonyRules.Judge(ColonyScope.Place(Place("Observatory")), 1, w, Present(0, 1), true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Observatory")), 0, w, Present(0, 1), true).IsAllowed);
        });

        yield return ("Colony: map areas are shared", () =>
        {
            var tiles = new List<(int, int, int)> { (1, 1, 1), (99, 99, 1) };
            var scope = ColonyScope.TileList(tiles);
            Check(ColonyRules.Judge(scope, 1, new FakeWorld(), Present(0, 1), true).IsAllowed);
            Equal(2, tiles.Count);
        });

        yield return ("Colony: a list of things is cut down to yours and nobody's, in order", () =>
        {
            var w = new FakeWorld().Own("a", 1).Own("b", 0).Own("c", 1);
            var ids = new List<string> { "b", "a", "loose", "c" };
            var judged = ColonyRules.Judge(ColonyScope.EntityList(ids, id => id), 1, w, Present(0, 1), false);
            Check(judged.IsAllowed); Equal(1, judged.Removed); Equal(4, ids.Count);
            var v = ColonyRules.Judge(ColonyScope.EntityList(ids, id => id), 1, w, Present(0, 1), true);
            Check(v.IsAllowed); Equal(1, v.Removed);
            Check(ids.SequenceEqual(new[] { "a", "loose", "c" }));
            Equal(ColonyRefusal.NothingOwn, ColonyRules.Judge(ColonyScope.EntityList(new List<string> { "b" }, id => id), 1, w, Present(0, 1), true).Refusal);
            // Demolition lists may include a crossing.
            var dem = new List<string> { "x" };
            Check(ColonyRules.Judge(ColonyScope.EntityList(dem, id => id, demolition: true), 1, new FakeWorld().Own("x", 0).Crossing("x"), Present(0, 1), true).IsAllowed);
        });

        yield return ("Colony: shared actions are always allowed", () =>
        {
            Check(ColonyRules.Judge(ColonyScope.Global, -1, new FakeWorld(), Present(0), true).IsAllowed);
        });

        // ---- founding ----

        yield return ("Colony: a player founds a colony once, where it joins no other colony's roads", () =>
        {
            Check(ColonyRules.JudgeFounding(actorHasSlot: true, actorOwnsDistrict: false, foundingAllowed: true, blocksValid: true, touchesOtherDistrict: false).IsAllowed);
            Equal(ColonyRefusal.CannotFound, ColonyRules.JudgeFounding(true, true, true, true, false).Refusal);
            Equal(ColonyRefusal.CannotFound, ColonyRules.JudgeFounding(false, false, true, true, false).Refusal);
            Equal(ColonyRefusal.CannotFound, ColonyRules.JudgeFounding(true, false, false, true, false).Refusal);
            Equal(ColonyRefusal.Blocked, ColonyRules.JudgeFounding(true, false, true, false, false).Refusal);
            Equal(ColonyRefusal.FoundingConflict, ColonyRules.JudgeFounding(true, false, true, true, true).Refusal);
        });

        // ---- automatic migration ----

        yield return ("Colony: automatic migration only pairs districts of one owner", () =>
        {
            Check(ColonyModeState.SameOwner(0, 0));
            Check(!ColonyModeState.SameOwner(0, 1));
            Check(ColonyModeState.SameOwner(null, 1));
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

    static Func<int, bool> Present(params int[] slots) => slot => slots.Contains(slot);

    sealed class FakeWorld : IColonyWorld
    {
        readonly Dictionary<string, int> owners = new();
        readonly HashSet<string> crossings = new();
        readonly HashSet<(int, string)> locked = new();

        public FakeWorld Own(string id, int slot) { owners[id] = slot; return this; }
        public FakeWorld Crossing(string id) { crossings.Add(id); return this; }
        public FakeWorld Locked(int slot, string template) { locked.Add((slot, template)); return this; }

        public int? OwnerOf(string entityId) => owners.TryGetValue(entityId, out int slot) ? slot : null;
        public bool IsCrossing(string entityId) => crossings.Contains(entityId);
        public bool IsUnlockedFor(int slot, string templateName) => !locked.Contains((slot, templateName));
    }
}
