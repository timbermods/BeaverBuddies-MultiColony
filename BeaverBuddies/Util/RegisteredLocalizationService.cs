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
            return SingletonManager.GetSingleton<RegisteredLocalizationService>().ILoc.T(key);
        }

        /// <summary>A text with {0}-style placeholders, filled in a fixed culture (as the connection panel does).</summary>
        public static string T(string key, params object[] args)
        {
            string text = T(key);
            return args == null || args.Length == 0 ? text : string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args);
        }
    }
}
