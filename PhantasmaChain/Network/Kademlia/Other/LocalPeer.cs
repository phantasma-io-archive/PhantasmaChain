using Phantasma.Utils;

namespace Phantasma.Network.Kademlia
{
    public class LocalPeer: Peer
    {
        private readonly NodeStore nodeStore;

        public LocalPeer(NodeStore store)
        {
            this.nodeStore = store;
        }

        public override KeyValueMessage GetValue(KeyMessage request)
        {
            if (!this.nodeStore.ContainsKey(request.Key))
            {
                ThrowRpcException("Key not found");
            }

            var value = this.nodeStore.GetValue(request.Key);
            var response = new KeyValueMessage()
            {
                Key = request.Key,
                Value = value
            };

            return response;
        }

        public override KeyMessage RemoveValue(KeyMessage request)
        {
            var removed = this.nodeStore.RemoveValue(request.Key);

            if (!removed)
            {
                ThrowRpcException("Key not found");
            }

            var response = new KeyMessage()
            {
                Key = request.Key
            };

            return response;
        }

        public override KeyValueMessage StoreValue(KeyValueMessage request)
        {
            try
            {
                var added = this.nodeStore.AddValue(request.Key, request.Value);

                if (!added)
                {
                    ThrowRpcException("Couldn't store value.");
                }
            }
            catch 
            {
                ThrowRpcException("Duplicate key found.");
            }

            return request;
        }

        private void ThrowRpcException(string message)
        {
            Log.Error("RPC error: "+message);
        }
    }
}
