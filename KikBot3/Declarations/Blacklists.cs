using System;
using System.Collections.Generic;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.Etc;

namespace KikBot3.Declarations
{
    internal enum BlacklistType
    {
        Chat,
        Message
#if BLASTER
        ,Blast
#endif
    }

    internal static class Blacklists
    {
        private static readonly HashSet<string> ChatBlacklist = new HashSet<string>();
        private static readonly HashSet<string> MessageBlacklist = new HashSet<string>();

#if BLASTER
        private static readonly HashSet<string> BlastBlacklist = new HashSet<string>();
#endif

        static Blacklists()
        {
            Collections = new Dictionary<BlacklistType, HashSet<string>>
            {
                [BlacklistType.Chat] = ChatBlacklist,
                [BlacklistType.Message] = MessageBlacklist
#if BLASTER
                ,[BlacklistType.Blast] = BlastBlacklist
#endif
            };
        }

        public static Dictionary<BlacklistType, HashSet<string>> Collections { get; }

        public static void Load()
        {
            foreach (BlacklistType blacklistType in Enum.GetValues(typeof(BlacklistType)))
                Collections[blacklistType].LoadFromFile($"{GeneralHelpers.ApplicationName()}" +
                                                        $"-{blacklistType.ToString().ToLower()}_" +
                                                        "blacklist.txt");

        }
    }
}
