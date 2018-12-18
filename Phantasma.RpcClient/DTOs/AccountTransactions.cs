using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class AccountTransactions
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }

        [JsonProperty("txs")]
        public List<Transaction> Txs { get; set; } = new List<Transaction>();

        public static AccountTransactions FromJson(string json) => JsonConvert.DeserializeObject<AccountTransactions>(json, JsonUtils.Settings);
        public string ToJson() => JsonConvert.SerializeObject(this, JsonUtils.Settings);
    }
}
