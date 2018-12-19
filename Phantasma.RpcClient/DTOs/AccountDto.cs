using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class AccountDto
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("tokens")]
        public List<TokenDto> Tokens { get; set; } = new List<TokenDto>();


        public static AccountDto FromJson(string json) => JsonConvert.DeserializeObject<AccountDto>(json, JsonUtils.Settings);

        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    }
}
