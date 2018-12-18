using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class SendRawTx
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        public bool HasError => string.IsNullOrEmpty(Hash) && !string.IsNullOrEmpty(Error);

        public static SendRawTx FromJson(string json) => JsonConvert.DeserializeObject<SendRawTx>(json, JsonUtils.Settings);
    }
}
