using System;
using System.Collections.Generic;
using Phantasma.Utils;

namespace Phantasma.Network.Kademlia
{
	/// <summary>
	/// This is the class you use to use the library.
	/// You can put and get values.
	/// It is responsible for bootstraping the local node and connecting to the appropriate overlay.
	/// It also registers us with the overlay.
	/// </summary>
	public class DHT
	{
		private const int MAX_SIZE = 8 * 1024; // 8K is big
		
		private KademliaNode dhtNode;
		
		/// <summary>
		/// Create a DHT using the given master server, and specify whether to publish our IP.
		/// PRECONDITION: Create one per app or you will have a node ID collision.
		/// </summary>
        /// <param name="dhtNode">The KademliaNode that is used to communicate using the protocol</param>
        /// <param name="alreadyBootstrapped">Checks if the node have or not to bootstrap</param>
        /// <param name="btpNode">The node to bootstrap with (can be leaved null)</param>
		public DHT(Endpoint bootstrapNode, KademliaNode dhtNode = null, bool alreadyBootstrapped = false)
		{
            if (dhtNode != null)
            {
                this.dhtNode = dhtNode;
            }
            else
            {
                dhtNode = new KademliaNode();
            }
            if (!alreadyBootstrapped)
            {
                /*if (btpNode == "")
                {
                    int ourPort = dhtNode.GetPort();
                    Log.Message("We are on UDP port " + ourPort.ToString());

                    Log.Message("Getting bootstrap list...");

                    AppSettingsReader asr = new AppSettingsReader();
                    
                    XDocument xmlDoc = XDocument.Load((string)asr.GetValue("KademliaNodesFile", typeof(string)));

                    List<Endpoint> nodes = new List<Endpoint>(from node in xmlDoc.Descendants("Node")
                                select new Endpoint("soap.udp://" + node.Element("Host").Value + ":" + node.Element("Port").Value + "/kademlia"));

                    foreach (var node in nodes)
                    {
                        if (dhtNode.AsyncBootstrap(nodes))
                        {
                            Log.Debug("OK!");
                        }
                        else
                        {
                            Log.Debug("Failed.");
                        }
                    }
                }
                else*/
                {
                    try
                    {
                        Log.Debug("Bootstrapping with " + bootstrapNode);
                        if (dhtNode.Bootstrap(bootstrapNode))
                        {
                            Log.Debug("OK!");
                        }
                        else
                        {
                            Log.Debug("Failed.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Bad entry!", ex);
                    }
                }
            }
            else
            {
                Log.Message("Self Bootstrapping");
                dhtNode.Bootstrap();
            }
			// Join the network officially
			Log.Message("Trying to join network....");
			if(dhtNode.JoinNetwork()) {
                Log.Message("Online");
			} else {
				Log.Warning("Unable to connect to Kademlia overlay!\n"
				                   + "Check that nodes list has accessible nodes.");
			}
		}
		
		/// <summary>
		/// Retrieve a value from the DHT.
		/// </summary>
		/// <param name="key">The key of the value to retrieve.</param>
		/// <returns>the best semantically matching value stored for the key, or null if no values are found</returns>
		public KademliaResource Get(ID key)
		{
            return dhtNode.Get(key);
		}
			
		/// <summary>
		/// Puts a value in the DHT under a key.
		/// </summary>
        /// <param name="filename">The filename of resource to store into the network</param>
		public void Put(IEnumerable<byte> data)
		{
			dhtNode.Put(data);
		}
		
		/// <summary>
		/// Returns the maximum size of individual puts.
		/// </summary>
		/// <returns>the maximum size of individual puts</returns>
		public int MaxSize()
		{
			return MAX_SIZE;
		}
	}
}
