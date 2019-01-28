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
        public string[] ids;
    }

    public struct AccountResult : IAPIResult
    {
        public string address;
        public string name;

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
        public string hash;

        [APIDescription("Transaction chain address")]
        public string chainAddress;

        public uint timestamp;

        public uint blockHeight;

        [APIDescription("Script content of the transaction, in hexadecimal format")]
        public string script;

        [APIDescription("List of events that triggered in the transaction")]
        public EventResult[] events;

        [APIDescription("Result of the transaction, if any. Serialized, in hexadecimal format")]
        public string result;
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

        public IAPIResult results;
    }

    public struct BlockResult : IAPIResult
    {
        public string hash;

        [APIDescription("Hash of previous block")]
        public string previousHash;

        public uint timestamp;
        public uint height;

        [APIDescription("Address of chain where the block belongs")]
        public string chainAddress;

        [APIDescription("Custom data choosen by the block miner, in hexadecimal format")]
        public string payload;

        [APIDescription("List of transactions in block")]
        public TransactionResult[] txs;

        [APIDescription("Address of validator who minted the block")]
        public string validatorAddress;

        [APIDescription("Amount of SOUL rewarded by this fees in this block")]
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

        [APIDescription("Address that controls minting of tokens")]
        public string ownerAddress;

        public string Flags;
    }

    public struct TokenDataResult : IAPIResult
    {
        [APIDescription("ID of token")]
        public string ID;

        [APIDescription("Address where currently is stored")]
        public string chainAddress;

        [APIDescription("Address who currently owns the token")]
        public string ownerAddress;

        [APIDescription("Writable data of token, hex encoded")]
        public string ram;

        [APIDescription("Read-only data of token, hex encoded")]
        public string rom;
    }

    public struct TxConfirmationResult : IAPIResult
    {
        [APIDescription("Hash of transaction")]
        public string hash;

        [APIDescription("Address that controls minting of tokens")]
        public string chainAddress;

        [APIDescription("Number of confirmations for the transaction")]
        public int confirmations;

        [APIDescription("Block height at which the transaction was accepted")]
        public uint height;
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

        public uint startDate;
        public uint endDate;

        public string baseSymbol;
        public string quoteSymbol;
        public string tokenId;
        public string price;
    }
}
