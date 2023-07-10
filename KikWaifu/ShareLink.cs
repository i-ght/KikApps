namespace KikWaifu
{
    public class ShareLink
    {
        public string Link { get; }
        public string ToJid { get; }
        public string AppName { get; }
        public string SpoofedDomain { get; }
        public string LinkTitle { get; }

        public ShareLink(string toJid, string link, string spoofedDomain, string linkTitle, string appName)
        {
            ToJid = toJid;
            Link = link;
            SpoofedDomain = spoofedDomain;
            LinkTitle = linkTitle;
            AppName = appName;
        }
    }
}
