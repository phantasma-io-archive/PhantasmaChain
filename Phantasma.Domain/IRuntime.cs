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
        int TransactionIndex { get; }

        ITask CurrentTask { get; }

        ExecutionContext CurrentContext { get; }
        ExecutionContext PreviousContext { get; }

        Address GasTarget { get; }
        BigInteger UsedGas { get; }
        BigInteger GasPrice { get; }

        IBlock GetBlockByHash(Hash hash);
        IBlock GetBlockByHeight(BigInteger height);

        Address GetValidator(Timestamp time);

        bool HasGenesis { get; }
        string NexusName { get; }
        uint ProtocolVersion { get; }
        Address GenesisAddress { get; }
        Hash GenesisHash { get; }
        Timestamp GetGenesisTime();

        ITransaction GetTransaction(Hash hash);

        string[] GetTokens();
        string[] GetChains();
        string[] GetPlatforms();
        string[] GetFeeds();
        string[] GetOrganizations();
        
        // returns contracts deployed on current chain
        IContract[] GetContracts();


        IToken GetToken(string symbol);
        Hash GetTokenPlatformHash(string symbol, IPlatform platform);
        IFeed GetFeed(string name);
        IContract GetContract(string name);
        Address GetContractOwner(Address address);

        IPlatform GetPlatformByName(string name);
        IPlatform GetPlatformByIndex(int index);

        bool TokenExists(string symbol);        
        bool NFTExists(string symbol, BigInteger tokenID);
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

        bool AddOwnerToArchive(Hash hash, Address address);

        bool RemoveOwnerFromArchive(Hash hash, Address address);

        bool WriteArchive(IArchive archive, int blockIndex, byte[] data);

        bool ChainExists(string name);
        IChain GetChainByAddress(Address address);
        IChain GetChainByName(string name);
        int GetIndexOfChain(string name);

        IChain GetChainParent(string name);

        void Log(string description);
        void Throw(string description);
        void Expect(bool condition, string description);
        void Notify(EventKind kind, Address address, byte[] data);
        VMObject CallContext(string contextName, uint jumpOffset, string methodName, params object[] args);

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

        TriggerResult InvokeTrigger(bool allowThrow, byte[] script, string contextName, ContractInterface abi, string triggerName, params object[] args);

        bool IsWitness(Address address);

        BigInteger GetBalance(string symbol, Address address);
        BigInteger[] GetOwnerships(string symbol, Address address);
        BigInteger GetTokenSupply(string symbol);

        void CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi);
        void SetPlatformTokenHash(string symbol, string platform, Hash hash);
        void CreateChain(Address creator, string organization, string name, string parentChain);
        void CreateFeed(Address owner, string name, FeedMode mode);
        IArchive CreateArchive(MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption);

        BigInteger CreatePlatform(Address from, string name, string externalAddress, Address interopAddress, string fuelSymbol);

        bool IsAddressOfParentChain(Address address);
        bool IsAddressOfChildChain(Address address);

        bool IsPlatformAddress(Address address);
        void RegisterPlatformAddress(string platform, Address localAddress, string externalAddress);

        void MintTokens(string symbol, Address from, Address target, BigInteger amount);
        void BurnTokens(string symbol, Address from, BigInteger amount);
        void TransferTokens(string symbol, Address source, Address destination, BigInteger amount);
        void SwapTokens(string sourceChain, Address from, string targetChain, Address to, string symbol, BigInteger value);

        BigInteger MintToken(string symbol, Address from, Address target, byte[] rom, byte[] ram, BigInteger seriesID);
        void BurnToken(string symbol, Address from, BigInteger tokenID);
        void InfuseToken(string symbol, Address from, BigInteger tokenID, string infuseSymbol, BigInteger value);
        void TransferToken(string symbol, Address source, Address destination, BigInteger tokenID);
        void WriteToken(Address from, string tokenSymbol, BigInteger tokenID, byte[] ram);
        TokenContent ReadToken(string tokenSymbol, BigInteger tokenID);
        ITokenSeries CreateTokenSeries(string tokenSymbol, Address from, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi);
        ITokenSeries GetTokenSeries(string symbol, BigInteger seriesID);

        byte[] ReadOracle(string URL);

        ITask StartTask(Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit);
        void StopTask(ITask task);
        ITask GetTask(BigInteger taskID);
    }
}
