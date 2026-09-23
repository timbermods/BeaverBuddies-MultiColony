/// <summary>
/// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
/// that need no game: reviewer A: the late-game systems against the game (automation, the HTTP API, water, power, blasts and terrain, fireworks, dev tools, crash paths). One check per finding, named after it.
/// </summary>
static class RcLateGameChecks
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

    // AutomationEvent's list of shared setters, as "Type.Method" (a property setter as "Type.set_Name").
    static List<string> AutomationList()
    {
        string body = Body(Source("BeaverBuddies", "Events", "AutomationEvents.cs"), "public static void ApplyAutomationPatches(");
        return System.Text.RegularExpressions.Regex.Matches(body, @"\(typeof\((\w+)\),\s*(""set_"" \+ )?nameof\(\w+\.(\w+)\)\)")
            .Select(m => m.Groups[1].Value + "." + (m.Groups[2].Success ? "set_" : "") + m.Groups[3].Value).ToList();
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("A2: AutomationEvent's list names each shared setter once, the pumps' flow rate, the valve's limit toggle and the dev generator among them", () =>
        {
            var list = AutomationList();
            Check(list.Count > 70, $"only {list.Count} entries were read; the scan is broken");
            var twice = list.GroupBy(e => e).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Check(twice.Count == 0, "listed twice: " + string.Join(", ", twice));
            foreach (string needed in new[] { "WaterMover.SetFlowRate", "ThrottlingValve.SetOutflowLimitEnabledAndSynchronize",
                "AdjustableStrengthPowerGenerator.set_GeneratorStrength", "AdjustableStrengthPowerGenerator.FlipRotation" })
                Check(list.Contains(needed), needed + " is not shared");
        });

        yield return ("R8: neither the water seep fix nor the shared setters' patching can throw out of the mod's start", () =>
        {
            string timing = Body(Source("BeaverBuddies", "Fixes", "WaterSourceTimingFix.cs"), "internal static IEnumerable<CodeInstruction> Transpiler(");
            Check(!timing.Contains("throw"), "the seep timing transpiler throws when the game's method changes");
            Check(timing.Contains("Unavailable ="), "the seep timing transpiler does not say when it could not be applied");
            string apply = Body(Source("BeaverBuddies", "Events", "AutomationEvents.cs"), "public static void ApplyAutomationPatches(");
            Check(apply.Contains("catch (Exception") && apply.Contains("MissingRecorders.Add"), "a missing setter throws out of the mod's start");
            string plugin = Source("BeaverBuddies", "Plugin.cs");
            Check(plugin.Contains("Bind<CoopFixGuard>()"), "the guard that stops a co-op game missing a fix is not bound");
        });

        yield return ("A1: the co-op Detonator arms on its input and never takes the arming back within a tick", () =>
        {
            string fixes = Source("BeaverBuddies", "Fixes", "TickTimingFixes.cs");
            string prefix = Body(fixes.Substring(fixes.IndexOf("class DetonatorPulseCoopPatcher", StringComparison.Ordinal)), "static bool Prefix(");
            Check(prefix.Contains("if (EventIO.IsNull) return true;"), "single player is not left alone");
            Check(prefix.Contains(".Arm()") && !prefix.Contains("Disarm"), "the co-op Detonator still disarms");
        });

        yield return ("A4, W1: the colony rules for Population Counters and synchronised water buildings act in separate-colonies games only", () =>
        {
            string counter = Source("BeaverBuddies", "Colonies", "ColonyPopulationCounter.cs");
            Check(Body(counter, "static bool Prefix(").Contains("if (!ColonyModeService.IsSeparateColonies || !__instance.GlobalMode) return true;"),
                "the colony counter does not leave shared games and district counters to the game");
            string sync = Source("BeaverBuddies", "Colonies", "ColonyWaterSync.cs");
            Check(Body(sync, "internal static int? Enter(").Contains("ColonyModeService.IsSeparateColonies ?"), "water synchronisation is filtered in shared games too");
            Check(sync.Contains("Owner == null ||"), "a building no colony owns is not left to the game");
        });

        yield return ("H1: every replayed action in the automation and panel events skips a building gone before it is played", () =>
        {
            var misses = new List<string>();
            foreach (string file in new[] { "AutomationEvents.cs", "EntityUIEvents.cs" })
            {
                string[] lines = Source("BeaverBuddies", "Events", file).Split('\n');
                for (int n = 0; n < lines.Length; n++)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(lines[n], @"(\w+)\s*=\s*(.*\?\s*null\s*:\s*)?GetComponent<[^>]+>\(context,");
                    if (!m.Success) continue;
                    string name = m.Groups[1].Value;
                    // A lookup that may be null by design (an input wire cleared) goes to a setter that accepts null.
                    if (m.Groups[2].Success) continue;
                    string after = string.Join("\n", lines.Skip(n + 1).Take(4));
                    bool guarded = System.Text.RegularExpressions.Regex.IsMatch(after,
                        $@"if \(.*(!{name}\b|\b{name} == null|\b{name} != null)");
                    if (!guarded) misses.Add($"{file}:{n + 1} {name}");
                }
            }
            Check(misses.Count == 0, "looked up by id and used without a check: " + string.Join(", ", misses));
        });
    }
}
