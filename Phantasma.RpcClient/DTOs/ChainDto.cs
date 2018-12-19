using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class ChainDto
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("parentAddress")]
        public string ParentAddress { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("children")]
        public List<ChainDto> Children { get; set; }
    }
}
