namespace Phantasma.Network.Kademlia
{
    public abstract class Peer
    {
        public abstract KeyValueMessage GetValue(KeyMessage request);
        public abstract KeyMessage RemoveValue(KeyMessage request);
        public abstract KeyValueMessage StoreValue(KeyValueMessage request);
    }
}
