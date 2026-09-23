#nullable enable
using System.Reflection;
using System.Reflection.Emit;

// 1.4.0-rc6, checks against the compiled mod and the game: an invite accepted in a game is saved and joined from the main
// menu with the game's own exit save, and a direct join is not waited for on the game thread.
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

        test("rc6: an invite accepted in a game alone saves it with the game's own exit save, and is joined from the main menu", () =>
        {
            // The game: SaveAndOpenMainMenu asks for the exit save (skipAutoSave false), and the autosaver makes it.
            Type loader = Game("Timberborn.MainMenuSceneLoading", "Timberborn.MainMenuSceneLoading.MainMenuSceneLoader");
            var save = IlScan.Instructions(Only(loader, "SaveAndOpenMainMenu"));
            int post = save.FindIndex(i => i.Op == OpCodes.Newobj && i.Member?.DeclaringType?.Name == "PreMainMenuStartedEvent");
            if (post < 1 || save[post - 1].Op != OpCodes.Ldc_I4_0) throw new Exception("the game's SaveAndOpenMainMenu no longer asks for an exit save");
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
            if (!IlScan.Of(steam).Values.Any(members => members.Any(m => m.Name == "SaveAndOpenMainMenu" && m.DeclaringType == loader)))
                throw new Exception("the invite does not save the game on the way to the main menu");
            if (!IlScan.Instructions(Only(steam, "UpdateSingleton")).Any(i => i.Calls && i.Member?.Name == "JoinPendingInvite"))
                throw new Exception("the main menu never joins an invite accepted in a game");
        });
    }
}
