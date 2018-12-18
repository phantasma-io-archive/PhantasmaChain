using Newtonsoft.Json;

namespace Phantasma.RpcClient.DTOs
{
    public class TxConfirmation
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("confirmations")]
        public int Confirmations { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        public bool IsConfirmed => Confirmations >= 1;
    }
}
