using System.Collections.Generic;

namespace Phantasma.RpcClient.DTOs
{
    public class SendHolding
    {
        public string ChainName  { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public decimal Amount { get; set; }
        public bool Fungible { get; set; }
        public List<string> Ids { get; set; }
    }
}
