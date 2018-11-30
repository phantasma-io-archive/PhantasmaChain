using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class ConsensusContract : SmartContract
    {
        public override string Name => "consensus";

        public ConsensusContract() : base()
        {
        }

        public bool IsValidMiner(Address address)
        {
            return true;
        }

        public bool IsValidReceiver(Address address)
        {
            return true;
        }
    }
}
