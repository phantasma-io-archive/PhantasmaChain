using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    //public class Chains
    //{
    //    [JsonProperty("chains")]
    //    public List<Chain> ChainList { get; set; }

    //    public static Chains FromJson(string json) => JsonConvert.DeserializeObject<Chains>(json, JsonUtils.Settings);

    //    public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    //}

    public class Chain
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("children")]
        public List<Chain> Children { get; set; }
    }
}
