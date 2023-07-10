using Newtonsoft.Json;

namespace KikWaifu
{
    internal class StreamInitPropertyMapAnon
    {
        [JsonProperty("anon")]
        public int Anon { get; set; }
        [JsonProperty("conn")]
        public string Conn { get; set; }
        [JsonProperty("cv")]
        public string Cv { get; set; }
        [JsonProperty("dev")]
        public string Dev { get; set; }
        [JsonProperty("lang")]
        public string Lang { get; set; }
        [JsonProperty("n")]
        public int? N { get; set; }
        [JsonProperty("sid")]
        public string Sid { get; set; }
        [JsonProperty("signed")]
        public string Signed { get; set; }
        [JsonProperty("ts")]
        public long Ts { get; set; }
        [JsonProperty("v")]
        public object V { get; set; }
    }
}