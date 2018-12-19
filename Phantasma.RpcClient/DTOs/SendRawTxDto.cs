using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class SendRawTxDto
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        public bool HasError => string.IsNullOrEmpty(Hash) && !string.IsNullOrEmpty(Error);

        public static SendRawTxDto FromJson(string json) => JsonConvert.DeserializeObject<SendRawTxDto>(json, JsonUtils.Settings);
    }
}
