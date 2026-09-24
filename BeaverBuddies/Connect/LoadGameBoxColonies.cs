using BeaverBuddies.Factions;
using BeaverBuddies.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Timberborn.GameSaveRepositorySystem;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    /// <summary>
    /// The gold line under the Load game box's picture: what the selected save is, for every save, in the main menu and in a
    /// game (1.4.0-rc7; rc4 to rc6 showed it only in the main menu's Host co-op game box). *Separate colonies: 2 players*
    /// (its colony slot table's rows), *… only yours so far*, *Folktails and Iron Teeth* for a mixed save, or *One shared
    /// colony*. In the save list's own small gold text (<c>game-text-small text--yellow</c>).
    /// <para>
    /// Each save is read once while the box lives, off the game's thread (the reader streams the save's world, tens of ms).
    /// The line keeps two lines' height, so the list never moves as each save's line is read, and it keeps its text when
    /// the box is shown again (a Co-op Game room closed over it, 1.4.0-rc5 review, A9).
    /// </para>
    /// </summary>
    internal sealed class LoadGameBoxColonies
    {
        public const string StatusName = "BeaverBuddiesSaveColonies";
        // How often a read under way is looked at (the box's own scheduler, so only while the box is shown).
        private const long PollMs = 100;

        private readonly Label status;
        private readonly Dictionary<string, SaveColonyInfo> read = new Dictionary<string, SaveColonyInfo>();
        private Task<SaveColonyInfo> reading;
        private string readingKey;
        private string shownKey;

        private LoadGameBoxColonies()
        {
            status = new Label { name = StatusName, userData = this };
            status.AddToClassList("game-text-small");
            status.AddToClassList("text--yellow");
            status.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter;
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.maxWidth = 300;
            status.style.marginTop = 6;
            // Two lines kept for it, so the save list does not move as each save's line is read.
            status.style.minHeight = 30;
            status.schedule.Execute(Poll).Every(PollMs);
        }

        /// <summary>The box's line (made the first time the box is shown), or null if the box has no SavesWrapper.</summary>
        public static LoadGameBoxColonies Of(VisualElement root)
        {
            if (root?.Q<Label>(StatusName)?.userData is LoadGameBoxColonies line) return line;
            VisualElement saves = root?.Q("SavesWrapper");
            if (saves == null) return null;
            var created = new LoadGameBoxColonies();
            saves.Add(created.status);
            return created;
        }

        /// <summary>The selected save changed (null: none): say what it is, reading it first if it has not been read.</summary>
        public void Show(SaveReference save, GameSaveRepository repository)
        {
            string key = Key(save);
            shownKey = key;
            if (save == null)
            {
                SetText(null);
                return;
            }
            if (read.TryGetValue(key, out SaveColonyInfo info))
            {
                SetText(info);
                return;
            }
            SetText(null);
            if (reading != null && readingKey == key) return;
            try
            {
                // Opened here (the repository's paths are the game's), read off the game's thread: a late save is large.
                System.IO.Stream stream = repository.OpenSaveWithoutLogging(save);
                readingKey = key;
                reading = Task.Run(() =>
                {
                    using (stream)
                    using (var bytes = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(bytes);
                        return SaveColonyReader.Read(bytes.ToArray());
                    }
                });
            }
            catch (Exception error)
            {
                Plugin.LogWarning("[Lobby] Could not read the save's colonies: " + error.Message);
            }
        }

        // A save's colonies, read: shown if that save is still the selected one.
        private void Poll()
        {
            if (reading == null || !reading.IsCompleted) return;
            SaveColonyInfo info = reading.Status == TaskStatus.RanToCompletion ? reading.Result : null;
            read[readingKey] = info;
            if (readingKey == shownKey) SetText(info);
            reading = null;
        }

        private void SetText(SaveColonyInfo info) => status.text = info == null ? "" : StatusText(info);

        /// <summary>What a save is, for the Load game box (display only).</summary>
        public static string StatusText(SaveColonyInfo info)
        {
            int players = Lobby.LobbyRules.PlayersRemembered(info?.SlotTable);
            string key = Lobby.LobbyRules.SaveStatusKey(info != null, info?.SeparateColonies ?? false, info?.Mixed ?? false, players);
            return key == null ? "" : RegisteredLocalizationService.T(key, players);
        }

        private static string Key(SaveReference save) => save == null ? "" : save.SettlementReference?.SettlementName + "/" + save.SaveName;
    }
}
