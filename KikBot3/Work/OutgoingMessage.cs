namespace KikBot3.Work
{
    internal class OutgoingMessage
    {
        public OutgoingMessage(string toJid, string body)
        {
            ToJid = toJid;

            if (body.Contains("%s"))
                IsLink = true;
            else if (body.Contains("%p"))
                IsImage = true;

            Body = body;
        }

        public string ToJid { get; }
        public string Body { get; }
        public bool IsLink { get; }
        public bool IsImage { get; }
        public bool IsKeepAlive { get; set; }

#if BLASTER
        public bool IsBlast { get; set; }
#endif

    }
}
