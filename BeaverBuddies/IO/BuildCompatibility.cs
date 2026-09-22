using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TimberNet;
using UnityEngine;

namespace BeaverBuddies.IO
{
    internal static class BuildCompatibility
    {
        // The mod's own blueprints (the Trading Post and the template collections that add it). Loaded by the game as
        // data, not code, so the DLLs' identity says nothing about them: a guest with the right DLL but a missing,
        // stale or edited copy would load a save's Trading Posts as nothing, or as a different building.
        internal static readonly string[] BlueprintFolders = { "Buildings", "TemplateCollections" };

        // Loaded module IDs identify the binaries actually executing, even if
        // the user replaced the files on disk without restarting the game.
        internal static string CreateIdentity() =>
            $"game={Application.version};mod={Plugin.Version};" +
            $"modBuild={typeof(Plugin).Module.ModuleVersionId:D};netBuild={typeof(TimberNetBase).Module.ModuleVersionId:D};" +
            $"blueprints={BlueprintDigest(Plugin.ModPath)}";

        /// <summary>
        /// One hash over every file under the mod's blueprint folders: their paths relative to the mod folder (with
        /// forward slashes, in ordinal order) and their bytes. "none" for a mod folder that is not known or holds no
        /// blueprints, so two such installs still match each other; a read error gives "error" (a join is then refused
        /// rather than quietly allowed).
        /// </summary>
        internal static string BlueprintDigest(string modPath)
        {
            try
            {
                if (string.IsNullOrEmpty(modPath) || !Directory.Exists(modPath)) return "none";
                var files = BlueprintFolders
                    .Select(folder => Path.Combine(modPath, folder))
                    .Where(Directory.Exists)
                    .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                    .Select(file => (path: RelativePath(modPath, file), file))
                    .OrderBy(entry => entry.path, StringComparer.Ordinal)
                    .ToList();
                if (files.Count == 0) return "none";
                using (var sha = SHA256.Create())
                {
                    foreach (var (path, file) in files)
                    {
                        byte[] name = Encoding.UTF8.GetBytes(path + "\n");
                        sha.TransformBlock(name, 0, name.Length, null, 0);
                        byte[] bytes = File.ReadAllBytes(file);
                        sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
                    }
                    sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    return BitConverter.ToString(sha.Hash).Replace("-", "").Substring(0, 16).ToLowerInvariant();
                }
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not read the mod's blueprint files for the join check: " + error.Message);
                return "error";
            }
        }

        private static string RelativePath(string root, string file)
        {
            string full = Path.GetFullPath(file);
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string relative = full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.GetFileName(full);
            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }
}
