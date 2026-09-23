using BeaverBuddies.Colonies;

/// <summary>
/// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
/// that need no game: reviewer E: the colony lifecycle at late-game size (founding, stamps, handover, stewards, absence, slots) and the simulation events outside automation (placement, demolition, planting and cutting, migration, distribution). One check per finding, named after it.
/// </summary>
static class RcColonyChecks
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

    /// <summary>
    /// One colony through <paramref name="days"/> days of hosted play, as ColonyLifecycle runs them: each day the host's
    /// check (on the count and announcement the last presence left, and today's presence), then the day's presence,
    /// played everywhere (the count, and today's announcement, with its warning). Returns the day it is handed over for
    /// its player's absence and the day it was announced, or null. <paramref name="loadedAt"/>: the count a save left.
    /// </summary>
    static (int? handedOver, int? warned) Absence(int limit, Func<int, bool> playerIn, Func<int, bool> stewardIn, int days, int loadedAt = 0)
    {
        int away = loadedAt;
        bool announced = false;
        int? warned = null;
        for (int day = 1; day <= days; day++)
        {
            // HostDaily: a colony whose player is in the game is not looked at.
            if (!playerIn(day) && ColonyAbsence.IsHandedOver(away, limit, stewardIn(day), announced)) return (day, warned);
            // Seen.
            away = playerIn(day) ? 0 : away + 1;
            bool now = ColonyAbsence.IsAnnounced(away, limit, playerIn(day) || stewardIn(day));
            if (now && !announced) warned = day;
            announced = now;
        }
        return (null, warned);
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("E-3: a hand-over for absence always comes the day after its warning, also when a steward kept the colony past the limit", () =>
        {
            Check(ColonyAbsence.IsAnnounced(7, 7, kept: false) && !ColonyAbsence.IsAnnounced(6, 7, kept: false), "announced at the limit");
            Check(!ColonyAbsence.IsAnnounced(30, 7, kept: true) && !ColonyAbsence.IsAnnounced(30, 0, kept: false), "not while kept, never without a limit");
            Check(!ColonyAbsence.IsHandedOver(30, 7, kept: false, announcedAtLastCheck: false), "never unannounced");
            Check(ColonyAbsence.IsHandedOver(30, 7, kept: false, announcedAtLastCheck: true), "announced, then handed over");
            Check(!ColonyAbsence.IsHandedOver(30, 7, kept: true, announcedAtLastCheck: true), "not once a steward is back");

            // Away from the first day, nobody looking after it: warned the day the count reaches 7, handed over the next (as before).
            var plain = Absence(7, day => false, day => false, days: 30);
            Check(plain.warned == 7 && plain.handedOver == 8, $"plain absence: warned {plain.warned}, handed over {plain.handedOver}");
            // Looked after for 20 days by a steward in the game, then nobody: it used to go at once, unwarned, on day 21.
            var kept = Absence(7, day => false, day => day <= 20, days: 30);
            Check(kept.warned == 21 && kept.handedOver == 22, $"after a steward: warned {kept.warned}, handed over {kept.handedOver}");
            // The steward is back the day after the warning: nothing happens.
            var back = Absence(7, day => false, day => day <= 20 || day == 22, days: 22);
            Check(back.warned == 21 && back.handedOver == null, $"steward back: handed over {back.handedOver}");
            // The player is back the day after the warning: nothing happens, and the count starts again.
            var player = Absence(7, day => day == 8, day => false, days: 14);
            Check(player.warned == 7 && player.handedOver == null, $"player back: handed over {player.handedOver}");
            // A save whose colony was already due: this session warns first (the players now in the game are told).
            var loaded = Absence(7, day => false, day => false, days: 5, loadedAt: 12);
            Check(loaded.warned == 1 && loaded.handedOver == 2, $"after a load: warned {loaded.warned}, handed over {loaded.handedOver}");
            Check(Absence(0, day => false, day => false, days: 60).handedOver == null, "a limit of 0 never hands over");

            // The host's check and the day's presence use exactly these rules, with the announcement kept between them.
            string lifecycle = Source("BeaverBuddies", "Colonies", "ColonyHandover.cs");
            string hostDaily = Body(lifecycle, "private void HostDaily(int day)");
            Check(hostDaily.Contains("ColonyAbsence.IsHandedOver(") && hostDaily.Contains("announced[slot]"),
                "the host's daily check must hand over only a colony the last presence announced");
            string seen = Body(lifecycle, "public void Seen(");
            Check(seen.Contains("ColonyAbsence.IsAnnounced(") && seen.Contains("announced[slot] = now") && seen.Contains("WarnBeforeHandover(newlyAnnounced)"),
                "the day's presence must announce, and warn of, each colony due that nobody keeps");
            Check(Body(lifecycle, "public void Transfer(int from, int to, HandoverReason reason)").Contains("announced[from] = false"),
                "a colony handed over is announced no more");
        });

        yield return ("E-4: a deletion sent as an action leaves no picked terrain behind in the tool", () =>
        {
            string patcher = Body(Source("BeaverBuddies", "Events", "ToolEvents.cs"), "class BuildingDeconstructionPatcher");
            string cleanup = Body(patcher, "if (!result)");
            Check(cleanup.Contains("_temporaryBlockObjects.Clear()") && cleanup.Contains("_temporaryTerrainCoords.Clear()"),
                "the recorded deletion must clear the tool's picked terrain as the game's DeleteBlockObjects does");
        });
    }
}
