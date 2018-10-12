using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class NamingContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Naming;

        public NamingContract() : base()
        {
        }
    }
}
