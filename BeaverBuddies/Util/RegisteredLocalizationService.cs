using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.Localization;

namespace BeaverBuddies.Util
{
    public class RegisteredLocalizationService : RegisteredSingleton
    {
        public ILoc ILoc { get; private set; }

        public RegisteredLocalizationService(ILoc iloc)
        {
            ILoc = iloc;
        }

        public static string T(string key)
        {
            // Between SingletonManager.Reset (a scene change, a join's load) and the next scene's container, nothing is
            // registered: show the key rather than throw from wherever the text was wanted.
            RegisteredLocalizationService service = SingletonManager.GetSingleton<RegisteredLocalizationService>();
            if (service == null)
            {
                Plugin.LogWarning($"No localisation is registered yet; showing the key {key}");
                return key;
            }
            return service.ILoc.T(key);
        }

        /// <summary>A text with {0}-style placeholders, filled in a fixed culture (as the connection panel does).</summary>
        public static string T(string key, params object[] args)
        {
            string text = T(key);
            return args == null || args.Length == 0 ? text : string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args);
        }
    }
}
