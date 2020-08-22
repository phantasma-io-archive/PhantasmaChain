using System.Numerics;
using System.Threading.Tasks;

namespace Phantasma.Ethereum.Signer
{
#if !DOTNET35
    public interface IEthExternalSigner
    {
        bool CalculatesV { get; }
        ExternalSignerTransactionFormat ExternalSignerTransactionFormat { get; }
        Task<string> GetAddressAsync();
        Task<EthECDSASignature> SignAsync(byte[] rawBytes);
        Task<EthECDSASignature> SignAsync(byte[] rawBytes, BigInteger chainId);
        Task SignAsync(Transaction transaction);
        Task SignAsync(TransactionChainId transaction);
    }
#endif
}