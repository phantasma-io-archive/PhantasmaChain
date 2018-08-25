using Phantasma.Cryptography;
using Phantasma.Mathematics;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class NexusContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Nexus;

        private HashSet<Hash> knownTransactions = new HashSet<Hash>();

        public NexusContract() : base()
        {
        }

        public bool IsKnown(Hash hash)
        {
            return knownTransactions.Contains(hash);
        }

        public bool IsRootChain(Address address)
        {
            return (address == this.Chain.Address);
        }

        public bool IsSideChain(Address address)
        {
            return false;
        }

        public void Send(Address from, Address to, BigInteger amount)
        {
            Expect(Transaction.IsSignedBy(from));

            if (IsRootChain(this.Chain.Address))
            {
                Expect(IsSideChain(to));
            }
            else
            {
                Expect(IsRootChain(to));
            }

            Expect(!IsKnown(Transaction.Hash));

            var fee = amount / 10;
            Expect(fee > 0);

            var token = (TokenContract) this.Chain.FindContract(NativeContractKind.Token);
            token.SetData(this.Chain, this.Transaction);

            token.Burn(from, amount);

            knownTransactions.Add(Transaction.Hash);
        }

        public void Receive(Address from, Address to, Hash hash)
        {
            if (IsRootChain(this.Chain.Address))
            {
                Expect(IsSideChain(from));
            }
            else
            {
                Expect(IsRootChain(from));
            }

            Expect(!IsKnown(hash));

            var otherChain = this.Chain.FindChain(from);
            var otherNexus = (NexusContract) otherChain.FindContract(NativeContractKind.Nexus);
            Expect(otherNexus.IsKnown(hash));

            var tx = otherChain.FindTransaction(hash);

            var token = (TokenContract)this.Chain.FindContract(NativeContractKind.Token);
            token.Mint(to, 0); // TODO FIX ME
            throw new NotImplementedException();

            knownTransactions.Add(Transaction.Hash);
        }
    }
}
