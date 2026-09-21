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

        // ---- 3. seats ----

        yield return ("Colony: the host plays its chosen colony and every guest plays the other", () =>
        {
            Equal(1, ColonySeats.ColonyOfPlayer(0, 1));
            Equal(2, ColonySeats.ColonyOfPlayer(1, 1));
            Equal(2, ColonySeats.ColonyOfPlayer(0, 2));
            Equal(1, ColonySeats.ColonyOfPlayer(1, 2));
            // A third player shares the guest colony.
            Equal(2, ColonySeats.ColonyOfPlayer(2, 1));
            Equal(1, ColonySeats.ColonyOfPlayer(5, 2));
            // An event the host could not attribute controls nothing.
            Equal(0, ColonySeats.ColonyOfPlayer(-1, 1));
            Equal(1, ColonySeats.Normalize(0));
            Equal(2, ColonySeats.Normalize(2));
            Equal(1, ColonySeats.Normalize(7));
        });

        // ---- 4. territory ----

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
                    touching = t.IsStrip(x, y) && t.IsStrip(x + 1, y) && t.OwnerOf(x, y) != t.OwnerOf(x + 1, y);
                Check(touching, "no back-to-back strip tiles");
            }
        });

        // ---- 6. saved mode state ----

        yield return ("Colony: the mode is off without at least two starts", () =>
        {
            Check(!ColonyModeState.IsUsable(true, new ColonyTile[0]));
            Check(!ColonyModeState.IsUsable(true, new[] { new ColonyTile(1, 1) }));
            Check(!ColonyModeState.IsUsable(false, new[] { new ColonyTile(1, 1), new ColonyTile(9, 9) }));
            Check(ColonyModeState.IsUsable(true, new[] { new ColonyTile(1, 1), new ColonyTile(9, 9) }));
            Check(!ColonyModeState.IsUsable(true, null));
        });

        // ---- 7. entity and district actions ----

        yield return ("Colony: an action on your own building or district is allowed", () =>
        {
            var w = new FakeWorld().Put("mine", 12, 10).Put("dc1", 10, 10);
            Check(ColonyRules.Judge(ColonyScope.Entities("mine"), 1, TwoColonies(), w, true).IsAllowed);
            Check(ColonyRules.Judge(ColonyScope.Entities("dc1", "mine"), 1, TwoColonies(), w, true).IsAllowed);
        });

        yield return ("Colony: an action on the other colony's building or district is refused", () =>
        {
            var w = new FakeWorld().Put("theirs", 28, 10).Put("dc1", 10, 10).Put("dc2", 30, 10);
            var v = ColonyRules.Judge(ColonyScope.Entities("theirs"), 1, TwoColonies(), w, true);
            Equal(ColonyRefusal.OtherColony, v.Refusal);
            // Manual migration names both districts: sending beavers to the other colony is refused.
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("dc1", "dc2"), 1, TwoColonies(), w, true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Entities("dc2"), 2, TwoColonies(), w, true).IsAllowed);
        });

        yield return ("Colony: a missing entity or empty id is left to the event to handle", () =>
        {
            var w = new FakeWorld();
            Check(ColonyRules.Judge(ColonyScope.Entities("gone", null!, ""), 1, TwoColonies(), w, true).IsAllowed);
        });

        yield return ("Colony: shared actions are always allowed, and a player without a colony may do nothing else", () =>
        {
            var w = new FakeWorld().Put("mine", 12, 10);
            Check(ColonyRules.Judge(ColonyScope.Global, 0, TwoColonies(), w, true).IsAllowed);
            Equal(ColonyRefusal.OtherColony, ColonyRules.Judge(ColonyScope.Entities("mine"), 0, TwoColonies(), w, true).Refusal);
        });

        // ---- 8. placement ----

        yield return ("Colony: a building wholly on your own land is allowed", () =>
        {
            var w = new FakeWorld().Foot("House", (5, 5), (6, 5), (5, 6), (6, 6));
            Check(ColonyRules.Judge(ColonyScope.Place(Place("House")), 1, TwoColonies(), w, true).IsAllowed);
        });

        yield return ("Colony: a building with one tile across the border is refused", () =>
        {
            var w = new FakeWorld().Foot("House", (19, 5), (20, 5), (21, 5));
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("House")), 1, TwoColonies(), w, true).Refusal);
            // The same building is also on colony 1's land, so colony 2 cannot place it either.
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("House")), 2, TwoColonies(), w, true).Refusal);
        });

        yield return ("Colony: the strip takes a District Crossing half but nothing else", () =>
        {
            var w = new FakeWorld().Foot("Path", (20, 5)).Foot("Crossing", (20, 5)).Crossing("Crossing")
                .Foot("Crossing2", (21, 5)).Crossing("Crossing2").Foot("DeepCrossing", (19, 5), (20, 5)).Crossing("DeepCrossing");
            Equal(ColonyRefusal.BorderStrip, ColonyRules.Judge(ColonyScope.Place(Place("Path")), 1, TwoColonies(), w, true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Crossing")), 1, TwoColonies(), w, true).IsAllowed);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Crossing2")), 2, TwoColonies(), w, true).IsAllowed);
            // A half deeper than one tile may reach back into its own land.
            Check(ColonyRules.Judge(ColonyScope.Place(Place("DeepCrossing")), 1, TwoColonies(), w, true).IsAllowed);
        });

        yield return ("Colony: one click places a crossing pair, and the half across the border must be back to back with yours", () =>
        {
            // The game places both halves at once. Colony 1's half is at x = 20; the other half faces it from x = 21.
            var w = new FakeWorld()
                .Foot("Near", (20, 5), (20, 6), (20, 7)).Crossing("Near").Back("Near", 1, 0)
                .Foot("Far", (21, 5), (21, 6), (21, 7)).Crossing("Far").Back("Far", -1, 0)
                .Foot("FacingAway", (21, 5), (21, 6), (21, 7)).Crossing("FacingAway").Back("FacingAway", 1, 0)
                .Foot("Deep", (25, 5), (25, 6), (25, 7)).Crossing("Deep").Back("Deep", -1, 0)
                .Foot("NoBack", (21, 5)).Crossing("NoBack")
                .Foot("Path", (21, 5)).Back("Path", -1, 0)
                .Foot("Straddling", (20, 5), (21, 5)).Crossing("Straddling").Back("Straddling", 0, 1)
                .StandingCrossing((20, 5), (20, 6), (20, 7));
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Near")), 1, TwoColonies(), w, true).IsAllowed);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Far")), 1, TwoColonies(), w, true).IsAllowed);
            // Colony 2 placing the same pair from its side: its own half ("Far") is fine; the half on colony 1's
            // side needs colony 2's half behind it, which this world does not have yet.
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("Near")), 2, TwoColonies(), w, true).Refusal);
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Far")), 2, TwoColonies(), w, true).IsAllowed);
            // For colony 2 the half across the border is "Near", backed by colony 2's half at x = 21.
            Check(ColonyRules.Judge(ColonyScope.Place(Place("Near")), 2, TwoColonies(),
                new FakeWorld().Foot("Near", (20, 5), (20, 6), (20, 7)).Crossing("Near").Back("Near", 1, 0).StandingCrossing((21, 5), (21, 6), (21, 7)), true).IsAllowed);
            // A half on the other side that does not face your border, or stands inside their land, is refused.
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("FacingAway")), 1, TwoColonies(), w, true).Refusal);
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("Deep")), 1, TwoColonies(), w, true).Refusal);
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("NoBack")), 1, TwoColonies(), w, true).Refusal);
            // A half on the other side with no half of yours behind it (the game placed it alone) is refused.
            var lone = new FakeWorld().Foot("Far", (21, 5), (21, 6), (21, 7)).Crossing("Far").Back("Far", -1, 0);
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("Far")), 1, TwoColonies(), lone, true).Refusal);
            // A half must stand wholly on one side.
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("Straddling")), 1, TwoColonies(), w, true).Refusal);
            // Only a crossing may reach across: a path with the same geometry is refused.
            Equal(ColonyRefusal.OutsideLand, ColonyRules.Judge(ColonyScope.Place(Place("Path")), 1, TwoColonies(), w, true).Refusal);
        });

        yield return ("Colony: a building whose footprint cannot be worked out is refused", () =>
        {
            Equal(ColonyRefusal.UnknownFootprint, ColonyRules.Judge(ColonyScope.Place(Place("Unknown")), 1, TwoColonies(), new FakeWorld(), true).Refusal);
        });

        // ---- 9. area actions ----

        yield return ("Colony: an area across the border is cut down to your own tiles, in order", () =>
        {
            var tiles = new List<(int x, int y, int z)> { (18, 1, 3), (21, 1, 3), (19, 1, 4), (25, 2, 3), (20, 2, 3) };
            var scope = ColonyScope.TileList(tiles, t => new ColonyTile(t.x, t.y));
            var judged = ColonyRules.Judge(scope, 1, TwoColonies(), new FakeWorld(), false);
            Check(judged.IsAllowed); Equal(2, judged.Removed);
            // Judging without rewriting leaves the list alone (a guest's own check).
            Equal(5, tiles.Count);
            var v = ColonyRules.Judge(scope, 1, TwoColonies(), new FakeWorld(), true);
            Check(v.IsAllowed); Equal(2, v.Removed);
            Check(tiles.SequenceEqual(new List<(int, int, int)> { (18, 1, 3), (19, 1, 4), (20, 2, 3) }));
        });

        yield return ("Colony: an entity list keeps your own and missing entities, in order", () =>
        {
            var w = new FakeWorld().Put("a", 1, 1).Put("b", 29, 1).Put("c", 2, 2);
            var ids = new List<string> { "b", "a", "gone", "c" };
            var v = ColonyRules.Judge(ColonyScope.EntityList(ids, id => id), 1, TwoColonies(), w, true);
            Check(v.IsAllowed); Equal(1, v.Removed);
            Check(ids.SequenceEqual(new[] { "a", "gone", "c" }));
        });

        yield return ("Colony: an area wholly on the other colony's land is refused", () =>
        {
            var tiles = new List<ColonyTile> { new(25, 1), new(26, 1) };
            var v = ColonyRules.Judge(ColonyScope.TileList(tiles, t => t), 1, TwoColonies(), new FakeWorld(), true);
            Equal(ColonyRefusal.NothingOwn, v.Refusal);
            Equal(2, tiles.Count);
            var w = new FakeWorld().Put("b", 29, 1);
            Equal(ColonyRefusal.NothingOwn, ColonyRules.Judge(ColonyScope.EntityList(new List<string> { "b" }, id => id), 1, TwoColonies(), w, true).Refusal);
            // An empty list is nobody's: the event does nothing either way.
            Check(ColonyRules.Judge(ColonyScope.TileList(new List<ColonyTile>(), t => t), 1, TwoColonies(), w, true).IsAllowed);
        });

        // ---- 10. automatic migration between colonies ----

        yield return ("Colony: automatic migration only pairs districts of one colony", () =>
        {
            var t = TwoColonies();
            Check(ColonyModeState.SameColony(t, new ColonyTile(10, 10), new ColonyTile(15, 3)));
            Check(!ColonyModeState.SameColony(t, new ColonyTile(10, 10), new ColonyTile(30, 10)));
            // With the mode off there is no territory, and every pair is allowed as in the game.
            Check(ColonyModeState.SameColony(null, new ColonyTile(10, 10), new ColonyTile(30, 10)));
        });
    }

    // A GroupedEvent as the mod's JSON settings write it: type names on, so the list is wrapped.
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
        readonly Dictionary<string, ColonyTile> entities = new();
        readonly Dictionary<string, List<ColonyTile>> footprints = new();
        readonly HashSet<string> crossings = new();
        readonly Dictionary<string, ColonyTile> backs = new();
        readonly HashSet<ColonyTile> standing = new();

        public FakeWorld Put(string id, int x, int y) { entities[id] = new ColonyTile(x, y); return this; }
        public FakeWorld Foot(string template, params (int x, int y)[] tiles)
        {
            footprints[template] = tiles.Select(t => new ColonyTile(t.x, t.y)).ToList();
            return this;
        }
        public FakeWorld Crossing(string template) { crossings.Add(template); return this; }
        public FakeWorld Back(string template, int dx, int dy) { backs[template] = new ColonyTile(dx, dy); return this; }
        public FakeWorld StandingCrossing(params (int x, int y)[] tiles) { foreach (var t in tiles) standing.Add(new ColonyTile(t.x, t.y)); return this; }

        public ColonyTile? EntityTile(string entityId) => entities.TryGetValue(entityId, out var tile) ? tile : null;
        public IReadOnlyList<ColonyTile> Footprint(ColonyPlacement placement) =>
            footprints.TryGetValue(placement.TemplateName, out var tiles) ? tiles : null!;
        public bool IsCrossing(string templateName) => crossings.Contains(templateName);
        public bool HasCrossingAt(ColonyTile tile, int z) => standing.Contains(tile);
        public ColonyTile? BackStep(ColonyPlacement placement) => backs.TryGetValue(placement.TemplateName, out var step) ? step : null;
    }
}
