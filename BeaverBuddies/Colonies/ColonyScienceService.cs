using Timberborn.SingletonSystem;

namespace BeaverBuddies.Colonies
{
    /// <summary>Separate science and unlocks per colony (Phase 4). Until then every colony may build everything.</summary>
    public class ColonyScienceService : RegisteredSingleton, ILoadableSingleton
    {
        public static ColonyScienceService Instance => SingletonManager.GetSingleton<ColonyScienceService>();

        public void Load() { }

        public bool IsUnlockedFor(int slot, string templateName) => true;
    }
}
