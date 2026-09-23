/// <summary>
/// The 1.4.0-rc1 review (design/REVIEW-PLAN-1.4.0-beta24.md, findings in design/REVIEW-FINDINGS-1.4.0-beta24.md), checks
/// that need no game: reviewer D: cost at late-game size, long sessions, compatibility, and the reporting Kyler's sessions read. One check per finding, named after it.
/// </summary>
static class RcPerformanceChecks
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

    // The movement animation's frame update (AnimationFixes), as the game calls it, at an interpolated time.
    static void Animate(Timberborn.CharacterMovementSystem.MovementAnimator animator, float time)
    {
        BeaverBuddies.SingletonManager.Progress.Time = time;
        typeof(BeaverBuddies.Fixes.AnimatedPathFollowerUpdatePathcer)
            .GetMethod("Prefix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, new object[] { animator, .02f });
    }

    public static IEnumerable<(string Name, Action Run)> Tests()
    {
        yield return ("D-S5: lockstep when the simulation is the limit: a late game keeps pace at speed 7, and no case spirals", () =>
        {
            // Estimated late-game tick costs (Script P measures them): host 40 ms, a guest half as fast again, 60 ms.
            var cases = new (string Name, LockstepModel.Result Result)[]
            {
                ("speed 7, 200+ characters (the game's limit, the default)", LockstepModel.Run(7, .4f, 40, 60)),
                ("speed 7, limit removed", LockstepModel.Run(7, 1, 40, 60)),
                ("speed 7, limit removed, guest twice as slow", LockstepModel.Run(7, 1, 30, 60)),
                ("speed 7, limit removed, 10 creations or deletions a tick", LockstepModel.Run(7, 1, 40, 60, interruptsPerTick: 10)),
                ("boost 15 (speed 22), limit removed", LockstepModel.Run(22, 1, 40, 60)),
                ("boost 15 (speed 22), the default limit", LockstepModel.Run(22, .4f, 40, 60)),
            };
            foreach (var (name, result) in cases) Console.WriteLine($"      {name}: {result}");
            var normal = cases[0].Result;
            // Budget B4: the guest within BufferTicksFor(speed) + 2 ticks for 95% of the time, and within 10% of the rate.
            Check(normal.LagP95 <= BeaverBuddies.CatchUpSpeed.BufferTicksFor(7) + 2, $"the default late game's guest is {normal.LagP95} ticks behind at p95");
            Check(normal.GuestTicksPerSecond >= .9 * normal.TargetTicksPerSecond, $"the default late game's guest runs at {normal.GuestTicksPerSecond:0.0} ticks/s");
            // No spiral: however far the computers fall short, the guest's lag stays bounded by the host's hold.
            foreach (var (name, result) in cases)
                Check(result.LagMaxLastQuarter <= BeaverBuddies.HostPacing.Scaled(BeaverBuddies.HostPacing.StopTicks, 22) + 10, $"{name}: the guest ended {result.LagMaxLastQuarter} ticks behind");
        });

        yield return ("D-S11: in a large game the desync dialog never offers to turn detailed logging on; with it on, posting stays", () =>
        {
            Check(BeaverBuddies.Connect.DesyncDialogPlan.ReportButtonKey(debug: false, canPostReports: true, largeGame: true) == null,
                "a large game is offered detailed logging, which would stop it for everyone");
            Check(!BeaverBuddies.Connect.DesyncDialogPlan.AsksToEnableLogging(debug: false, canPostReports: true, largeGame: true),
                "a large game's message asks every player to enable logging");
            Check(BeaverBuddies.Connect.DesyncDialogPlan.ReportButtonKey(debug: true, canPostReports: true, largeGame: true)
                == BeaverBuddies.Connect.DesyncDialogPlan.PostBugReportKey, "with logging already on, a large game can no longer post");
            Check(BeaverBuddies.Connect.DesyncDialogPlan.ReportButtonKey(debug: false, canPostReports: true)
                == BeaverBuddies.Connect.DesyncDialogPlan.EnableLoggingKey, "a small game is no longer offered logging");
            Check(BeaverBuddies.Connect.DesyncDialogPlan.LargeGameCharacters == 200, "the line moved from the game's own large-colony population");
            string dialog = Source("BeaverBuddies", "Events", "ConnectionEvents.cs");
            Check(dialog.Contains("DesyncDialogPlan.ReportButtonKey(Settings.Debug, reportingService.HasAccessToken, largeGame)")
                && dialog.Contains("DesyncDialogPlan.AsksToEnableLogging(Settings.Debug, reportingService.HasAccessToken, largeGame)"),
                "the desync dialog does not tell the plan how large the game is");
        });

        yield return ("D-S1: a sampled profiler spot counts every call, times one in 16 and reports it scaled up, for less than a timed spot costs", () =>
        {
            var sampled = BeaverBuddies.Colonies.ColonyProfiler.DeclareSampled("D-S1 sampled spot");
            var timed = BeaverBuddies.Colonies.ColonyProfiler.Declare("D-S1 timed spot");
            BeaverBuddies.Colonies.ColonyProfiler.Reset();
            for (int i = 0; i < 1600; i++)
                BeaverBuddies.Colonies.ColonyProfiler.StopSampled(sampled, BeaverBuddies.Colonies.ColonyProfiler.StartSampled(sampled));
            var row = BeaverBuddies.Colonies.ColonyProfiler.Snapshot().Single(r => r.name.StartsWith("D-S1 sampled spot"));
            Check(row.calls == 1600, $"{row.calls} calls counted, not 1600");
            Check(row.name.EndsWith("(~)"), "a sampled spot's row does not say it is estimated");
            // The working-hours check: every beaver and workplace, every tick. The spot's own cost, per call.
            const int n = 4_000_000;
            for (int i = 0; i < 100_000; i++)
            {
                BeaverBuddies.Colonies.ColonyProfiler.Stop(timed, BeaverBuddies.Colonies.ColonyProfiler.Start());
                BeaverBuddies.Colonies.ColonyProfiler.StopSampled(sampled, BeaverBuddies.Colonies.ColonyProfiler.StartSampled(sampled));
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) BeaverBuddies.Colonies.ColonyProfiler.Stop(timed, BeaverBuddies.Colonies.ColonyProfiler.Start());
            double every = clock.Elapsed.TotalMilliseconds; clock.Restart();
            for (int i = 0; i < n; i++) BeaverBuddies.Colonies.ColonyProfiler.StopSampled(sampled, BeaverBuddies.Colonies.ColonyProfiler.StartSampled(sampled));
            double oneIn16 = clock.Elapsed.TotalMilliseconds;
            Console.WriteLine($"      Profiler: {n:N0} calls cost {every:F0} ms timed every call, {oneIn16:F0} ms timed one in 16 ({every * 1e6 / n:F1} and {oneIn16 * 1e6 / n:F1} ns a call)");
            Check(oneIn16 < every, $"sampling ({oneIn16:F0} ms) should cost less than timing every call ({every:F0} ms)");
            BeaverBuddies.Colonies.ColonyProfiler.Reset();
        });

        yield return ("D-S9: a walker's animation goes on from its cached corner each frame, and starts again only when its clock goes back", () =>
        {
            var animator = new Timberborn.CharacterMovementSystem.MovementAnimator();
            var follower = animator._animatedPathFollower;
            Animate(animator, .5f);
            Check(follower._nextCornerIndex == 1 && follower.Scanned == 2, $"first frame: corner {follower._nextCornerIndex}, {follower.Scanned} looked at");
            // Later frames of the same tick: the search goes on from the cached corner (until 1.4.0-rc1 it went back to the
            // first corner every frame: 3 corners looked at here, and 3 again below).
            follower.Scanned = 0;
            Animate(animator, 1.5f);
            Check(follower.Scanned == 2, $"at 1.5 the search looked at {follower.Scanned} corners, not the 2 from the cached one");
            follower.Scanned = 0;
            Animate(animator, 1.8f);
            Check(follower.Scanned == 1 && follower._nextCornerIndex == 2, $"at 1.8: {follower.Scanned} corners looked at, corner {follower._nextCornerIndex}");
            Check(Math.Abs(animator.ModelPosition.x - (1 + .8f * 100)) < 1e-3, "the segment drawn at 1.8 is not the one a search from the first corner finds");
            // The clock goes back at a tick boundary: the search starts again and draws the right segment.
            Animate(animator, .5f);
            Check(follower._nextCornerIndex == 1 && Math.Abs(animator.ModelPosition.x - .5f) < 1e-6, "going back, the segment was not searched again");
            // Past the last corner, then back.
            Animate(animator, 2.5f);
            Check(follower._nextCornerIndex == follower._pathCorners.Count + 1, "past the end");
            Animate(animator, 1.2f);
            Check(follower._nextCornerIndex == 2 && Math.Abs(animator.ModelPosition.x - (1 + .2f * 100)) < 1e-3, "back from past the end");
        });
    }
}
