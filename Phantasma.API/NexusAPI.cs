using System.Linq;
using Phantasma.Blockchain;
using LunarLabs.Parser;
using Phantasma.Blockchain.Plugins;
using Phantasma.Cryptography;

namespace Phantasma.API
{
    public class NexusAPI
    {
        public Nexus Nexus { get; private set; }

        public NexusAPI(Nexus nexus)
        {
            this.Nexus = nexus;
        }

        public DataNode GetAccount(Address address)
        {
            var result = DataNode.CreateObject();

            result.AddField("address", address.Text);

            var tokenNode = DataNode.CreateArray("tokens");
            result.AddNode(tokenNode);

            foreach (var token in Nexus.Tokens)
            {
                DataNode chainNode = null;

                foreach (var chain in Nexus.Chains)
                {
                    var balance = chain.GetTokenBalance(token, address);
                    if (balance > 0)
                    {
                        if (chainNode == null)
                        {
                            chainNode = DataNode.CreateArray("chains");
                        }

                        var balanceNode = DataNode.CreateObject();
                        chainNode.AddNode(balanceNode);

                        balanceNode.AddField("chain", chain.Name);
                        balanceNode.AddField("balance", balance);
                    }
                }

                if (chainNode != null)
                {
                    var entryNode = DataNode.CreateObject();
                    tokenNode.AddNode(entryNode);
                    entryNode.AddField("symbol", token.Symbol);
                    entryNode.AddField("name", token.Name);
                    entryNode.AddNode(chainNode);
                }
            }

            return result;
        }

        public DataNode GetBlock(Hash hash)
        {
            var result = DataNode.CreateObject();

            result.AddField("hash", hash.ToString());
            foreach (var chain in Nexus.Chains)
            {
                var block = chain.FindBlockByHash(hash);
                if (block != null)
                {

                    result.AddField("timestamp", block.Timestamp);
                    result.AddField("height", block.Height);
                    result.AddField("chainAddress", block.Chain.Address);
                    result.AddField("chainName", block.Chain.Name);
                    result.AddField("previousHash", block.PreviousHash);
                    result.AddField("nonce", block.Nonce);
                    result.AddField("minerAddress", block.MinerAddress.Text);
                }
            }

            return result;
        }

        public DataNode GetAddressTransactions(Address address, int amount)
        {
            var result = DataNode.CreateObject();
            var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
            var txsNode = DataNode.CreateArray("txs");
            result.AddField("address", address.Text);
            result.AddField("amount", amount);
            result.AddNode(txsNode);
            var txs = plugin?.GetAddressTransactions(address).OrderByDescending(p => p.Block.Timestamp.Value).Take(amount);
            if (txs != null)
            {
                foreach (var transaction in txs)
                {
                    var entryNode = DataNode.CreateObject();
                    entryNode.AddField("txid", transaction.Hash.ToString());
                    entryNode.AddField("chainAddress", transaction.Block.Chain.Address);
                    entryNode.AddField("chainName", transaction.Block.Chain.Name);
                    entryNode.AddField("timestamp", transaction.Block.Timestamp.Value);
                    entryNode.AddField("blockHeight", transaction.Block.Height);
                    entryNode.AddField("gasLimit", transaction.GasLimit.ToString());
                    entryNode.AddField("gasPrice", transaction.GasPrice.ToString());
                    txsNode.AddNode(entryNode);
                }
            }

            return result;
        }
        /*
       public DataNode GetTransaction(Hash hash)
       {

       }

       public DataNode GetChains()
       {

       }

       public DataNode GetTokens()
       {

       }

       public DataNode GetApps()
       {

       }*/
    }
}
