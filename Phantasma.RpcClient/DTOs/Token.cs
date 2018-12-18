using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class Token
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("decimals")]
        public int Decimals { get; set; }

        [JsonProperty("isFungible")]
        public bool Fungible { get; set; }

        [JsonProperty("currentSupply")]
        public string CurrentSupply { get; set; }

        [JsonProperty("maxSupply")]
        public string MaxSupply { get; set; }

        [JsonProperty("chains")]
        public List<BalanceChain> Chains { get; set; } //todo remove from DTO
    }

    public class TokenList
    {
        [JsonProperty("tokens")]
        public List<Token> Tokens { get; set; }
    }
    
}
