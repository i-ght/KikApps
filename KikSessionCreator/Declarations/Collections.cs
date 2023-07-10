using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using System.Collections.Generic;
using System.IO;

namespace KikSessionCreator.Declarations
{
    internal static class Collections
    {
        public static SettingsUi SettingsUi { get; set; }

        public static Queue<string> Accounts => SettingsUi.GetQueue("Accounts");

        public static Queue<string> Proxies => SettingsUi.GetQueue("Proxies");

        public static Queue<string> AndroidDevices => SettingsUi.GetQueue("AndroidDevices");

        public static Queue<string> WebBrowserProxies => SettingsUi.GetQueue("WebBrowserProxies");

        public static Queue<string> SubmitCaptchaProxies => SettingsUi.GetQueue("SubmitCaptchaProxies");

        private static Queue<string> _imageDirs = new Queue<string>();
        public static Queue<string> ImageDirs
        {
            get
            {
                var imageDirs = Settings.Get<string>("ImageDirs");
                if (!Directory.Exists(imageDirs))
                    return _imageDirs;

                var dirs = new HashSet<string>(Directory.GetDirectories(imageDirs));
                if (dirs.SetEquals(_imageDirs))
                    return _imageDirs;

                _imageDirs = new Queue<string>(dirs);
                return _imageDirs;
            }
        }

        public static void Shuffle()
        {
            AndroidDevices.Shuffle();
        }
    }
}
