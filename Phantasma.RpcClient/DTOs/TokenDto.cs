using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class TokenDto
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

        [JsonProperty("owner")]
        public string Owner { get; set; }

        [JsonProperty("flags")]
        public TokenFlags Flags { get; set; }
    }

    [Flags]
    public enum TokenFlags
    {
        None = 0,
        Transferable = 1 << 0,
        Fungible = 1 << 1,
        Finite = 1 << 2,
        Divisible = 1 << 3,
    }

    public class TokenList
    {
        [JsonProperty("tokens")]
        public List<TokenDto> Tokens { get; set; }
    }
    
}
