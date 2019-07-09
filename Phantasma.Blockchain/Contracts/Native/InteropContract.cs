using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct InteropBlock
    {
        public string Chain;
        public Hash Hash;
        public Event[] Events;
    }

    public sealed class InteropContract : SmartContract
    {
        public override string Name => "interop";

        private StorageMap _hashes; 

        public InteropContract() : base()
        {
        }

        // receive from external chain
        public void SettleBlock(Hash hash, string chain)
        {
            Runtime.Expect(chain == "NEO", "Only NEO supported for now");

            //Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "invalid witness");

            var chainHashes = _hashes.Get<string, StorageSet>(chain);
            Runtime.Expect(!chainHashes.Contains<Hash>(hash), "hash already seen");
            chainHashes.Add<Hash>(hash);

            var interopBytes = Runtime.OracleReader($"interop://{chain}/{hash}");
            var interopBlock = Serialization.Unserialize<InteropBlock>(interopBytes);

            Runtime.Expect(interopBlock.Chain == chain, "unxpected chain");
            Runtime.Expect(interopBlock.Hash == hash, "unxpected hash");

            foreach (var evt in interopBlock.Events)
            {
                if (evt.Kind == EventKind.TokenSend)
                {
                    var destination = evt.Address;
                    Runtime.Expect(destination != Address.Null, "invalid destination");

                    var transfer = evt.GetContent<TokenEventData>();
                    Runtime.Expect(transfer.value > 0, "amount must be positive and greater than zero");

                    Runtime.Expect(Runtime.Nexus.TokenExists(transfer.symbol), "invalid token");
                    var token = this.Runtime.Nexus.GetTokenInfo(transfer.symbol);


                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
                    Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

                    var source = Address.Null;

                    Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, transfer.symbol, destination, transfer.value), "mint failed");
                    Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = transfer.value, symbol = transfer.symbol});
                }
            }
        }

        // send to external chain
        public void WithdrawTokens(Address destination, string symbol, BigInteger amount)
        {
            Runtime.Expect(amount > 0, "amount must be positive and greater than zero");
            Runtime.Expect(destination != Address.Null, "invalid destination");
            Runtime.Expect(IsWitness(Runtime.Nexus.GenesisAddress), "invalid witness");

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var token = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "token must be transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.External), "token must be external");

            var source = Address.Null;

            Runtime.Expect(Runtime.Nexus.BurnTokens(Runtime, symbol, destination, amount), "burn failed");

            Runtime.Notify(EventKind.TokenSend, destination, new TokenEventData() { chainAddress = this.Runtime.Chain.Address, value = amount, symbol = symbol });
        }
    }
}
