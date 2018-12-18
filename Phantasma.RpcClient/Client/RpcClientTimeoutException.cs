using System;

namespace Phantasma.RpcClient.Client
{
    public class RpcClientTimeoutException : Exception
    {
        public RpcClientTimeoutException(string message) : base(message)
        {
        }

        public RpcClientTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}