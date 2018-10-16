using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class NexusContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Nexus;

        public NexusContract() : base()
        {
        }

        public bool IsRootChain(Address address)
        {
            return (address == this.Chain.Address);
        }

        public bool IsSideChain(Address address)
        {
            return false;
        }

        public Token CreateToken(Address owner, string symbol, string name, BigInteger maxSupply)
        {
            Expect(!string.IsNullOrEmpty(symbol));
            Expect(!string.IsNullOrEmpty(name));
            Expect(maxSupply > 0);

            Expect(IsWitness(owner));

            symbol = symbol.ToUpperInvariant();

            var token = this.Nexus.CreateToken(owner, symbol, name, maxSupply);
            Expect(token != null);

            return token;
        }

        public Chain CreateChain(Address owner, string name, string parentName)
        {
            Expect(!string.IsNullOrEmpty(name));
            Expect(!string.IsNullOrEmpty(parentName));

            Expect(IsWitness(owner));

            name = name.ToLowerInvariant();
            Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase));

            var parent = this.Nexus.FindChainByName(parentName);
            Expect(parent != null);

            var chain = this.Nexus.CreateChain(owner, name, parent, this.Block);
            Expect(chain != null);

            return chain;
        }

        public void SendTokens(Address targetChain, Address from, Address to, string symbol, BigInteger amount)
        {
            Expect(IsWitness(from));

            if (IsRootChain(this.Chain.Address))
            {
                Expect(IsSideChain(targetChain));
            }
            else
            {
                Expect(IsRootChain(targetChain));
            }

            Expect(!IsKnown(Transaction.Hash));

            var fee = amount / 10;
            Expect(fee > 0);

            var otherChain = this.Chain.FindChain(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Expect(otherConsensus.IsValidReceiver(from));*/

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);
            token.Burn(balances, from, amount);

            RegisterHashAsKnown(Transaction.Hash);
        }

        public void ReceiveTokens(Address sourceChain, Address from, Address to, Hash hash)
        {
            if (IsRootChain(this.Chain.Address))
            {
                Expect(IsSideChain(sourceChain));
            }
            else
            {
                Expect(IsRootChain(sourceChain));
            }

            Expect(!IsKnown(hash));

            var otherChain = this.Chain.FindChain(sourceChain);

            var tx = otherChain.FindTransaction(hash);
            Expect(tx != null);

            var disasm = new Disassembler(tx.Script);
            var instructions = disasm.GetInstructions();

            string symbol = null;
            BigInteger amount = 0;
            foreach (var ins in instructions)
            {
                if (ins.Opcode == Opcode.CALL)
                {
                    // TODO
                }
            }

            Expect(symbol != null);

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);

            token.Mint(balances, to, amount);

            RegisterHashAsKnown(Transaction.Hash);
        }

        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Expect(amount > 0);

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            Expect(IsWitness(token.Owner));

            var balances = this.Chain.GetTokenBalances(token);
            Expect(token.Mint(balances, target, amount));
        }

        public void BurnTokens(Address target, string symbol, BigInteger amount)
        {           
            Expect(amount > 0);
            Expect(IsWitness(target));

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);
            Expect(token.Burn(balances, target, amount));           
        }

        public void TransferTokens(Address source, Address destination, string symbol, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(source != destination);
            Expect(IsWitness(source));

            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);
            Expect(token.Transfer(balances, source, destination, amount));
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }

    }
}
