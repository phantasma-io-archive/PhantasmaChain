using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM;

namespace Phantasma.Domain
{
    public interface IRuntime
    {
        IBlock GetBlockByHash(IChain chain, Hash hash);
        IBlock GetBlockByHeight(IChain chain, BigInteger height);

        ITransaction GetTransaction(IChain chain, Hash hash);

        IToken GetToken(string symbol);
        IFeed GetFeed(string name);
        IPlatform GetPlatform(string name);

        IChain GetChainByAddress(Address address);
        IChain GetChainByName(string name);

        void Log(string description);
        void Throw(string description);
        void Expect(bool condition, string description);
        void Notify(EventKind kind, Address address, VMObject content);
        VMObject CallContext(string contextName, string methodName, params object[] args);

        IEvent GetTransactionEvents(ITransaction transaction);

        BigInteger GetTokenPrice(string symbol);
        BigInteger GetTokenQuote(string baseSymbol, string quoteSymbol, BigInteger amount);
        BigInteger GetRandomNumber();
        BigInteger GetGovernanceValue(string name);
        BigInteger GetBalance(IChain chain, IToken token, Address address);
    }
}
