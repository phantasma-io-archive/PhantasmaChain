using System.Threading.Tasks;

namespace Phantasma.RpcClient.Client
{
    public interface IClient
    {
        Task<T> SendRequestAsync<T>(RpcRequest request, string route = null);
        Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList);
        Task SendRequestAsync(RpcRequest request, string route = null);
        Task SendRequestAsync(string method, string route = null, params object[] paramList);
    }
}
