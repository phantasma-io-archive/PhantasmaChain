using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using Phantasma.VM;

namespace Phantasma.Domain
{
    public interface IRuntime
    {
        IChain Chain { get; }
        ITransaction Transaction { get; }
        Timestamp Time { get; }
        StorageContext Storage { get; }
        bool IsTrigger { get; }

        Address GasTarget { get; }
        BigInteger UsedGas { get; }
        BigInteger GasPrice { get; }

        IBlock GetBlockByHash(Hash hash);
        IBlock GetBlockByHeight(BigInteger height);

        Address GetValidator(Timestamp time);

        bool HasGenesis { get; }
        string NexusName { get; }
        Address GenesisAddress { get; }
        Hash GenesisHash { get; }
        Timestamp GetGenesisTime();

        ITransaction GetTransaction(Hash hash);

        string[] GetTokens();
        string[] GetContracts();
        string[] GetChains();
        string[] GetPlatforms();
        string[] GetFeeds();
        string[] GetOrganizations();

        IToken GetToken(string symbol);
        IFeed GetFeed(string name);
        IContract GetContract(string name);

        IPlatform GetPlatformByName(string name);
        IPlatform GetPlatformByIndex(int index);

        bool TokenExists(string symbol);
        bool FeedExists(string name);
        bool PlatformExists(string name);

        bool OrganizationExists(string name);
        IOrganization GetOrganization(string name);

        bool AddMember(string organization, Address admin, Address target);
        bool RemoveMember(string organization, Address admin, Address target);
        void MigrateMember(string organization, Address admin, Address source, Address destination);

        bool ContractExists(string name);
        bool ContractDeployed(string name);

        bool ArchiveExists(Hash hash);
        IArchive GetArchive(Hash hash);
        bool DeleteArchive(Hash hash);

        bool ChainExists(string name);
        IChain GetChainByAddress(Address address);
        IChain GetChainByName(string name);
        int GetIndexOfChain(string name);

        IChain GetChainParent(string name);

        void Log(string description);
        void Throw(string description);
        void Expect(bool condition, string description);
        void Notify(EventKind kind, Address address, byte[] data);
        VMObject CallContext(string contextName, string methodName, params object[] args);

        Address LookUpName(string name);
        bool HasAddressScript(Address from);
        byte[] GetAddressScript(Address from);
        string GetAddressName(Address from);

        Event[] GetTransactionEvents(Hash transactionHash);
        Hash[] GetTransactionHashesForAddress(Address address);

        ValidatorEntry GetValidatorByIndex(int index);
        ValidatorEntry[] GetValidators();
        bool IsPrimaryValidator(Address address);
        bool IsSecondaryValidator(Address address);
        int GetPrimaryValidatorCount();
        int GetSecondaryValidatorCount();
        bool IsKnownValidator(Address address);

        bool IsStakeMaster(Address address); // TODO remove
        BigInteger GetStake(Address address);

        BigInteger GetTokenPrice(string symbol);
        BigInteger GetGovernanceValue(string name);

        BigInteger GenerateUID();
        BigInteger GenerateRandomNumber();

        bool InvokeTrigger(byte[] script, string triggerName, params object[] args);

        bool IsWitness(Address address);

        BigInteger GetBalance(string symbol, Address address);
        BigInteger[] GetOwnerships(string symbol, Address address);
        BigInteger GetTokenSupply(string symbol);

        void CreateToken(Address from, string symbol, string name, string platform, Hash hash, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script);
        void CreateChain(Address creator, string organization, string name, string parentChain);
        void CreateFeed(Address owner, string name, FeedMode mode);
        void CreateArchive(Address from, MerkleTree merkleTree, BigInteger size, ArchiveFlags flags, byte[] key);

        BigInteger CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol);

        bool IsAddressOfParentChain(Address address);
        bool IsAddressOfChildChain(Address address);

        bool IsPlatformAddress(Address address);

        void MintTokens(string symbol, Address from, Address target, BigInteger amount);
        void BurnTokens(string symbol, Address from, BigInteger amount);
        void TransferTokens(string symbol, Address source, Address destination, BigInteger amount);
        void SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value, byte[] rom, byte[] ram);

        BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram);
        void BurnToken(string symbol, Address from, BigInteger tokenID);
        void TransferToken(string symbol, Address source, Address destination, BigInteger tokenID);
        void WriteToken(string tokenSymbol, BigInteger tokenID, byte[] ram);
        TokenContent ReadToken(string tokenSymbol, BigInteger tokenID);

        byte[] ReadOracle(string URL);
    }
}
