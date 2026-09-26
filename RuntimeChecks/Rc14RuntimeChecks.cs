#nullable enable
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;

// 1.4.0-rc14, checks against the compiled mod and the game's UI files: the Trading Post's goods selector keeps a long
// name on one line, cut with an ellipsis, as the game's own dropdowns show an item.
internal static class Rc14RuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, string managedPath, Action<string, Action> test)
    {
        test("rc14: the goods selector's name is one line with an ellipsis, as the game's dropdown item (CommonStyle)", () =>
        {
            // The game's own rule the selector copies.
            using ZipArchive ui = ZipFile.OpenRead(Path.GetFullPath(Path.Combine(managedPath, "..", "StreamingAssets", "Modding", "UI.zip")));
            using var reader = new StreamReader((ui.GetEntry("Views/Common/CommonStyle.uss") ?? throw new Exception("UI.zip has no CommonStyle")).Open());
            Match rule = Regex.Match(reader.ReadToEnd(), @"\.dropdown-item__text\s*\{([^}]*)\}");
            if (!rule.Success) throw new Exception("the game's dropdown item style is gone");
            foreach (string part in new[] { "white-space: nowrap", "overflow: hidden", "text-overflow: ellipsis" })
                if (!rule.Groups[1].Value.Contains(part)) throw new Exception("the game's dropdown item no longer has " + part);
            // The mod's selector.
            Type fragment = mod.GetType("BeaverBuddies.Colonies.TradingPostFragment", true)!;
            MethodInfo build = fragment.GetMethod("BuildOfferSide", All) ?? throw new Exception("BuildOfferSide is gone");
            var calls = IlScan.Instructions(build).Where(i => i.Calls).Select(i => i.Member?.Name).ToList();
            foreach (string setter in new[] { "set_whiteSpace", "set_overflow", "set_textOverflow" })
                if (!calls.Contains(setter)) throw new Exception("the goods selector's name no longer sets " + setter.Substring(4) + ": a long name spills out");
        });
    }
}
