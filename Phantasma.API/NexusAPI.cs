using System.Globalization;
using System.Linq;
using Phantasma.Blockchain;
using LunarLabs.Parser;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Plugins;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;

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

        public DataNode GetBlockByHeight(uint height, string chainName)
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

                    string description = null;

                    Token senderToken = null;
                    Address senderAddress = Address.Null;

                    Token receiverToken = null;
                    Address receiverAddress = Address.Null;

                    BigInteger amount = 0;
                    foreach (var evt in transaction.Events)//todo move this
                    {
                        switch (evt.Kind)
                        {
                            case EventKind.TokenSend:
                                {
                                    var data = evt.GetContent<TokenEventData>();
                                    amount = data.value;
                                    senderAddress = evt.Address;
                                    senderToken = Nexus.FindTokenBySymbol(data.symbol);
                                }
                                break;

                            case EventKind.TokenReceive:
                                {
                                    var data = evt.GetContent<TokenEventData>();
                                    amount = data.value;
                                    receiverAddress = evt.Address;
                                    receiverToken = Nexus.FindTokenBySymbol(data.symbol);
                                }
                                break;

                            case EventKind.AddressRegister:
                                {
                                    var name = evt.GetContent<string>();
                                    description = $"{evt.Address} registered the name '{name}'";
                                }
                                break;

                            case EventKind.FriendAdd:
                                {
                                    var address2 = evt.GetContent<Address>();
                                    description = $"{evt.Address} added '{address2} to friends.'";
                                }
                                break;

                            case EventKind.FriendRemove:
                                {
                                    var address2 = evt.GetContent<Address>();
                                    description = $"{evt.Address} removed '{address2} from friends.'";
                                }
                                break;
                        }
                    }

                    if (description == null)
                    {
                        if (amount > 0 && senderAddress != Address.Null && receiverAddress != Address.Null && senderToken != null && senderToken == receiverToken)
                        {
                            var amountDecimal = TokenUtils.ToDecimal(amount, senderToken.Decimals);
                            description = $"{amountDecimal} {senderToken.Symbol} sent from {senderAddress.Text} to {receiverAddress.Text}";
                            entryNode.AddField("amount", amountDecimal.ToString(CultureInfo.InvariantCulture));
                            entryNode.AddField("asset", senderToken.Symbol);
                            entryNode.AddField("addressTo", senderAddress.Text);
                            entryNode.AddField("addressFrom", receiverAddress.Text);
                        }
                        else
                        {
                            description = "Custom transaction";
                        }
                    }

                    entryNode.AddField("description", description);
                    txsNode.AddNode(entryNode);
                }
            }




            return result;
        }

        public void SendRawTransaction(string signedTransaction)
        {
            var bytes = Base16.Decode(signedTransaction);
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
