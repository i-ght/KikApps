namespace KikWaifu
{
    public class ShareImage
    {
        public int ImageLength { get; }
        public string ToJid { get; }
        public string Base64ImageData { get; }

        public ShareImage(string toJid, int imageLen, string b64ImgBytes)
        {
            ToJid = toJid;
            ImageLength = imageLen;
            Base64ImageData = b64ImgBytes;
        }
    }
}
