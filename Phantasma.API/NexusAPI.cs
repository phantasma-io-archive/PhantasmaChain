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
            var result = DataNode.CreateObject("account");

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
                    entryNode.AddNode(chainNode);
                }
            }

            return result;
        }

        /*public DataNode GetBlock(Hash hash)
        {

        }

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
