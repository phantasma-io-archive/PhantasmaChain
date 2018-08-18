using System.Numerics;
using Phantasma.Blockchain;

namespace Phantasma.Utils
{
    public static class TokenUtils
    {
        public static BigInteger GetTokenBalance(this Chain chain, byte[] tokenPublicKey, byte[] accountPublicKey)
        {
            var contract = chain.FindContract(tokenPublicKey);

            if (contract == null)
            {
                return 0;
            }

            var balance = (BigInteger)VMUtils.InvokeScript(contract.Script, new object[] { "balanceOf", accountPublicKey });
            return balance;
        }
    }
}
