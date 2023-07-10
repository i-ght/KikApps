using DankLibWaifuz.Etc;

namespace KikBot3.Work
{
    internal class SpoofedLinkInfo
    {
        public string SpoofedDomain { get; }
        public string LinkTitle { get; }
        public string AppName { get; }

        public bool IsValid { get; }

        public SpoofedLinkInfo(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.Contains("|"))
                return;

            var split = input.Split('|');
            SpoofedDomain = split[0];
            LinkTitle = split[1];
            AppName = split[2];

            IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(SpoofedDomain, LinkTitle, AppName);
        }
    }
}
