#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// 1.4.0-rc6, checks against the compiled mod and the game: an invite accepted in a game never takes over a co-op game (rc7:
// alone, it joins the room in the game; rc6 saved the game and joined from the main menu), and a direct join is not waited
// for on the game thread.
internal static class Rc6RuntimeChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static void Run(Assembly mod, Action<string, Action> test)
    {
        Type Game(string assembly, string type) => Assembly.Load(assembly).GetType(type, true)!;
        MethodInfo Only(Type type, string name) =>
            type.GetMethods(All).SingleOrDefault(m => m.Name == name) ?? throw new Exception($"{type.FullName}.{name} is gone");

        test("rc6: a direct join's connect is started on the game thread and waited for on the network thread", () =>
        {
            Type client = Assembly.Load("TimberNet").GetType("TimberNet.TimberClient", true)!;
            var start = IlScan.Instructions(Only(client, "Start"));
            if (!start.Any(i => i.Calls && i.Member?.Name == "ConnectAsync")) throw new Exception("Start no longer starts the connection");
            if (start.Any(i => i.Calls && i.Member?.Name == "Wait" && i.Member.DeclaringType == typeof(Task)))
                throw new Exception("Start waits for the connection on the game thread again");
            // The wait is in the network thread's body (a lambda of Start).
            if (!IlScan.Of(client).Any(m => m.Key.Name.Contains("<Start>") && m.Value.Any(x => x.Name == "Wait" && x.DeclaringType == typeof(Task))))
                throw new Exception("nothing waits for the connection before the handshake");
        });

        test("rc6: an invite accepted in a game asks the invite's rule before it connects (rc7: alone, it joins in the game)", () =>
        {
            // The game's exit save (rc7 makes it at Start, Rc7RuntimeChecks): the one the game makes on its way to the main menu.
            Type autosaver = Game("Timberborn.Autosaving", "Timberborn.Autosaving.Autosaver");
            if (!IlScan.Instructions(Only(autosaver, "OnPreMainMenuStarted")).Any(i => i.Calls && i.Member?.Name == "CreateExitSave"))
                throw new Exception("the game no longer saves on the way to the main menu");
            // The mod, in the Steam build (the other build keeps an empty service).
            Type? steam = mod.GetType("BeaverBuddies.Steam.SteamOverlayConnectionService", false);
            if (steam?.GetMethod("JoinHostLobby", All) == null)
            {
                if (steam?.GetMethod("OnLobbyEntered", All) != null) throw new Exception("the Steam build lost JoinHostLobby");
                return;
            }
            var join = IlScan.Instructions(Only(steam, "JoinHostLobby"));
            int decide = join.FindIndex(i => i.Calls && i.Member?.Name == "Decide" && i.Member.DeclaringType?.Name == "InviteRules");
            int connect = join.FindIndex(i => i.Calls && i.Member?.Name == "TryToConnect");
            if (decide < 0 || connect < decide) throw new Exception("a lobby entered in a game connects before the invite's rule is asked");
            // rc7: no Save-and-join box, and no join left for the main menu.
            if (IlScan.Of(steam).Values.Any(members => members.Any(m => m.Name == "SaveAndOpenMainMenu")))
                throw new Exception("an invite in a game saves it and goes to the main menu again");
            foreach (string gone in new[] { "JoinPendingInvite", "OfferJoinFromGame" })
                if (steam.GetMethod(gone, All) != null) throw new Exception(gone + " is back: an invite in a game goes through the main menu");
        });
    }
}
