// notes: Keep the structs here simple only using primitive C# types or arrays
namespace Phantasma.API
{
    public interface IAPIResult
    {
    }

    public struct ErrorResult : IAPIResult
    {
        public string error;
    }

    public struct SingleResult : IAPIResult
    {
        public object value;
    }

    public struct ArrayResult : IAPIResult
    {
        public object[] values;
    }

    public struct BalanceResult : IAPIResult
    {
        public string chain;
        public string amount;
        public string symbol;
        public uint decimals;
        public string[] ids;
    }

    public struct PlatformResult : IAPIResult
    {
        public string platform;
        public string address;
        public string interop;
        public string fuel;
        public string[] tokens;
    }

    public struct InteropResult: IAPIResult
    {
        public string platform;
        public string address;
        public string interop;
    }

    public struct AccountResult : IAPIResult
    {
        public string address;
        public string name;

        [APIDescription("Amount of staked SOUL")]
        public string stake;

        [APIDescription("Amount of available KCAL for relay channel")]
        public string relay;

        [APIDescription("List of token balances")]
        public BalanceResult[] balances;
    }

    public struct ChainResult : IAPIResult
    {
        public string name;
        public string address;

        [APIDescription("Address of parent chain")]
        public string parentAddress;

        [APIDescription("Current chain height")]
        public uint height;

        [APIDescription("Contracts deployed in the chain")]
        public string[] contracts;
    }

    public struct AppResult : IAPIResult
    {
        public string id;
        public string title;
        public string url;

        [APIDescription("Description of app")]
        public string description;

        [APIDescription("Storage hash of the app icon")]
        public string icon;
    }

    public struct EventResult : IAPIResult
    {
        public string address;
        public string kind;

        [APIDescription("Data in hexadecimal format, content depends on the event kind")]
        public string data;
    }

    public struct TransactionResult : IAPIResult
    {
        [APIDescription("Hash of the transaction")]
        public string hash;

        [APIDescription("Transaction chain address")]
        public string chainAddress;

        [APIDescription("Block time")]
        public uint timestamp;

        [APIDescription("Number of confirmations for the transaction")]
        public int confirmations;

        [APIDescription("Block height at which the transaction was accepted")]
        public int blockHeight;

        [APIDescription("Hash of the block")]
        public string blockHash;

        [APIDescription("Script content of the transaction, in hexadecimal format")]
        public string script;

        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events;

        [APIDescription("Result of the transaction, if any. Serialized, in hexadecimal format")]
        public string result;

        [APIDescription("Fee of the transaction, in KCAL, fixed point")]
        public string fee;
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

        [APIDescription("Platform of token")]
        public string platform;

        [APIDescription("Hash of token")]
        public string hash;

        public string flags;
    }

    public struct TokenDataResult : IAPIResult
    {
        [APIDescription("ID of token")]
        public string ID;

        [APIDescription("Chain where currently is stored")]
        public string chainName;

        [APIDescription("Address who currently owns the token")]
        public string ownerAddress;

        [APIDescription("Writable data of token, hex encoded")]
        public string ram;

        [APIDescription("Read-only data of token, hex encoded")]
        public string rom;

        [APIDescription("True if is being sold in market")]
        public bool forSale;
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

        public string rom;
        public string ram;
    }

    public struct OracleResult : IAPIResult
    {
        [APIDescription("URL that was read by the oracle")]
        public string url;

        [APIDescription("Byte array content read by the oracle, encoded as hex string")]
        public string content;
    }

    public struct ScriptResult : IAPIResult
    {
        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events;

        [APIDescription("Result of the transaction, if any. Serialized, in hexadecimal format")]
        public string result;

        [APIDescription("List of oracle reads that were triggered in the transaction")]
        public OracleResult[] oracles;
    }

    public struct ArchiveResult : IAPIResult
    {
        [APIDescription("Archive hash")]
        public string hash;

        [APIDescription("Size of archive in bytes")]
        public uint size;

        [APIDescription("Archive flags")]
        public string flags;

        [APIDescription("Encryption public key")]
        public string key;

        [APIDescription("Number of blocks")]
        public int blockCount;

        [APIDescription("Metadata")]
        public string[] metadata;
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

    public struct ABIContractResult : IAPIResult
    {
        [APIDescription("Name of contract")]
        public string name;

        [APIDescription("List of methods")]
        public ABIMethodResult[] methods;
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
        public string sourceHash;
        public string sourcePlatform;
        public string sourceAddress;
        public string destinationHash;
        public string destinationPlatform;
        public string destinationAddress;
        public string symbol;
        public uint decimals;
        public string amount;
        public string status;
    }
}
