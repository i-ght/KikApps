using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using System.Collections.Generic;

namespace KikContactVerifier2.Declarations
{
    internal static class Collections
    {
        public static SettingsUi SettingsUi { get; set; }

        public static Queue<string> Accounts => SettingsUi.GetQueue("Accounts");

        public static Queue<string> Proxies => SettingsUi.GetQueue("Proxies");

        public static FileStreamWaifu Contacts => SettingsUi.GetFileStream("Contacts");

        public static Queue<string> VerifiedContacts => SettingsUi.GetQueue("VerifiedContacts");

        public static void Shuffle()
        {
            Proxies.Shuffle();
        }
    }
}
