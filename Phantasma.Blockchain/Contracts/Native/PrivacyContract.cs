using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class PrivacyContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Privacy;

        public const uint TransferAmount = 10;

        public PrivacyContract() : base()
        {
        }

        public void SendPrivate(Address from, string symbol)
        {
            Expect(IsWitness(from));

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balance = this.Chain.GetTokenBalance(token, from);
            Expect(balance >= TransferAmount);


        }
    }
}
