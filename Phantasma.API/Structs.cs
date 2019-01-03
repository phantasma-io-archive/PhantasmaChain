using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Tokens;
using System.Collections.Generic;

namespace Phantasma.API
{
    public struct AccountResult
    {
        public string Address { get; set; }
        public string Name { get; set; }
        public List<BalanceSheetResult> Tokens { get; set; };
    }

    public struct BalanceSheetResult
    {
        public string ChainName { get; set; }
        public string Amount { get; set; }
        public string Symbol { get; set; }
        public List<string> Ids { get; set; }
    }

    public struct AppResult
    {
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
    }

    public struct AppListResult
    {
        public List<AppResult> Apps { get; set; }
    }

    public struct AccountTransactionsResult
    {
        public string Address { get; set; }
        public long Amount { get; set; }
        public List<TransactionDto> Txs { get; set; };
    }

    public class BlockDto
    {
        public string Hash { get; set; }
        public string PreviousHash { get; set; }
        public long Timestamp { get; set; }
        public long Height { get; set; }
        public string ChainAddress { get; set; }
        public long Nonce { get; set; }
        public string Payload { get; set; }
        public List<TransactionResult> Txs { get; set; }
        public string MinerAddress { get; set; }
        public decimal Reward { get; set; }
    }

    public class EventResult
    {
        public string EventAddress { get; set; }
        public string Data { get; set; }
        public EventKind Kind { get; set; }
    }

    public class RootChainResult
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public int Height { get; set; }
    }

    public class SendRawTxResult
    {
        public string Hash { get; set; }
        public string Error { get; set; }
    }

    public class TokenResult
    {
        public string Symbol { get; set; }

        public string Name { get; set; }

        public int Decimals { get; set; }

        public bool IsFungible { get; set; }

        public string CurrentSupply { get; set; }

        public string MaxSupply { get; set; }

        public string Owner { get; set; }

        public TokenFlags Flags { get; set; }
    }

    public class TokenListResult
    {
        public List<TokenResult> Tokens { get; set; }
    }

    public class TxConfirmationResult
    {
        public string Hash { get; set; }

        public int Confirmations { get; set; }

        public int Height { get; set; }

        public string Error { get; set; }

        public bool IsConfirmed => Confirmations >= 1;
    }

    public class TransactionResult
    {
        public string Txid { get; set; }

        public string ChainAddress { get; set; }

        public string ChainName { get; set; }

        public uint Timestamp { get; set; }

        public uint BlockHeight { get; set; }

        public decimal GasLimit { get; set; }

        public decimal GasPrice { get; set; }

        public string Script { get; set; }

        public List<EventResult> Events { get; set; }
    }
}
