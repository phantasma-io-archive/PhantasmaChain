using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class NexusContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Nexus;

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

        public void CreateToken(string symbol, string name, BigInteger maxSupply)
        {
            throw new System.NotImplementedException();
        }

        public void CreateChain(string name)
        {
            throw new System.NotImplementedException();
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

            throw new System.NotImplementedException();
            /*
            var otherChain = this.Chain.FindChain(to);
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Expect(otherConsensus.IsValidReceiver(from));

            //var tokenContract = (TokenContract) this.Chain.FindContract(NativeContractKind.Token);
            var tokenContract = this.Chain.FindContract(token);
            Expect(tokenContract != null);

            var tokenABI = Chain.FindABI(NativeABI.Token);
            Expect(tokenContract.ABI.Implements(tokenABI));

            tokenContract.SetData(this.Chain, this.Transaction, this.Storage);

            tokenABI["Transfer"].Invoke(tokenContract, from, this.Address, amount);
            tokenABI["Burn"].Invoke(tokenContract, this.Address, amount);

            knownTransactions.Add(Transaction.Hash);*/
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

            throw new System.NotImplementedException();
            /*
            var otherChain = this.Chain.FindChain(from);
            var otherNexus = (NexusContract) otherChain.FindContract(ContractKind.Nexus);
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

            knownTransactions.Add(Transaction.Hash);*/
        }
    }
}
