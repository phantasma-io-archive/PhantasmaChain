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
        public BalanceResult[] balances;
    }

    public struct ChainResult : IAPIResult
    {
        public string name;
        public string address;
        public string parentAddress;
        public uint height;
    }

    public struct AppResult : IAPIResult
    {
        public string description;
        public string icon;
        public string id;
        public string title;
        public string url;
    }

    public class EventResult : IAPIResult
    {
        public string address;
        public string data;
        public string kind;
    }

    public class TransactionResult : IAPIResult
    {
        public string txid;

        public string chainAddress;

        public string chainName;

        public uint timestamp;

        public uint blockHeight;

        public string script;

        public EventResult[] events;
    }

    public struct AccountTransactionsResult : IAPIResult
    {
        public string address;
        public uint amount;
        public TransactionResult[] txs;
    }

    public class BlockResult : IAPIResult
    {
        public string hash;
        public string previousHash;
        public uint timestamp;
        public uint height;
        public string chainAddress;
        public uint nonce;
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
