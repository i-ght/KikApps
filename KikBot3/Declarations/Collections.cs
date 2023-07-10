using System.Collections.Generic;
using System.IO;
using DankLibWaifuz.CollectionsWaifu;
using DankLibWaifuz.SettingsWaifu;

namespace KikBot3.Declarations
{
    internal static class Collections
    {
        public static SettingsUi SettingsUi { get; set; }

        public static Queue<string> Accounts => SettingsUi.GetQueue("Accounts");

        public static Queue<string> Proxies => SettingsUi.GetQueue("Proxies");

        public static Queue<string> Links => SettingsUi.GetQueue("Links");

        public static Queue<string> KeepAlives => SettingsUi.GetQueue("KeepAlives");

        public static List<string> Script => SettingsUi.GetList("Script");

        public static List<string> Restricts => SettingsUi.GetList("Restricts");

        public static Queue<string> Apologies => SettingsUi.GetQueue("Apologies");

        public static Queue<string> WebBrowserProxies => SettingsUi.GetQueue("WebBrowserProxies");

        public static Dictionary<string, List<string>> Keywords => SettingsUi.GetKeywordsList("Keywords");

#if BLASTER
        public static FileStreamWaifu Contacts => SettingsUi.GetFileStream("Contacts");

        public static Queue<string> Greets => SettingsUi.GetQueue("Greets");
#endif

        private static List<string> _imageFiles = new List<string>();
        public static List<string> ImageFiles
        {
            get
            {
                var pathToImageFiles = Settings.Get<string>("ImageFilesDir");
                if (!Directory.Exists(pathToImageFiles))
                    return _imageFiles;

                var jpgs = new HashSet<string>(Directory.GetFiles(pathToImageFiles, "*.jpg"));
                if (jpgs.SetEquals(_imageFiles))
                    return _imageFiles;

                _imageFiles = new List<string>(jpgs);
                return _imageFiles;
            }
        }

        public static bool IsValid()
        {
            if (Accounts.Count == 0 || Links.Count == 0 ||
                Script.Count == 0
                || Proxies.Count == 0)
                return false;

            return true;
        }

        public static void Shuffle()
        {
            Proxies.Shuffle();
            Links.Shuffle();
            KeepAlives.Shuffle();

#if BLASTER
            Greets.Shuffle();
#endif
        }
    }
}
