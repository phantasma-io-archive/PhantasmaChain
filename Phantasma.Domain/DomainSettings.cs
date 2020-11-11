using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public enum TriggerResult
    {
        Failure,
        Missing,
        Success,
    }

    public enum AccountTrigger
    {
        OnMint, // address, symbol, amount
        OnBurn, // address, symbol, amount
        OnSend, // address, symbol, amount
        OnReceive, // address, symbol, amount
        OnWitness, // address
        OnUpgrade, // address
    }

    public enum TokenTrigger
    {
        OnMint, // address, symbol, amount
        OnBurn, // address, symbol, amount
        OnSend, // address, symbol, amount
        OnReceive, // address, symbol, amount
        OnUpgrade, // address
    }

    public enum OrganizationTrigger
    {
        OnAdd, // address
        OnRemove, // address
        OnUpgrade, // address
    }

    public static class DomainSettings
    {
        public const int MAX_TOKEN_DECIMALS = 18;

        public const string FuelTokenSymbol = "KCAL";
        public const string FuelTokenName = "Phantasma Energy";
        public const int FuelTokenDecimals = 10;

        public const string StakingTokenSymbol = "SOUL";
        public const string StakingTokenName = "Phantasma Stake";
        public const int StakingTokenDecimals = 8;

        public const string FiatTokenSymbol = "USD";
        public const string FiatTokenName = "Dollars";
        public const int FiatTokenDecimals = 8;

        public const string RootChainName = "main";

        public const string ValidatorsOrganizationName = "validators";
        public const string MastersOrganizationName = "masters";
        public const string StakersOrganizationName = "stakers";

        public const string PhantomForceOrganizationName = "phantom_force";

        public static readonly BigInteger PlatformSupply = UnitConversion.ToBigInteger(100000000, FuelTokenDecimals);
        public static readonly string PlatformName = "phantasma";

        public static readonly int ArchiveMinSize = 64; // in bytes
        public static readonly int ArchiveMaxSize = 104857600; //100mb
        public static readonly uint ArchiveBlockSize = MerkleTree.ChunkSize;
    }
}
