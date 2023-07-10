using Newtonsoft.Json;

namespace KikCreator2.Work.Json
{
    internal class StreamInitPropertyMap
    {
        [JsonProperty("signed")]
        public string Signed { get; set; }

        [JsonProperty("dev")]
        public string Dev { get; set; }

        [JsonProperty("ts")]
        public long Ts { get; set; }

        [JsonProperty("n")]
        public int N { get; set; }

        [JsonProperty("v")]
        public string V { get; set; }

        [JsonProperty("cv")]
        public string Cv { get; set; }

        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("conn")]
        public string Conn { get; set; }

        [JsonProperty("anon")]
        public int Anon { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }
    }
}
