using DankLibWaifuz.CollectionsWaifu;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KikRateTester2.Declarations
{
    internal enum BlacklistType
    {
        Login
    }

    internal static class Blacklists
    {
        private static readonly HashSet<string> LoginBlacklist = new HashSet<string>();

        public static Dictionary<BlacklistType, HashSet<string>> Dict { get; } = new Dictionary<BlacklistType, HashSet<string>>
        {
            [BlacklistType.Login] = LoginBlacklist
        };

        public static void Load()
        {
            foreach (BlacklistType blacklistType in Enum.GetValues(typeof(BlacklistType)))
                Dict[blacklistType].LoadFromFile($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt");
        }
    }
}
