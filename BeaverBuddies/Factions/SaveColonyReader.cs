using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace BeaverBuddies.Factions
{
    /// <summary>What a save says of its colonies, read in the main menu without loading it (display only).</summary>
    public sealed class SaveColonyInfo
    {
        /// <summary>The game's own faction (FactionService.Id): the host's, and every colony's outside a mixed game.</summary>
        public string BaseFaction { get; set; } = "";
        /// <summary>A separate-colonies save (BeaverBuddies.ColonyMode.Enabled).</summary>
        public bool SeparateColonies { get; set; }
        /// <summary>The colony slot table's text ("slot|id|name" lines), or "".</summary>
        public string SlotTable { get; set; } = "";
        /// <summary>A mixed-factions save (BeaverBuddies.ColonyFactions.Mixed).</summary>
        public bool Mixed { get; set; }
        /// <summary>Each colony's faction, as the save records it (only colonies that have had a district center).</summary>
        public FactionTable Factions { get; set; } = new FactionTable();

        /// <summary>A colony's faction (by slot): its own in a mixed save, else the base faction; null in a mixed save for a colony that has none yet.</summary>
        public string FactionOfSlot(int slot) => Mixed ? Factions.Of(slot) : BaseFaction;
    }

    /// <summary>
    /// Reads a save's colonies from its bytes: the save is a zip whose world.json holds "Singletons" before "Entities"; the
    /// reader streams it and stops once it has the few singletons it wants, or at the end of "Singletons", never reading the
    /// entities. The mod's singletons are saved after the game's, so in practice it reads nearly all of "Singletons": about
    /// 35-55 ms for a 9 MB world under .NET 8 (review of 1.4.0-beta20, C-E6), likely a few times that in the game, once, on
    /// the click that opens a save's waiting room. Plain code (System and Newtonsoft only), checked headless.
    /// </summary>
    public static class SaveColonyReader
    {
        public const string WorldEntry = "world.json";
        private static readonly string[] Wanted = { "FactionService", "BeaverBuddies.ColonyMode", "BeaverBuddies.ColonySlots", "BeaverBuddies.ColonyFactions" };

        /// <summary>The save's colonies, or null when the bytes are not a save this can read.</summary>
        public static SaveColonyInfo Read(byte[] saveBytes)
        {
            if (saveBytes == null || saveBytes.Length == 0) return null;
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(saveBytes, writable: false), ZipArchiveMode.Read))
                {
                    ZipArchiveEntry entry = zip.GetEntry(WorldEntry);
                    if (entry == null) return null;
                    using (var reader = new JsonTextReader(new StreamReader(entry.Open())))
                        return ReadWorld(reader);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static SaveColonyInfo ReadWorld(JsonTextReader reader)
        {
            var singletons = new Dictionary<string, JObject>();
            if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return null;
            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                string name = (string)reader.Value;
                if (!reader.Read()) break;
                if (name != "Singletons" || reader.TokenType != JsonToken.StartObject)
                {
                    reader.Skip();
                    continue;
                }
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    string key = (string)reader.Value;
                    if (!reader.Read()) break;
                    if (Wanted.Contains(key) && reader.TokenType == JsonToken.StartObject) singletons[key] = JObject.Load(reader);
                    else reader.Skip();
                    if (singletons.Count == Wanted.Length) break;
                }
                break;
            }
            return Interpret(singletons);
        }

        private static SaveColonyInfo Interpret(Dictionary<string, JObject> singletons)
        {
            var info = new SaveColonyInfo();
            if (singletons.TryGetValue("FactionService", out JObject faction)) info.BaseFaction = (string)faction["Id"] ?? "";
            if (singletons.TryGetValue("BeaverBuddies.ColonyMode", out JObject mode))
                info.SeparateColonies = mode["Enabled"] is JValue { Type: JTokenType.Boolean } enabled && (bool)enabled;
            if (singletons.TryGetValue("BeaverBuddies.ColonySlots", out JObject slots)) info.SlotTable = (string)slots["Table"] ?? "";
            if (singletons.TryGetValue("BeaverBuddies.ColonyFactions", out JObject factions))
            {
                info.Mixed = factions["Mixed"] is JValue { Type: JTokenType.Boolean } mixed && (bool)mixed;
                if (factions["Colonies"] is JArray rows)
                    info.Factions = FactionTable.Decode(rows.Select(r => r.Type == JTokenType.String ? (string)r : null));
            }
            if (!info.SeparateColonies) info.Mixed = false;
            return info;
        }
    }
}
