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

        [JsonProperty("balances")]
        public List<BalanceSheetDto> Tokens { get; set; } = new List<BalanceSheetDto>();


        public static AccountDto FromJson(string json) => JsonConvert.DeserializeObject<AccountDto>(json, JsonUtils.Settings);

        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    }
}
