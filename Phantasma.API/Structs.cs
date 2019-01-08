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

        [APIResultField("List of token balances")]
        public BalanceResult[] balances;
    }

    public struct ChainResult : IAPIResult
    {
        public string name;
        public string address;

        [APIResultField("Address of parent chain")]
        public string parentAddress;

        [APIResultField("Current chain height")]
        public uint height;
    }

    public struct AppResult : IAPIResult
    {
        public string id;
        public string title;
        public string url;

        [APIResultField("Description of app")]
        public string description;

        [APIResultField("Storage hash of the app icon")]
        public string icon;
    }

    public class EventResult : IAPIResult
    {
        public string address;
        public string kind;

        [APIResultField("Data in hexadecimal format, content depends on the event kind")]
        public string data;
    }

    public class TransactionResult : IAPIResult
    {
        public string hash;

        [APIResultField("Transaction chain address")]
        public string chainAddress;

        public uint timestamp;

        public uint blockHeight;

        [APIResultField("Script content of the transaction, in hexadecimal format")]
        public string script;

        [APIResultField("List of events that triggered in the transaction")]
        public EventResult[] events;
    }

    public struct AccountTransactionsResult : IAPIResult
    {
        public string address;
        public uint amount;

        [APIResultField("List of transactions")]
        public TransactionResult[] txs;
    }

    public class BlockResult : IAPIResult
    {
        public string hash;

        [APIResultField("Hash of previous block")]
        public string previousHash;

        public uint timestamp;
        public uint height;

        [APIResultField("Address of chain where the block belongs")]
        public string chainAddress;

        [APIResultField("Custom data choosen by the block miner, in hexadecimal format")]
        public string payload;

        public TransactionResult[] txs;
        public string minerAddress;
        public string reward;
    }

    public class RootChainResult : IAPIResult
    {
        public string name;
        public string address;
        public uint height;
    }

    public class TokenResult : IAPIResult
    {
        public string symbol;

        public string name;

        public int decimals;

        public bool isFungible;

        public string currentSupply;

        public string maxSupply;

        public string owner;

        //public string[] Flags; TODO
    }

    public class TxConfirmationResult : IAPIResult
    {
        public string hash;

        public string chain;

        public int confirmations;

        public uint height;

        public bool isConfirmed => confirmations >= 1;
    }
}
