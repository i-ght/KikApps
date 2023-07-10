using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using System.Collections.Generic;

namespace KikRateTester2.Declarations
{
    internal static class Collections
    {
        public static SettingsUi SettingsUi { get; set; }

        public static Queue<string> Proxies => SettingsUi.GetQueue("Proxies");

        public static Queue<string> ReceiveProxies => SettingsUi.GetQueue("ReceiveProxies");

        public static Queue<string> ReceiveAccounts => SettingsUi.GetQueue("ReceiveAccounts");

        public static Queue<string> SendAccounts => SettingsUi.GetQueue("SendAccounts");

        public static Queue<string> MessagesToSend => SettingsUi.GetQueue("MessagesToSend");

        public static void Shuffle()
        {
            Proxies.Shuffle();
            ReceiveProxies.Shuffle();
            MessagesToSend.Shuffle();
        }

        public static bool IsValid()
        {
            if (Proxies.Count == 0 || ReceiveProxies.Count == 0 || ReceiveAccounts.Count == 0 ||
                SendAccounts.Count == 0 || MessagesToSend.Count == 0)
                return false;

            return true;
        }
    }
}
