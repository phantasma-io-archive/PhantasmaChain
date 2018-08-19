using Phantasma.VM.Types;
using System;

namespace Phantasma.VM.Contracts
{
    public static class ContractExtensions
    {
        public static void Expect(this IContract contract, bool assertion)
        {
            throw new NotImplementedException();
        }

        public static Address GetSource(this ITransaction tx)
        {
            return new Address(tx.PublicKey);
        }

        public static Address GetAddress(this IContract contract)
        {
            return new Address(contract.PublicKey);
        }
    }
}
