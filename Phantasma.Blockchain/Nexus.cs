using System.Collections.Generic;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Log;
using Phantasma.Cryptography;

namespace Phantasma.Blockchain
{
    public class Nexus
    {
        public Chain RootChain { get; private set; }
        public Token NativeToken { get; private set; }

        private Dictionary<string, Chain> _chains = new Dictionary<string, Chain>();
        private Dictionary<string, Token> _tokens = new Dictionary<string, Token>();

        public Nexus(KeyPair owner, Logger logger = null)
        {
            this.RootChain = new Chain(owner, new NexusContract(), logger, null);
        }
    }
}
