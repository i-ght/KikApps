using DankLibWaifuz.CollectionsWaifu;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace KikBot2.Declarations
{
    internal enum BlacklistType
    {
        Chat,
        Message
    }

    internal static class Blacklists
    {
        private static readonly HashSet<string> ChatBlacklist = new HashSet<string>();
        private static readonly HashSet<string> MessageBlacklist = new HashSet<string>();

        public static Dictionary<BlacklistType, HashSet<string>> Dict { get; } = new Dictionary<BlacklistType, HashSet<string>>
        {
            [BlacklistType.Chat] = ChatBlacklist,
            [BlacklistType.Message] = MessageBlacklist
        };

        public static void Load()
        {
            foreach (BlacklistType blacklistType in Enum.GetValues(typeof(BlacklistType)))
                Dict[blacklistType].LoadFromFile($"{Assembly.GetEntryAssembly().GetName().Name.Replace(" ", "_").ToLower()}-{blacklistType.ToString().ToLower()}_blacklist.txt");
        }
    }
}
