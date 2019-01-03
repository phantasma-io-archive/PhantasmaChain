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

    public struct AccountResult : IAPIResult
    {
        public string Address;
        public string Name;
        public BalanceSheetResult[] Balances;
    }

    public struct BalanceSheetResult : IAPIResult
    {
        public string Chain;
        public string Amount;
        public string Symbol;
        public string[] Ids;
    }

    public struct ChainResult
    {
        public string Name;
        public string Address;
        public string ParentAddress;
        public uint Height;
        public ChainResult[] Children;
    }

    public struct AppResult : IAPIResult
    {
        public string Description;
        public string Icon;
        public string Id;
        public string Title;
        public string Url;
    }

    public struct AppListResult : IAPIResult
    {
        public AppResult[] Apps;
    }

    public struct AccountTransactionsResult : IAPIResult
    {
        public string Address;
        public long Amount;
        public TransactionResult[] Txs;
    }

    public class BlockResult : IAPIResult
    {
        public string Hash;
        public string PreviousHash;
        public long Timestamp;
        public long Height;
        public string ChainAddress;
        public long Nonce;
        public string Payload;
        public TransactionResult[] Txs;
        public string MinerAddress;
        public decimal Reward;
    }

    public class EventResult : IAPIResult
    {
        public string Address;
        public string Data;
        public string Kind;
    }

    public class RootChainResult : IAPIResult
    {
        public string Name;
        public string Address;
        public uint Height;
    }

    public class TokenResult : IAPIResult
    {
        public string Symbol;

        public string Name;

        public int Decimals;

        public bool IsFungible;

        public string CurrentSupply;

        public string MaxSupply;

        public string Owner;

        //public string[] Flags; TODO
    }

    public class TokenListResult : IAPIResult
    {
        public TokenResult[] Tokens;
    }

    public class TxConfirmationResult : IAPIResult
    {
        public string Hash;

        public string Chain;

        public int Confirmations;

        public uint Height;

        public bool IsConfirmed => Confirmations >= 1;
    }

    public class TransactionResult : IAPIResult
    {
        public string Txid;

        public string ChainAddress;

        public string ChainName;

        public uint Timestamp;

        public uint BlockHeight;

        public decimal GasLimit;

        public decimal GasPrice;

        public string Script;

        public EventResult[] Events;
    }
}
