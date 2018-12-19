using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Phantasma.RpcClient.DTOs
{
    public class EventDto
    {
        [JsonProperty("address")]
        public string EventAddress { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("kind")]
        [JsonConverter(typeof(StringEnumConverter))]
        public EvtKind EvtKind { get; set; }
    }

    public enum EvtKind
    {
        ChainCreate,
        TokenCreate,
        TokenSend,
        TokenReceive,
        TokenMint,
        TokenBurn,
        TokenEscrow,
        AddressRegister,
        FriendAdd,
        FriendRemove,
        GasEscrow,
        GasPayment,
    }
}
