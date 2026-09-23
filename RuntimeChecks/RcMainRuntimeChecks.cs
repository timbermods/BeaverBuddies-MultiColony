#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
// against the compiled mod and the installed game's assemblies: the main session: the release-readiness leads (R), beta24's Join box (B24) and the findings made while planning (P).
internal static class RcMainRuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, string managedPath, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");

        test("P-1: the game's Reset resets one transmitter and Reset all the partition; the mod's replay does the same", () =>
        {
            Type fragment = Game("Timberborn.AutomationUI", "Timberborn.AutomationUI.SequentialTransmitterResetFragment");
            var reset = IlScan.Instructions(Only(fragment, "OnReset"));
            var resetAll = IlScan.Instructions(Only(fragment, "OnResetAll"));
            if (!reset.Any(i => i.Calls && i.Member?.Name == "Reset" && i.Member.DeclaringType?.Name == "ISequentialTransmitter"))
                throw new Exception("the game's OnReset no longer calls ISequentialTransmitter.Reset");
            if (!resetAll.Any(i => i.Calls && i.Member?.Name == "ResetPartition" && i.Member.DeclaringType?.Name == "AutomationResetter"))
                throw new Exception("the game's OnResetAll no longer calls AutomationResetter.ResetPartition");
            // The mod's replay: the resetAll branch (compiled first) resets the partition, the other one transmitter.
            Type ev = mod.GetType("BeaverBuddies.Events.ResetTransmitterEvent", true)!;
            var replay = IlScan.Instructions(ev.GetMethod("Replay", All)!);
            int partition = replay.FindIndex(i => i.Calls && i.Member?.Name == "ResetPartition");
            int single = replay.FindIndex(i => i.Calls && i.Member?.Name == "Reset" && i.Member.DeclaringType?.Name == "ISequentialTransmitter");
            if (partition < 0 || single < 0) throw new Exception("ResetTransmitterEvent.Replay no longer calls both resets");
            if (partition > single) throw new Exception("ResetTransmitterEvent.Replay resets one transmitter for Reset all (resetAll), the partition for Reset");
            // The recorders: OnReset records resetAll = false, OnResetAll resetAll = true.
            Type Patch(string n) => mod.GetType("BeaverBuddies.Events." + n, true)!;
            // false and true compile to ldc.i4.0 and ldc.i4.1, which carry no operand (IlScan leaves Number empty).
            int? Constant(IlScan.Instruction i) => i.Number ?? (i.Op == OpCodes.Ldc_I4_0 ? 0 : i.Op == OpCodes.Ldc_I4_1 ? 1 : null);
            bool Writes(Type patch, int value) => patch.GetNestedTypes(All).Append(patch).SelectMany(t => t.GetMethods(All))
                .Select(m => IlScan.Instructions(m)).Any(ins => Enumerable.Range(1, Math.Max(0, ins.Count - 1)).Any(k =>
                    ins[k].Member?.Name == "resetAll" && Constant(ins[k - 1]) == value));
            if (!Writes(Patch("SequentialTransmitterResetFragmentOnResetPatch"), 0)) throw new Exception("OnReset no longer records resetAll = false");
            if (!Writes(Patch("SequentialTransmitterResetFragmentOnResetAllPatch"), 1)) throw new Exception("OnResetAll no longer records resetAll = true");
        });

        test("R8: patch classes a game update breaks are named, the rest still apply, and a co-op game missing any is stopped", () =>
        {
            Type plugin = mod.GetType("BeaverBuddies.Plugin", true)!;
            MethodInfo patchAll = plugin.GetMethod("PatchAllIsolatingFactions", All) ?? throw new Exception("PatchAllIsolatingFactions is gone");
            // Two catches: the mixed-factions group's (beta21) and, new, one around each other patch class.
            if (patchAll.GetMethodBody()!.ExceptionHandlingClauses.Count(c => c.Flags == ExceptionHandlingClauseOptions.Clause) < 2)
                throw new Exception("a patch class that fails still throws out of the mod's start, skipping every later patch");
            if (!IlScan.Instructions(patchAll).Any(i => i.Member?.Name == "FailedPatches"))
                throw new Exception("a patch class that fails is not recorded");
            Type guard = mod.GetType("BeaverBuddies.Fixes.CoopFixGuard", true)!;
            if (!IlScan.Instructions(guard.GetMethod("UpdateSingleton", All)!).Any(i => i.Member?.Name == "FailedPatches"))
                throw new Exception("the co-op guard does not look at the patches left out");
            MethodInfo missing = guard.GetMethod("Missing", All) ?? throw new Exception("CoopFixGuard.Missing is gone");
            string none = (string?)missing.Invoke(null, new object?[] { null, false, new List<string>(), new List<string>() })!;
            string one = (string?)missing.Invoke(null, new object?[] { null, false, new List<string>(), new List<string> { "BeaverBuddies.Fixes.Example" } })!;
            if (none != null) throw new Exception("a game with everything applied is stopped: " + none);
            if (one == null || !one.Contains("BeaverBuddies.Fixes.Example")) throw new Exception("a game with a patch left out is not stopped, or the message doesn't name it");
        });

        // ---- 1.4.0-rc2: hand-overs ----

        test("rc2: an absent player's colony is handed over only within its faction in a mixed game, and not at all by default", () =>
        {
            // The compiled rule: its own faction or none (the flag is new in rc2).
            Type rules = mod.GetType("BeaverBuddies.Factions.FactionRules", true)!;
            MethodInfo prefer = rules.GetMethod("PreferSameFaction", All) ?? throw new Exception("FactionRules.PreferSameFaction is gone");
            if (prefer.GetParameters().Length != 4) throw new Exception("PreferSameFaction has no same-faction-only choice");
            Func<int, string> factionOf = slot => slot == 1 ? "IronTeeth" : "Folktails";
            var otherOnly = new List<(int, long)> { (1, 10) };
            if (prefer.Invoke(null, new object?[] { otherOnly, factionOf, "Folktails", true }) != null)
                throw new Exception("an absent Folktails colony would go to an Iron Teeth one");
            if ((int?)prefer.Invoke(null, new object?[] { otherOnly, factionOf, "Folktails", false }) != 1)
                throw new Exception("a colony with nobody left no longer goes to the nearest of any faction");
            // The host's check and the day's presence use the absence receiver; AbsenceReceiver asks for the same faction only.
            Type lifecycle = mod.GetType("BeaverBuddies.Colonies.ColonyLifecycle", true)!;
            bool CallsReceiver(string method) => IlScan.Instructions(Only(lifecycle, method)).Any(i => i.Calls && i.Member?.Name == "AbsenceReceiver");
            if (!CallsReceiver("HostDaily")) throw new Exception("the host's daily check does not hand over to the absence receiver");
            if (!CallsReceiver("Seen")) throw new Exception("the day's presence warns without asking for a receiver");
            var receiver = IlScan.Instructions(Only(lifecycle, "AbsenceReceiver"));
            int nearest = receiver.FindIndex(i => i.Calls && i.Member?.Name == "NearestLiving");
            if (nearest < 1 || receiver[nearest - 1].Op != OpCodes.Ldc_I4_1) throw new Exception("AbsenceReceiver must ask NearestLiving for the same faction only");
            // The setting's default: the constant pushed just before its name, in Settings' constructor.
            Type settings = mod.GetType("BeaverBuddies.Settings", true)!;
            var init = settings.GetConstructors(All).SelectMany(c => IlScan.Instructions(c)).ToList();
            int name = init.FindIndex(i => i.Op == OpCodes.Ldstr && i.Text == "BeaverBuddies.Settings.AbandonedColonyDays");
            if (name < 1) throw new Exception("the absence setting is no longer made in Settings' constructor");
            if (init[name - 1].Op != OpCodes.Ldc_I4_0) throw new Exception($"the absence setting's default is not 0 ({init[name - 1].Op} {init[name - 1].Number})");
        });

        // ---- 1.4.0-rc3: separate or shared, chosen on the Game Mode page; a shared game split from the game menu ----

        test("rc3: the Game Mode page's colony checkboxes are the page's own rows, beside its Tutorial checkbox", () =>
        {
            using ZipArchive ui = ZipFile.OpenRead(Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip")));
            string Read(string entry)
            {
                using var reader = new StreamReader((ui.GetEntry(entry) ?? throw new Exception("UI.zip has no " + entry)).Open());
                return reader.ReadToEnd();
            }
            // The page's Tutorial row, which the checkboxes copy and join: in ModeDetails, before the custom settings.
            string page = Read("Views/MainMenu/NewGameModePanel.uxml");
            int details = page.IndexOf("name=\"ModeDetails\"", StringComparison.Ordinal);
            int tutorial = page.IndexOf("name=\"TutorialToggleWrapper\"", StringComparison.Ordinal);
            int custom = page.IndexOf("name=\"CustomModeSettings\"", StringComparison.Ordinal);
            if (details < 0 || tutorial < details || custom < tutorial) throw new Exception("the Tutorial row is no longer in ModeDetails, before the custom settings");
            string row = page.Substring(tutorial, custom - tutorial);
            var used = (string[])mod.GetType("BeaverBuddies.Lobby.NewGameColonyOptions", true)!.GetField("ClassesUsed", All)!.GetValue(null)!;
            var notOnTheRow = used.Where(c => !row.Contains(c)).ToList();
            if (notOnTheRow.Count > 0) throw new Exception("classes the page's Tutorial row doesn't use: " + string.Join(", ", notOnTheRow));
            // ...and every one is in a style sheet the main menu loads.
            string title = Read("Views/MainMenu/TitleScreen.uxml");
            var defined = new HashSet<string>();
            foreach (Match sheet in Regex.Matches(title, "Style src=\"/Assets/Resources/UI/(Views/[^\"]+\\.uss)\""))
                foreach (Match m in Regex.Matches(Read(sheet.Groups[1].Value), "\\.([A-Za-z_][A-Za-z0-9_-]*)")) defined.Add(m.Groups[1].Value);
            var missing = used.Where(c => !defined.Contains(c)).ToList();
            if (missing.Count > 0) throw new Exception("not in the main menu's style sheets: " + string.Join(", ", missing));
            // The game menu keeps a Settings button to put Found your own colony under.
            if (!Read("Views/Game/GameOptionsBox.uxml").Contains("name=\"SettingsButton\"")) throw new Exception("the game menu has no SettingsButton");
            // No Mod Setting decides a new game's colonies any more.
            Type settings = mod.GetType("BeaverBuddies.Settings", true)!;
            var left = new[] { "SeparateColonies", "FoundingInSharedGames", "SeparateScience", "MixedFactions" }
                .Where(n => settings.GetProperty(n, All) != null).ToList();
            if (left.Count > 0) throw new Exception("still a Mod Setting: " + string.Join(", ", left));
        });

        test("rc3: every new world reads the Game Mode page's choice, and only a guest can split a shared game", () =>
        {
            // A new world: both start paths ask NewGameColonyChoice (a waiting room's own copy, else the page's).
            Type start = mod.GetType("BeaverBuddies.MultiStart.StartingBuildingInitializerInitializePatcher", true)!;
            if (IlScan.Instructions(Only(start, "Prefix")).Count(i => i.Calls && i.Member?.Name == "ForNewWorld") != 2)
                throw new Exception("a start path of a new world does not read NewGameColonyChoice.ForNewWorld");
            // The host's verdict on a founding: ColonyRules.MayFound, with the actor's seat against the host's.
            Type founding = mod.GetType("BeaverBuddies.Colonies.ColonyFoundingService", true)!;
            if (!IlScan.Instructions(Only(founding, "Judge")).Any(i => i.Calls && i.Member?.Name == "MayFound"))
                throw new Exception("the founding verdict no longer asks ColonyRules.MayFound");
            Type rules = mod.GetType("BeaverBuddies.Colonies.ColonyRules", true)!;
            bool MayFound(bool separate, bool host) => (bool)rules.GetMethod("MayFound", All)!.Invoke(null, new object[] { separate, host })!;
            if (MayFound(false, true)) throw new Exception("the host could split a shared game");
            if (!MayFound(false, false) || !MayFound(true, false) || !MayFound(true, true)) throw new Exception("MayFound refuses a founding it should allow");
            bool Offered(bool guest, bool separate, bool seated, bool owns) =>
                (bool)rules.GetMethod("SplitOffered", All)!.Invoke(null, new object[] { guest, separate, seated, owns })!;
            if (!Offered(true, false, true, false)) throw new Exception("a seated guest without a colony in a shared game is not offered the split");
            if (Offered(false, false, true, false) || Offered(true, true, true, false) || Offered(true, false, true, true) || Offered(true, false, false, false))
                throw new Exception("the split is offered where it should not be (the host, a separate game, a player with a colony, before seating)");
            // The split keeps one pool of science: the Enable of a founding in a shared game passes separateScience false.
            var found = IlScan.Instructions(Only(founding, "Found"));
            int enable = found.FindIndex(i => i.Calls && i.Member?.Name == "Enable" && i.Member.DeclaringType?.Name == "ColonyModeService");
            if (enable < 2 || found[enable - 2].Op != OpCodes.Ldc_I4_0) throw new Exception("a split no longer keeps science shared (separateScience false)");
            // The game menu's button: through the started check and the confirmation, which comes before BeginSplit.
            Type split = mod.GetType("BeaverBuddies.Colonies.SharedColonySplit", true)!;
            var ask = IlScan.Instructions(Only(split, "Ask"));
            if (!ask.Any(i => i.Calls && i.Member?.Name == "SplitCanBeginNow") || !ask.Any(i => i.Calls && i.Member?.Name == "SetConfirmButton"))
                throw new Exception("the split no longer checks the game has started and asks for confirmation");
            if (ask.Any(i => i.Calls && i.Member?.Name == "BeginSplit")) throw new Exception("the split begins before it is confirmed");
        });
    }
}
