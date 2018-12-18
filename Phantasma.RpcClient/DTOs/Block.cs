using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class Block
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("height")]
        public long Height { get; set; }

        [JsonProperty("chainAddress")]
        public string ChainAddress { get; set; }

        [JsonProperty("chainName")]
        public string ChainName { get; set; }

        [JsonProperty("previousHash")]
        public string PreviousHash { get; set; }

        [JsonProperty("nonce")]
        public long Nonce { get; set; }

        [JsonProperty("minerAddress")]
        public string MinerAddress { get; set; }


        public static Block FromJson(string json) => JsonConvert.DeserializeObject<Block>(json, JsonUtils.Settings);

        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    }
}
