using DankLibWaifuz.CollectionsWaifu;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KikCreator2.Declarations
{
    internal enum BlacklistType
    {
        Email
    }

    internal static class Blacklists
    {
        private static readonly HashSet<string> EmailBlacklist = new HashSet<string>();

        public static Dictionary<BlacklistType, HashSet<string>> Dict { get; } = new Dictionary<BlacklistType, HashSet<string>>
        {
            [BlacklistType.Email] = EmailBlacklist
        };

        public static void Load()
        {
            foreach (BlacklistType blacklistType in Enum.GetValues(typeof(BlacklistType)))
                Dict[blacklistType].LoadFromFile($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt");
        }
    }
}
