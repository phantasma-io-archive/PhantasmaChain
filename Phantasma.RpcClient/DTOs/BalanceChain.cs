using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class BalanceChain
    {
        [JsonProperty("chain")]
        public string ChainName { get; set; }

        [JsonProperty("balance")]
        public string Balance { get; set; }

        [JsonProperty("ids")]
        public List<string> Ids { get; set; } = new List<string>();
    }
}
