using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct TokenEventData
    {
        public string symbol;
        public BigInteger value;
        public Address chainAddress;
    }

    public sealed class NexusContract : SmartContract
    {
        public override string Name => "nexus";

        public const int MAX_TOKEN_DECIMALS = 20;

        public NexusContract() : base()
        {
        }

        public void CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, BigInteger decimals, TokenFlags flags)
        {
            Runtime.Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Runtime.Expect(!string.IsNullOrEmpty(name), "token name required");
            Runtime.Expect(maxSupply >= 0, "token supply cant be negative");
            Runtime.Expect(decimals >= 0, "token decimals cant be negative");
            Runtime.Expect(decimals <= MAX_TOKEN_DECIMALS, $"token decimals cant exceed {MAX_TOKEN_DECIMALS}");

            if (symbol == Nexus.NativeTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Native), "token should be native");
            }
            else
            {
                Runtime.Expect(!flags.HasFlag(TokenFlags.Native), "token can't be native");
            }

            if (flags.HasFlag(TokenFlags.External))
            {
                Runtime.Expect(owner == Runtime.Nexus.GenesisAddress, "external token not permitted");
            }

            Runtime.Expect(IsWitness(owner), "invalid witness");

            symbol = symbol.ToUpperInvariant();

            var token = this.Runtime.Nexus.CreateToken(Runtime.Chain, owner, symbol, name, maxSupply, (int)decimals, flags);
            Runtime.Expect(token != null, "invalid token");

            if (token.IsCapped)
            {
                Runtime.Chain.InitSupplySheet(token, maxSupply);
            }

            Runtime.Notify(EventKind.TokenCreate, owner, symbol);
        }

        public void CreateChain(Address owner, string name, string parentName)
        {
            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(!string.IsNullOrEmpty(parentName), "parent chain required");

            Runtime.Expect(IsWitness(owner), "invalid witness");

            name = name.ToLowerInvariant();
            Runtime.Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase), "same name as parent");

            var parent = this.Runtime.Nexus.FindChainByName(parentName);
            Runtime.Expect(parent != null, "invalid parent");

            var chain = this.Runtime.Nexus.CreateChain(owner, name, parent, this.Runtime.Block);
            Runtime.Expect(chain != null, "chain creation failed");

            Runtime.Notify(EventKind.ChainCreate, owner, chain.Address);
        }
    }
}
