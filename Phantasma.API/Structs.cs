// notes: Keep the structs here simple only using primitive C# types or arrays
using Phantasma.Domain;

namespace Phantasma.API
{
    public struct BalanceResult : IAPIResult
    {
        public string chain;
        public string amount;
        public string symbol;
        public uint decimals;
        public string[] ids;
    }

    public struct InteropResult: IAPIResult
    {
        public string local;
        public string external;
    }

    public struct PlatformResult : IAPIResult
    {
        public string platform;
        public string chain;
        public string fuel;
        public string[] tokens;
        public InteropResult[] interop;
    }

    public struct GovernanceResult : IAPIResult
    {
        public string name;
        public string value;
    }

    public struct OrganizationResult : IAPIResult
    {
        public string id;
        public string name;
        public string[] members;
    }

    // TODO add APIDescription tags
    public struct CrowdsaleResult : IAPIResult
    {
        public string hash;
        public string name;
        public string creator;
        public string flags;
        public uint startDate;
        public uint endDate;
        public string sellSymbol;
        public string receiveSymbol;
        public uint price;
        public string globalSoftCap;
        public string globalHardCap;
        public string userSoftCap;
        public string userHardCap;
    }

    public struct NexusResult : IAPIResult
    {
        [APIDescription("Name of the nexus")]
        public string name;

        [APIDescription("Network protocol version")]
        public uint protocol;

        [APIDescription("List of platforms")]
        public PlatformResult[] platforms;

        [APIDescription("List of tokens")]
        public TokenResult[] tokens;

        [APIDescription("List of chains")]
        public ChainResult[] chains;

        [APIDescription("List of governance values")]
        public GovernanceResult[] governance;

        [APIDescription("List of organizations")]
        public string[] organizations;
    }

    public struct StakeResult : IAPIResult
    {
        [APIDescription("Amount of staked SOUL")]
        public string amount;

        [APIDescription("Time of last stake")]
        public uint time;

        [APIDescription("Amount of claimable KCAL")]
        public string unclaimed;
    }

    public struct StorageResult : IAPIResult
    {
        [APIDescription("Amount of available storage bytes")]
        public uint available;

        [APIDescription("Amount of used storage bytes")]
        public uint used;

        [APIDescription("Avatar data")]
        public string avatar;

        [APIDescription("List of stored files")]
        public ArchiveResult[] archives;
    }

    public struct AccountResult : IAPIResult
    {
        public string address;
        public string name;

        [APIDescription("Info about staking if available")]
        public StakeResult stakes;

        public string stake; //Deprecated
        public string unclaimed;//Deprecated

        [APIDescription("Amount of available KCAL for relay channel")]
        public string relay;

        [APIDescription("Validator role")]
        public string validator;

        [APIDescription("Info about storage if available")]
        public StorageResult storage;

        public BalanceResult[] balances;

        public string[] txs;
    }

    public struct LeaderboardRowResult : IAPIResult
    {
        public string address;
        public string value;
    }

    public struct LeaderboardResult : IAPIResult
    {
        public string name;
        public LeaderboardRowResult[] rows;
    }

    public struct DappResult : IAPIResult
    {
        public string name;
        public string address;
        public string chain;
    }

    public struct ChainResult : IAPIResult
    {
        public string name;
        public string address;

        [APIDescription("Name of parent chain")]
        public string parent;

        [APIDescription("Current chain height")]
        public uint height;

        [APIDescription("Chain organization")]
        public string organization;

        [APIDescription("Contracts deployed in the chain")]
        public string[] contracts;

        [APIDescription("Dapps deployed in the chain")]
        public string[] dapps;
    }

    public struct EventResult : IAPIResult
    {
        public string address;
        public string contract;
        public string kind;

        [APIDescription("Data in hexadecimal format, content depends on the event kind")]
        public string data;
    }

    public struct OracleResult : IAPIResult
    {
        [APIDescription("URL that was read by the oracle")]
        public string url;

        [APIDescription("Byte array content read by the oracle, encoded as hex string")]
        public string content;
    }

    public struct SignatureResult: IAPIResult
    {
        [APIDescription("Kind of signature")]
        public string Kind;

        [APIDescription("Byte array containing signature data, encoded as hex string")]
        public string Data;
    }

    public struct TransactionResult : IAPIResult
    {
        [APIDescription("Hash of the transaction")]
        public string hash;

        [APIDescription("Transaction chain address")]
        public string chainAddress;

