namespace Phantasma.Network
{
    public abstract class Peer
    {
        public Endpoint Endpoint { get; protected set; }

        public Status Status { get; protected set; }

        public abstract void Send(Message msg);
        public abstract Message Receive();
    }
}
