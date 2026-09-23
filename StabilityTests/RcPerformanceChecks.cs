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
