namespace Phantasma.Network
{
	/// <summary>
	/// Represents a generic network message
	/// </summary>
	public abstract class Message
	{
        public Opcode Opcode { get; private set; }
        public byte[] PublicKey { get; private set; }
        public byte[] Signature { get; private set; }

        public bool IsSigned() {
            throw new System.NotImplementedException();
        }
    }

    public struct DeliveredMessage {
        public Message message;
        public Endpoint source;
    }
}
