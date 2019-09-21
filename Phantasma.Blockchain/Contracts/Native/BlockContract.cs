using Phantasma.Cryptography;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class BlockContract: SmartContract
    {
        public override string Name => "block";

        public BlockContract() : base()
        {
        }

        public void OpenBlock(Address from)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var count = Runtime.Nexus.GetActiveValidatorCount();
            if (count > 0)
            {
                Runtime.Expect(Runtime.Nexus.IsKnownValidator(from), "validator failed");
                Runtime.Expect(Runtime.Chain.IsCurrentValidator(from), "current validator mismatch");
            }
            else
            {
                Runtime.Expect(Runtime.Chain.IsRoot, "must be root chain");
            }

            Runtime.Notify(EventKind.BlockCreate, from, Runtime.Chain.Address);
        }

        public void CloseBlock(Address from)
        {
            Runtime.Expect(Runtime.Nexus.IsActiveValidator(from), "validator failed");
            Runtime.Expect(Runtime.Chain.IsCurrentValidator(from), "current validator mismatch");
            Runtime.Expect(IsWitness(from), "witness failed");

            var validators = Runtime.Nexus.GetActiveValidatorAddresses();
            Runtime.Expect(validators.Length > 0, "no active validators found");

            var totalAvailable = Runtime.GetBalance(Nexus.FuelTokenSymbol, this.Address);
            var amountPerValidator = totalAvailable / validators.Length;
            Runtime.Expect(amountPerValidator > 0, "not enough fees available");

            Runtime.Notify(EventKind.BlockClose, from, Runtime.Chain.Address);

            int delivered = 0;
            for (int i = 0; i < validators.Length; i++)
            {
                var validatorAddress = validators[i];
                if (Runtime.Nexus.TransferTokens(Runtime, Nexus.FuelTokenSymbol, this.Address, validatorAddress, amountPerValidator))
                {
                    Runtime.Notify(EventKind.TokenReceive, validatorAddress, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amountPerValidator, symbol = Nexus.FuelTokenSymbol });
                    delivered++;
                }
            }

            Runtime.Expect(delivered > 0, "failed to claim fees");
        }
    }
}
