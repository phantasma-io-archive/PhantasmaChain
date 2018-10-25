using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class MessagingContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Names;

        public MessagingContract() : base()
        {
        }
    }
}
