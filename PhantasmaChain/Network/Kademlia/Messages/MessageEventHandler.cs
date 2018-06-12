namespace Phantasma.Network.Kademlia.Messages
{
	/// <summary>
	/// A delegate for handling message events.
	/// </summary>
	public delegate void MessageEventHandler<T>(Contact sender, T message) where T : Message;
}
