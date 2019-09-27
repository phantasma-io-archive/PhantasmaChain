using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using Phantasma.VM;

namespace Phantasma.Domain
{
    public interface IRuntime
    {
        INexus Nexus { get; }
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

        ITransaction GetTransaction(Hash hash);

        IToken GetToken(string symbol);
        IFeed GetFeed(string name);
        IPlatform GetPlatform(string name);
        IContract GetContract(string name);

        bool TokenExists(string symbol);
        bool FeedExists(string name);
        bool PlatformExists(string name);

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

        Event[] GetTransactionEvents(Hash transactionHash);

        Address GetValidatorForBlock(Hash blockHash);
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
        BigInteger GetTokenQuote(string baseSymbol, string quoteSymbol, BigInteger amount);
        BigInteger GetGovernanceValue(string name);

        BigInteger GenerateUID();
        BigInteger GenerateRandomNumber();

        bool InvokeTrigger(byte[] script, string triggerName, params object[] args);

        bool IsWitness(Address address);

        BigInteger GetBalance(string symbol, Address address);
        BigInteger[] GetOwnerships(string symbol, Address address);
        BigInteger GetTokenSupply(string symbol);

        bool CreateToken(string symbol, string name, string platform, Hash hash, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script);
        bool CreateChain(Address owner, string name, string parentChain);
        bool CreateFeed(Address owner, string name, FeedMode mode);
        bool CreatePlatform(Address address, string name, string fuelSymbol);
        bool CreateArchive(MerkleTree merkleTree, BigInteger size, ArchiveFlags flags, byte[] key);

        bool IsAddressOfParentChain(Address address);
        bool IsAddressOfChildChain(Address address);

        bool IsPlatformAddress(Address address);

        bool MintTokens(string symbol, Address from, BigInteger amount);
        bool BurnTokens(string symbol, Address from, BigInteger amount);
        bool TransferTokens(string symbol, Address source, Address destination, BigInteger amount);
        bool SendTokens(Address targetChainAddress, Address from, Address to, string symbol, BigInteger amount);

        BigInteger MintToken(string symbol, Address target, byte[] rom, byte[] ram);
        bool TransferToken(string symbol, Address source, Address destination, BigInteger tokenID);
        bool SendToken(Address targetChainAddress, Address from, Address to, string symbol, BigInteger tokenID);
        bool BurnToken(string symbol, Address from, BigInteger tokenID);
        bool WriteToken(string tokenSymbol, BigInteger tokenID, byte[] ram);
        TokenContent ReadToken(string tokenSymbol, BigInteger tokenID);

        byte[] ReadOracle(string URL);
    }
}
