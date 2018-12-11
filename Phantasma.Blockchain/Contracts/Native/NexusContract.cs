using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct TokenEventData
    {
        public string symbol;
        public LargeInteger value;
        public Address chainAddress;
    }

    public sealed class NexusContract : SmartContract
    {
        public override string Name => "nexus";

        public const int MAX_TOKEN_DECIMALS = 12;

        public NexusContract() : base()
        {
        }

        public Token CreateToken(Address owner, string symbol, string name, LargeInteger maxSupply, LargeInteger decimals, TokenFlags flags)
        {
            Runtime.Expect(!string.IsNullOrEmpty(symbol), "symbol required");
            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(maxSupply >= 0, "supply cant be negative");
            Runtime.Expect(decimals >= 0, "decimals cant be negative");
            Runtime.Expect(decimals <= MAX_TOKEN_DECIMALS, $"decimals cant exceed {MAX_TOKEN_DECIMALS}");

            Runtime.Expect(IsWitness(owner), "invalid witness");

            symbol = symbol.ToUpperInvariant();

            var token = this.Runtime.Nexus.CreateToken(Runtime.Chain, owner, symbol, name, maxSupply, (int)decimals, flags);
            Runtime.Expect(token != null, "invalid token");

            if (token.IsCapped)
            {
                Runtime.Chain.InitSupplySheet(token, maxSupply);
            }

            Runtime.Notify(EventKind.TokenCreate, owner, symbol);

            return token;
        }

        public Chain CreateChain(Address owner, string name, string parentName)
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

            return chain;
        }
    }
}
