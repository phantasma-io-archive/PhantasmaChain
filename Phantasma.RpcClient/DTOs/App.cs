using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class App
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("icon")]
        public string Icon { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class AppList
    {
        [JsonProperty("apps")]
        public List<App> Tokens { get; set; }
    }
}

