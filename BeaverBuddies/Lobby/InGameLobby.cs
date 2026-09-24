using BeaverBuddies.Util;
using System;
using Timberborn.AssetSystem;
using Timberborn.Autosaving;
using UnityEngine.UIElements;

namespace BeaverBuddies.Lobby
{
    /// <summary>
    /// A game scene's side of the Co-op Game room (1.4.0-rc7): bound in every game, never in the main menu, so it also says
    /// which of the two the room is opened in (<see cref="InGame"/>).
    /// <para>
    /// The room's window and the Join co-op game box are built from the main menu's classes (LobbyPage.ClassesUsed,
    /// JoinCoopBox.ClassesUsed), from style sheets a game scene does not load: a game has CoreStyle and CommonStyle and the
    /// game's own sheets. <see cref="AttachStyles"/> adds the others to the box's own root, loaded by the game's asset
    /// loader from the paths the main menu's UXML names (RuntimeChecks reads UI.zip: every class the window uses is in
    /// CoreStyle, CommonStyle or one of these).
    /// </para>
    /// <para>
    /// At Start, the game the room was opened over is replaced by the hosted save: <see cref="ExitSaveForStart"/> makes its
    /// exit save first, as Exit to menu does (K3). The game's Autosaver is a game-scene service, so it is reached here, not
    /// from the panels or ClientConnectionService, which the main menu binds too.
    /// </para>
    /// </summary>
    public class InGameLobby : RegisteredSingleton
    {
        /// <summary>The main menu's sheets the room's window needs, as Resources paths (UXML: /Assets/Resources/UI/Views/….uss).</summary>
        public static readonly string[] SheetPaths =
        {
            "UI/Views/Options/OptionsStyle",
            "UI/Views/MainMenu/MainMenuStyle",
            "UI/Views/MainMenu/MainMenuMiscStyle",
            "UI/Views/Modding/ModdingStyle",
        };

        private readonly IAssetLoader _assetLoader;
        private readonly Autosaver _autosaver;
        private StyleSheet[] sheets;

        public static InGameLobby Current => SingletonManager.GetSingleton<InGameLobby>();

        /// <summary>This scene is a game (a room opens as a window over it), not the main menu (a page).</summary>
        public static bool InGame => Current != null;

        public InGameLobby(IAssetLoader assetLoader, Autosaver autosaver)
        {
            _assetLoader = assetLoader;
            _autosaver = autosaver;
        }

        /// <summary>
        /// Start is about to replace this game with a hosted save (the host's room's Start, or a guest's save arriving): its
        /// exit save, the game's own (Autosaver.CreateExitSave, as Exit to menu makes it), unless ExitSaveRules says not: a
        /// game loaded as a guest (its copy is the host's), or one just saved for this room. A save that fails is logged,
        /// and the start goes on.
        /// </summary>
        public void ExitSaveForStart(bool savedForRoom)
        {
            ReplayService replay = SingletonManager.GetSingleton<ReplayService>();
            bool loadedAsGuest = replay != null && !replay.LoadedAsHost;
            if (!ExitSaveRules.Make(inGame: true, loadedAsGuest, savedForRoom))
            {
                Plugin.Log($"[Lobby] No exit save of this game at Start ({(loadedAsGuest ? "it was loaded as a guest" : "it was just saved for the room")})");
                return;
            }
            try
            {
                _autosaver.CreateExitSave();
                Plugin.Log("[Lobby] This game's exit save is made; the hosted save replaces it");
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Lobby] Could not make this game's exit save; starting anyway: " + error);
            }
        }

        /// <summary>Adds the main menu's sheets the window's classes come from to <paramref name="root"/>.</summary>
        public void AttachStyles(VisualElement root)
        {
            if (root == null) return;
            if (sheets == null)
            {
                sheets = new StyleSheet[SheetPaths.Length];
                for (int i = 0; i < SheetPaths.Length; i++)
                {
                    try { sheets[i] = _assetLoader.Load<StyleSheet>(SheetPaths[i]); }
                    catch (Exception error)
                    {
                        // The window still opens, with the game's styles only; the log names the sheet.
                        Plugin.LogWarning($"[Lobby] Could not load the style sheet {SheetPaths[i]} for the window: {error.Message}");
                    }
                }
            }
            foreach (StyleSheet sheet in sheets)
            {
                if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            }
        }
    }
}
