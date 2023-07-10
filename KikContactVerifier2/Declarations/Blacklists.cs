using DankLibWaifuz.CollectionsWaifu;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KikContactVerifier2.Declarations
{
    internal enum BlacklistType
    {
        Verify,
        Rated
    }

    internal static class Blacklists
    {
        private static readonly HashSet<string> VerifyBlacklist = new HashSet<string>();
        private static readonly HashSet<string> RatedBlacklist = new HashSet<string>();

        public static Dictionary<BlacklistType, HashSet<string>> Dict { get; } = new Dictionary<BlacklistType, HashSet<string>>
        {
            [BlacklistType.Verify] = VerifyBlacklist,
            [BlacklistType.Rated] = RatedBlacklist
        };

        public static void Load()
        {
            foreach (BlacklistType blacklistType in Enum.GetValues(typeof(BlacklistType)))
                Dict[blacklistType].LoadFromFile($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt");
        }
    }
}
