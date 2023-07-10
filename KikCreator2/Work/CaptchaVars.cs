using DankLibWaifuz.Etc;

namespace KikCreator2.Work
{
    internal class CaptchaVars
    {
        public string Url { get; }
        public string Id { get; }
        public string Response { get; set; }
        public bool IsValid { get; }
        public string Solution { get; set; }

        public CaptchaVars(string url, string id)
        {
            Url = url;
            Id = id;

            IsValid = !GeneralHelpers.AnyNullOrWhiteSpace(Url, Id);
        }
    }
}
