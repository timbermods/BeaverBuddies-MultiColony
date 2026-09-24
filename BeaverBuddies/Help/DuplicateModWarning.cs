using System;
using System.Linq;
using Timberborn.CoreUI;
using Timberborn.Modding;
using Timberborn.SingletonSystem;

namespace BeaverBuddies.Help
{
    /// <summary>
    /// Every other BeaverBuddies (the original, the Stability Fork, this mod's own earlier builds) patches the same game code
    /// for its own multiplayer, so it cannot run alongside this one. When one is enabled too, the main menu says so,
    /// by name, every time, until it is disabled.
    /// </summary>
    public class DuplicateModWarning : IPostLoadableSingleton
    {
        private readonly ModRepository _modRepository;
        private readonly DialogBoxShower _dialogBoxShower;

        public DuplicateModWarning(ModRepository modRepository, DialogBoxShower dialogBoxShower)
        {
            _modRepository = modRepository;
            _dialogBoxShower = dialogBoxShower;
        }

        /// <summary>
        /// The original, the Stability Fork and this mod's earlier builds: all use this id, or carry these names. An add-on
        /// or translation named after BeaverBuddies is not one of them.
        /// </summary>
        private static bool IsBeaverBuddies(string id, string name)
        {
            if (string.Equals(id, Plugin.OtherBeaverBuddiesID, StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, Plugin.EarlierBuildID, StringComparison.OrdinalIgnoreCase)) return true;
            name = name ?? "";
            return name.StartsWith("BeaverBuddies", StringComparison.OrdinalIgnoreCase)
                && (name.IndexOf("Co-Op", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Stability", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("MultiColony", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void PostLoad()
        {
            try
            {
                var others = _modRepository.EnabledMods
                    .Where(mod => mod?.Manifest != null && mod.Manifest.Id != Plugin.ID && IsBeaverBuddies(mod.Manifest.Id, mod.Manifest.Name))
                    .Select(mod => mod.Manifest.Name)
                    .ToList();
                if (others.Count == 0) return;
                Plugin.LogError("Other BeaverBuddies mods are enabled: " + string.Join(", ", others));
                string message = string.Format(_dialogBoxShower._loc.T("BeaverBuddies.DuplicateMod.Message"), string.Join(", ", others));
                _dialogBoxShower.Create().SetMessage(message).Show();
            }
            catch (Exception error)
            {
                Plugin.LogWarning("Could not check for other BeaverBuddies mods: " + error.Message);
            }
        }
    }
}