        [APIDescription("Block time")]
        public uint timestamp;

        [APIDescription("Block height at which the transaction was accepted")]
        public int blockHeight;

        [APIDescription("Hash of the block")]
        public string blockHash;

        [APIDescription("Script content of the transaction, in hexadecimal format")]
        public string script;

        [APIDescription("Payload content of the transaction, in hexadecimal format")]
        public string payload;

        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events;

        [APIDescription("Result of the transaction, if any. Serialized, in hexadecimal format")]
        public string result;

        [APIDescription("Fee of the transaction, in KCAL, fixed point")]
        public string fee;

        [APIDescription("List of signatures that signed the transaction")]
        public SignatureResult[] signatures;

        [APIDescription("Expiration time of the transaction")]
        public uint expiration;
    }

    public struct AccountTransactionsResult : IAPIResult
    {
        public string address;

        [APIDescription("List of transactions")]
        public TransactionResult[] txs;
    }

    public struct PaginatedResult : IAPIResult
    {
        public uint page;
        public uint pageSize;
        public uint total;
        public uint totalPages;

        public IAPIResult result;
    }

    public struct BlockResult : IAPIResult
    {
        public string hash;

        [APIDescription("Hash of previous block")]
        public string previousHash;

        public uint timestamp;

        // TODO support bigint here
        public uint height;

        [APIDescription("Address of chain where the block belongs")]
        public string chainAddress;

        [APIDescription("Network protocol version")]
        public uint protocol;

        [APIDescription("List of transactions in block")]
        public TransactionResult[] txs;

        [APIDescription("Address of validator who minted the block")]
        public string validatorAddress;

        [APIDescription("Amount of KCAL rewarded by this fees in this block")]
        public string reward;

        [APIDescription("Block events")]
        public EventResult[] events;

        [APIDescription("Block oracles")]
        public OracleResult[] oracles;
    }

    public struct TokenExternalResult: IAPIResult
    {
        [APIDescription("Platform name")]
        public string platform;

        [APIDescription("External hash")]
        public string hash;
    }

    public struct TokenResult : IAPIResult
    {
        [APIDescription("Ticker symbol for the token")]
        public string symbol;

        public string name;

        [APIDescription("Amount of decimals when converting from fixed point format to decimal format")]
        public int decimals;

        [APIDescription("Amount of minted tokens")]
        public string currentSupply;

        [APIDescription("Max amount of tokens that can be minted")]
        public string maxSupply;

        [APIDescription("Total amount of burned tokens")]
        public string burnedSupply;

        [APIDescription("Address of token contract")]
        public string address;

        [APIDescription("Owner address")]
        public string owner;

        public string flags;

        [APIDescription("Script attached to token, in hex")]
        public string script;

        [APIDescription("Series info. NFT only")]
        public TokenSeriesResult[] series;

        [APIDescription("External platforms info")]
        public TokenExternalResult[] external;
    }

    public struct TokenSeriesResult : IAPIResult
    {
        public uint seriesID;

        [APIDescription("Current amount of tokens in circulation")]
        public string currentSupply;

        [APIDescription("Maximum possible amount of tokens")]
        public string maxSupply;

        [APIDescription("Total amount of burned tokens")]
        public string burnedSupply;

        public TokenSeriesMode mode;

        public string script;

        [APIDescription("List of methods")]
        public ABIMethodResult[] methods;
    }

    public struct TokenPropertyResult : IAPIResult
    {
        public string Key;
        public string Value;
    }

    public struct TokenDataResult : IAPIResult
    {
        [APIDescription("id of token")]
        public string ID;

        [APIDescription("series id of token")]
        public string series;

        [APIDescription("mint number of token")]
        public string mint;

        [APIDescription("Chain where currently is stored")]
        public string chainName;

        [APIDescription("Address who currently owns the token")]
        public string ownerAddress;

        [APIDescription("Address who minted the token")]
        public string creatorAddress;

        [APIDescription("Writable data of token, hex encoded")]
        public string ram;

        [APIDescription("Read-only data of token, hex encoded")]
        public string rom;

        [APIDescription("Status of nft")]
        public string status;

        public TokenPropertyResult[] infusion;

        public TokenPropertyResult[] properties;
    }

    public struct SendRawTxResult : IAPIResult
    {
        [APIDescription("Transaction hash")]
        public string hash;

        [APIDescription("Error message if transaction did not succeed")]
        public string error;
    }

