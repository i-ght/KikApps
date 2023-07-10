using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;
using System.Collections.Generic;
using System.IO;

namespace KikCreator2.Declarations
{
    internal static class Collections
    {
        public static SettingsUi SettingsUi { get; set; }

        public static Queue<string> Proxies => SettingsUi.GetQueue("Proxies");

        public static Queue<string> WebBrowserProxies => SettingsUi.GetQueue("WebBrowserProxies");

        public static Queue<string> SubmitCaptchaProxies => SettingsUi.GetQueue("SubmitCaptchaProxies");

        public static Queue<string> Words1 => SettingsUi.GetQueue("Words1");

        public static Queue<string> Words2 => SettingsUi.GetQueue("Words2");

        public static Queue<string> FirstNames => SettingsUi.GetQueue("FirstNames");

        public static Queue<string> LastNames => SettingsUi.GetQueue("LastNames");

        public static Queue<string> AndroidDevices => SettingsUi.GetQueue("AndroidDevices");

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

        public static bool IsValid()
        {
#if DEBUG
            return true;
#endif
            if (Proxies.Count == 0 || WebBrowserProxies.Count == 0 ||
                SubmitCaptchaProxies.Count == 0 || Words1.Count == 0 || 
                Words2.Count == 0 || AndroidDevices.Count == 0 || 
                ImageDirs.Count == 0)
                return false;

            return true;
        }

        public static void Shuffle()
        {
            Words1.Shuffle();
            Words2.Shuffle();
            FirstNames.Shuffle();
            LastNames.Shuffle();
            AndroidDevices.Shuffle();
            ImageDirs.Shuffle();
        }
    }
}
