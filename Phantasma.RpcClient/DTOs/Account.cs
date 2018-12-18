using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class Account
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tokens")]
        public List<Token> Tokens { get; set; } = new List<Token>();


        public static Account FromJson(string json) => JsonConvert.DeserializeObject<Account>(json, JsonUtils.Settings);

        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    }
}
