using Newtonsoft.Json;

namespace KikWaifu
{
    internal class StreamInitPropertyMap
    {
        [JsonProperty("signed")]
        public string Signed { get; set; }

        [JsonProperty("ts")]
        public long Ts { get; set; }

        [JsonProperty("n")]
        public int? N { get; set; }

        [JsonProperty("p")]
        public string P { get; set; }

        [JsonProperty("v")]
        public string V { get; set; }

        [JsonProperty("cv")]
        public string Cv { get; set; }

        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("conn")]
        public string Conn { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("lang")]
        public string Lang { get; set; }
    }
}
