using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using System;

namespace Phantasma.Contracts.Native
{
    public sealed class NexusContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Nexus;

        public const int MAX_TOKEN_DECIMALS = 18;

        public NexusContract() : base()
        {
        }

        public void CreateToken(Address from, string symbol, string name, string platform, Hash hash, BigInteger maxSupply, BigInteger decimals, TokenFlags flags, byte[] script)
        {
            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(!string.IsNullOrEmpty(symbol), "token symbol required");
            Runtime.Expect(!string.IsNullOrEmpty(name), "token name required");
            Runtime.Expect(maxSupply >= 0, "token supply cant be negative");
            Runtime.Expect(decimals >= 0, "token decimals cant be negative");
            Runtime.Expect(decimals <= MAX_TOKEN_DECIMALS, $"token decimals cant exceed {MAX_TOKEN_DECIMALS}");

            Runtime.Expect(!Runtime.TokenExists(symbol), "token already exists");

            if (symbol == DomainSettings.FuelTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Fuel), "token should be native");
            }
            else
            {
                Runtime.Expect(!flags.HasFlag(TokenFlags.Fuel), "token can't be native");
            }

            if (symbol == DomainSettings.StakingTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Stakable), "token should be stakable");
            }

            if (symbol == DomainSettings.FiatTokenSymbol)
            {
                Runtime.Expect(flags.HasFlag(TokenFlags.Fiat), "token should be fiat");
            }

            Runtime.Expect(!string.IsNullOrEmpty(platform), "chain name required");

            if (flags.HasFlag(TokenFlags.External))
            {
                Runtime.Expect(from == Runtime.Nexus.GenesisAddress, "genesis address only");
                Runtime.Expect(platform != DomainSettings.PlatformName, "external token chain required");
                Runtime.Expect(Runtime.PlatformExists(platform), "platform not found");
            }
            else
            {
                Runtime.Expect(platform == DomainSettings.PlatformName, "chain name is invalid");
            }

            Runtime.Expect(from.IsUser, "owner address must be user address");
            Runtime.Expect(Runtime.IsStakeMaster(from), "needs to be master");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(this.Runtime.CreateToken(symbol, name, platform, hash, maxSupply, (int)decimals, flags, script), "token creation failed");
            Runtime.Notify(EventKind.TokenCreate, from, symbol);
        }

        public void CreateChain(Address owner, string name, string parentName)
        {
            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");
            Runtime.Expect(!string.IsNullOrEmpty(parentName), "parent chain required");

            Runtime.Expect(owner.IsUser, "owner address must be user address");
            Runtime.Expect(Runtime.IsStakeMaster(owner), "needs to be master");
            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            name = name.ToLowerInvariant();
            Runtime.Expect(!name.Equals(parentName, StringComparison.OrdinalIgnoreCase), "same name as parent");

            Runtime.Expect(this.Runtime.CreateChain(owner, name, parentName), "chain creation failed");

            Runtime.Notify(EventKind.ChainCreate, owner, name);
        }

        public void CreateFeed(Address owner, string name, FeedMode mode)
        {
            var pow = Runtime.Transaction.Hash.GetDifficulty();
            Runtime.Expect(pow >= (int)ProofOfWork.Minimal, "expected proof of work");

            Runtime.Expect(!string.IsNullOrEmpty(name), "name required");

            Runtime.Expect(owner.IsUser, "owner address must be user address");
            Runtime.Expect(Runtime.IsStakeMaster(owner), "needs to be master");
            Runtime.Expect(Runtime.IsWitness(owner), "invalid witness");

            Runtime.Expect(Runtime.CreateFeed(owner, name, mode), "feed creation failed");

            Runtime.Notify(EventKind.FeedCreate, owner, name);
        }

        public void CreatePlatform(Address target, string fuelSymbol)
        {
            Runtime.Expect(Runtime.IsWitness(Runtime.Nexus.GenesisAddress), "must be genesis");

            Runtime.Expect(target.IsInterop, "external address must be interop");

            string platformName;
            byte[] data;
            target.DecodeInterop(out platformName, out data, 0);

            Runtime.Expect(ValidationUtils.IsValidIdentifier(platformName), "invalid platform name");

            Runtime.Expect(Runtime.CreatePlatform(target, platformName, fuelSymbol), "creation of platform failed");

            Runtime.Notify(EventKind.AddressRegister, target, platformName);
        }
    }
}
