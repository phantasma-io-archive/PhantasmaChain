using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class AccountTransactionsDto
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }

        [JsonProperty("txs")]
        public List<TransactionDto> Txs { get; set; } = new List<TransactionDto>();

        public static AccountTransactionsDto FromJson(string json) => JsonConvert.DeserializeObject<AccountTransactionsDto>(json, JsonUtils.Settings);
        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    }
}
