using Phantasma.Cryptography;
using Phantasma.Mathematics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
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

        public void Send(Address token, Address from, Address to, BigInteger amount)
        {
            Expect(IsWitness(from));

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

            var otherChain = this.Chain.FindChain(to);
            var otherConsensus = (ConsensusContract)otherChain.FindContract(NativeContractKind.Consensus);
            Expect(otherConsensus.IsValidReceiver(from));

            //var tokenContract = (TokenContract) this.Chain.FindContract(NativeContractKind.Token);
            var tokenContract = this.Chain.FindContract(token);
            Expect(tokenContract != null);

            var tokenABI = Chain.FindABI(NativeABI.Token);
            Expect(tokenContract.ABI.Implements(tokenABI));

            tokenContract.SetData(this.Chain, this.Transaction, this.Storage);

            tokenABI["Transfer"].Invoke(tokenContract, from, this.Address, amount);
            tokenABI["Burn"].Invoke(tokenContract, this.Address, amount);

            knownTransactions.Add(Transaction.Hash);
        }

        public void Receive(Address token, Address from, Address to, Hash hash)
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
            BigInteger amount = null; // TODO obtain real amount from "tx"

            var tokenContract = this.Chain.FindContract(token);
            Expect(tokenContract != null);

            var tokenABI = Chain.FindABI(NativeABI.Token);
            Expect(tokenContract.ABI.Implements(tokenABI));

            tokenContract.SetData(this.Chain, this.Transaction, this.Storage);

            tokenABI["Mint"].Invoke(tokenContract, this.Address, amount);
            tokenABI["Transfer"].Invoke(tokenContract, this.Address, to, amount);

            knownTransactions.Add(Transaction.Hash);
        }
    }
}