    public struct AuctionResult : IAPIResult
    {
        [APIDescription("Address of auction creator")]
        public string creatorAddress;

        [APIDescription("Address of auction chain")]
        public string chainAddress;
        public uint startDate;
        public uint endDate;
        public string baseSymbol;
        public string quoteSymbol;
        public string tokenId;
        public string price;
        public string endPrice;
        public string extensionPeriod;
        public string type; 
        public string rom;
        public string ram;
        public string listingFee;
        public string currentWinner;
    }

    public struct ScriptResult : IAPIResult
    {
        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events;

        public string result; // deprecated

        [APIDescription("Results of the transaction, if any. Serialized, in hexadecimal format")]
        public string[] results;

        [APIDescription("List of oracle reads that were triggered in the transaction")]
        public OracleResult[] oracles;
    }

    public struct ArchiveResult : IAPIResult
    {
        [APIDescription("File name")]
        public string name;

        [APIDescription("Archive hash")]
        public string hash;

        [APIDescription("Time of creation")]
        public uint time;

        [APIDescription("Size of archive in bytes")]
        public uint size;

        [APIDescription("Encryption address")]
        public string encryption;

        [APIDescription("Number of blocks")]
        public int blockCount;

        [APIDescription("Missing block indices")]
        public int[] missingBlocks;

        [APIDescription("List of addresses who own the file")]
        public string[] owners;
    }

    public struct ABIParameterResult : IAPIResult
    {
        [APIDescription("Name of method")]
        public string name;

        public string type;
    }

    public struct ABIMethodResult : IAPIResult
    {
        [APIDescription("Name of method")]
        public string name;

        public string returnType;

        [APIDescription("Type of parameters")]
        public ABIParameterResult[] parameters;
    }

    public struct ABIEventResult : IAPIResult
    {
        [APIDescription("Value of event")]
        public int value;

        [APIDescription("Name of event")]
        public string name;

        public string returnType;

        [APIDescription("Description script (base16 encoded)")]
        public string description;
    }

    public struct ContractResult : IAPIResult
    {
        [APIDescription("Name of contract")]
        public string name;

        [APIDescription("Address of contract")]
        public string address;

        [APIDescription("Script bytes, in hex format")]
        public string script;

        [APIDescription("List of methods")]
        public ABIMethodResult[] methods;

        [APIDescription("List of events")]
        public ABIEventResult[] events;
    }

    public struct ChannelResult : IAPIResult
    {
        [APIDescription("Creator of channel")]
        public string creatorAddress;

        [APIDescription("Target of channel")]
        public string targetAddress;

        [APIDescription("Name of channel")]
        public string name;

        [APIDescription("Chain of channel")]
        public string chain;

        [APIDescription("Creation time")]
        public uint creationTime;

        [APIDescription("Token symbol")]
        public string symbol;

        [APIDescription("Fee of messages")]
        public string fee;

        [APIDescription("Estimated balance")]
        public string balance;

        [APIDescription("Channel status")]
        public bool active;

        [APIDescription("Message index")]
        public int index;
    }

    public struct ReceiptResult : IAPIResult
    {
        [APIDescription("Name of nexus")]
        public string nexus;

        [APIDescription("Name of channel")]
        public string channel;

        [APIDescription("Index of message")]
        public string index;

        [APIDescription("Date of message")]
        public uint timestamp;

        [APIDescription("Sender address")]
        public string sender;

        [APIDescription("Receiver address")]
        public string receiver;

        [APIDescription("Script of message, in hex")]
        public string script;
    }

    public struct PeerResult: IAPIResult
    {
        [APIDescription("URL of peer")]
        public string url;

        [APIDescription("Software version of peer")]
        public string version;

        [APIDescription("Features supported by peer")]
        public string flags;

        [APIDescription("Minimum fee required by node")]
        public string fee;

        [APIDescription("Minimum proof of work required by node")]
        public uint pow;
    }

    public struct ValidatorResult : IAPIResult
    {
        [APIDescription("Address of validator")]
        public string address;

        [APIDescription("Either primary or secondary")]
        public string type;
    }

    // TODO document this
    public struct SwapResult : IAPIResult
    {
        public string sourcePlatform;
        public string sourceChain;
        public string sourceHash;
        public string sourceAddress;

        public string destinationPlatform;
        public string destinationChain;
        public string destinationHash;
        public string destinationAddress;

        public string symbol;
        public string value;
    }
}
