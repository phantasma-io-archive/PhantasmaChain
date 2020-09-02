using System.Collections.Generic;

namespace Phantasma.Ethereum.RLP
{
    public class RLPCollection : List<IRLPElement>, IRLPElement
    {
        public byte[] RLPData { get; set; }
    }
}
