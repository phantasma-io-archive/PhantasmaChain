using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class RootChain
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}
