using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class Chains
    {
        [JsonProperty("chains")]
        public List<ChainElement> ChainList { get; set; }

        public static Chains FromJson(string json) => JsonConvert.DeserializeObject<Chains>(json, JsonUtils.Settings);

        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);

    }
    public class ChainElement
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("parentName")]
        public string ParentChainName { get; set; }

        [JsonProperty("parentAddress")]
        public string ParentChainAddress { get; set; }

        [JsonProperty("children")]
        public List<ChainElement> Children { get; set; }
    }

    public class ChildChain
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }
    }
}
