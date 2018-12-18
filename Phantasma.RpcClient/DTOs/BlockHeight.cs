using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class BlockHeight
    {
        [JsonProperty("chain")]
        public string Chain { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}
