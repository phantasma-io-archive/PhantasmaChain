using Phantasma.Blockchain;
using LunarLabs.Parser;
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
