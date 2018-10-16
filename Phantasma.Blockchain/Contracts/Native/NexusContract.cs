using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct TokenEventData
    {
        public string symbol;
        public BigInteger amount;
        public Address chainAddress;
    }

    public sealed class NexusContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Nexus;

        public NexusContract() : base()
        {
        }

        public bool IsChain(Address address)
        {
            return Runtime.Nexus.FindChainByAddress(address) != null;
        }

        public bool IsRootChain(Address address)
        {
            return (IsChain(address) && address == this.Runtime.Chain.Address);
        }

        public bool IsSideChain(Address address)
        {
            return (IsChain(address) && address != this.Runtime.Chain.Address);
        }

        public Token CreateToken(Address owner, string symbol, string name, BigInteger maxSupply)
        {
            Expect(!string.IsNullOrEmpty(symbol));
            Expect(!string.IsNullOrEmpty(name));
            Expect(maxSupply > 0);

            Expect(IsWitness(owner));

            symbol = symbol.ToUpperInvariant();

            var token = this.Runtime.Nexus.CreateToken(owner, symbol, name, maxSupply);
            Expect(token != null);

            Runtime.Notify(EventKind.TokenCreate, owner, symbol);

            return token;
        }

        public Chain CreateChain(Address owner, string name, string parentName)
        {
            Expect(!string.IsNullOrEmpty(name));
            Expect(!string.IsNullOrEmpty(parentName));

            Expect(IsWitness(owner));

            name = name.ToLowerInvariant();
            Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase));

            var parent = this.Runtime.Nexus.FindChainByName(parentName);
            Expect(parent != null);

            var chain = this.Runtime.Nexus.CreateChain(owner, name, parent, this.Runtime.Block);
            Expect(chain != null);

            Runtime.Notify(EventKind.ChainCreate, owner, chain.Address);

            return chain;
        }

        public void SendTokens(Address targetChain, Address from, Address to, string symbol, BigInteger amount)
        {
            Expect(IsWitness(from));

            if (IsRootChain(this.Runtime.Chain.Address))
            {
                Expect(IsSideChain(targetChain));
            }
            else
            {
                Expect(IsRootChain(targetChain));
            }

            var fee = amount / 10;
            Expect(fee > 0);

            var otherChain = this.Runtime.Chain.FindChain(targetChain);
            /*TODO
            var otherConsensus = (ConsensusContract)otherChain.FindContract(ContractKind.Consensus);
            Expect(otherConsensus.IsValidReceiver(from));*/

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            token.Burn(balances, from, amount);

            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { symbol = symbol, amount = amount, chainAddress = targetChain });
        }

        public void ReceiveTokens(Address sourceChain, Address from, Address to, Hash hash)
        {
            if (IsRootChain(this.Runtime.Chain.Address))
            {
                Expect(IsSideChain(sourceChain));
            }
            else
            {
                Expect(IsRootChain(sourceChain));
            }

            Expect(!IsKnown(hash));

            var otherChain = this.Runtime.Chain.FindChain(sourceChain);

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

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);

            token.Mint(balances, to, amount);
            Runtime.Notify(EventKind.TokenReceive, to, new TokenEventData() { symbol = symbol, amount = amount, chainAddress = otherChain.Address});

            RegisterHashAsKnown(Runtime.Transaction.Hash);
        }

        public void MintTokens(Address target, string symbol, BigInteger amount)
        {
            Expect(amount > 0);

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            Expect(IsWitness(token.Owner));

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Mint(balances, target, amount));

            Runtime.Notify(EventKind.TokenMint, target, new TokenEventData() { symbol = symbol, amount = amount, chainAddress = this.Runtime.Chain.Address });
        }

        public void BurnTokens(Address from, string symbol, BigInteger amount)
        {           
            Expect(amount > 0);
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Burn(balances, from, amount));

            Runtime.Notify(EventKind.TokenBurn, from, new TokenEventData() { symbol = symbol, amount = amount });
        }

        public void TransferTokens(Address target, Address destination, string symbol, BigInteger amount)
        {
            Expect(amount > 0);
            Expect(target != destination);
            Expect(IsWitness(target));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(token.Transfer(balances, target, destination, amount));

            Runtime.Notify(EventKind.TokenSend, target, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = amount, symbol = symbol });
            Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, amount = amount, symbol = symbol });
        }

        public BigInteger GetBalance(Address address, string symbol)
        {
            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            return balances.Get(address);
        }

    }
}
