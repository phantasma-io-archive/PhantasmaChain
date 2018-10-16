using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class BankContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Bank;

    }
}
