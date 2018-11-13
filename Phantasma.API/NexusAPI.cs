using System.Linq;
using Phantasma.Blockchain;
using LunarLabs.Parser;
using Phantasma.Blockchain.Plugins;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core.Types;

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

        private DataNode GetBlock(Block block)
        {
            var result = DataNode.CreateObject();

            result.AddField("hash", block.Hash.ToString());
            result.AddField("timestamp", block.Timestamp);
            result.AddField("height", block.Height);
            result.AddField("chainAddress", block.Chain.Address);
            result.AddField("chainName", block.Chain.Name);
            result.AddField("previousHash", block.PreviousHash);
            result.AddField("nonce", block.Nonce);
            result.AddField("minerAddress", block.MinerAddress.Text);

            return result;
        }

        public DataNode GetBlockByHash(Hash hash)
        {
            foreach (var chain in Nexus.Chains)
            {
                var block = chain.FindBlockByHash(hash);
                if (block != null)
                {
                    return GetBlock(block);
                }
            }

            return null;
        }

        public DataNode GetBlockByHeight(string chainName, uint height)
        {
            var chain = Nexus.FindChainByName(chainName);
            var block = chain.FindBlockByHeight(height);
            if (block != null)
            {
                return GetBlock(block);
            }

            return null;
        }

        public DataNode GetAddressTransactions(Address address, int amountTx)
        {
            var result = DataNode.CreateObject();
            var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
            var txsNode = DataNode.CreateArray("txs");
            var eventsNode = DataNode.CreateArray("events");
            result.AddField("address", address.Text);
            result.AddField("amount", amountTx);
            result.AddNode(txsNode);
            var txs = plugin?.GetAddressTransactions(address).OrderByDescending(p => p.Block.Timestamp.Value).Take(amountTx);
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
                    entryNode.AddField("script", ByteArrayToHex(transaction.Script));
                    
                    foreach (var evt in transaction.Events)
                    {
                        var eventNode = DataNode.CreateObject();
                        eventNode.AddField("eventAddress", evt.Address);
                        eventNode.AddField("data", ByteArrayToHex(evt.Data));
                        eventNode.AddField("evtKind", evt.Kind);
                        eventsNode.AddNode(eventNode);
                    }

                    entryNode.AddNode(eventsNode);
                    txsNode.AddNode(entryNode);
                }
            }

            return result;
        }

        public bool SendRawTransaction(string chainName, string signedTransaction)
        {
            var bytes = Base16.Decode(signedTransaction);
            var tx = Transaction.Unserialize(bytes);

            var chain = Nexus.FindChainByName(chainName);

            // TODO this should go to a mempool instead
            var miner = KeyPair.Generate();
            var block = new Block(chain, miner.Address, Timestamp.Now, new Transaction[] { tx }, chain.LastBlock);
            return true;
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


        //todo remove this, just for testing
        private static readonly uint[] _lookup32 = CreateLookup32();

        private static uint[] CreateLookup32()
        {
            var result = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                string s = i.ToString("X2");
                result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
            }
            return result;
        }

        private static string ByteArrayToHex(byte[] bytes)
        {
            var lookup32 = _lookup32;
            var result = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = lookup32[bytes[i]];
                result[2 * i] = (char)val;
                result[2 * i + 1] = (char)(val >> 16);
            }
            return new string(result);
        }
    }
}
