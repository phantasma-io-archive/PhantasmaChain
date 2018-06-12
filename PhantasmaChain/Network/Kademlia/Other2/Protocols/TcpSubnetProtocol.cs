using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;

using Phantasma.Kademlia.Common;

namespace Phantasma.Kademlia.Protocols
{
    // ==========================

    public class TcpSubnetProtocol : IProtocol
    {
#if DEBUG       // for unit tests
        public bool Responds { get; set; }
#endif

        // For serialization:
        public string Url { get { return url; } set { url = value; } }
        public int Port { get { return port; } set { port = value; } }
        public int Subnet { get { return subnet; } set { subnet = value; } }

        protected string url;
        protected int port;
        protected int subnet;

        /// <summary>
        /// For serialization.
        /// </summary>
        public TcpSubnetProtocol()
        {
        }

        public TcpSubnetProtocol(string url, int port, int subnet)
        {
            this.url = url;
            this.port = port;
            this.subnet = subnet;

#if DEBUG
            Responds = true;
#endif
        }

        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID key)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindNodeResponse, ErrorResponse>(url + ":" + port + "//FindNode",
                new FindNodeRequest()
                {
                    Protocol = sender.Protocol,
                    ProtocolName = sender.Protocol.GetType().Name,
                    Subnet = subnet,
                    Sender = sender.ID.Value,
                    Key = key.Value,
                    RandomID = id.Value
                }, out error, out timeoutError);

            try
            {
                var contacts = ret?.Contacts?.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList();

                // Return only contacts with supported protocols.
                return (contacts?.Where(c => c.Protocol != null).ToList() ?? EmptyContactList(), GetRpcError(id, ret, timeoutError, error));
            }
            catch (Exception ex)
            {
                return (null, new RpcError() { ProtocolError = true, ProtocolErrorMessage = ex.Message });
            }
        }

        /// <summary>
        /// Attempt to find the value in the peer network.
        /// </summary>
        /// <returns>A null contact list is acceptable here as it is a valid return if the value is found.
        /// The caller is responsible for checking the timeoutError flag to make sure null contacts is not
        /// the result of a timeout error.</returns>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID key)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindValueResponse, ErrorResponse>(url + ":" + port + "//FindValue",
                new FindValueRequest()
                {
                    Protocol = sender.Protocol,
                    ProtocolName = sender.Protocol.GetType().Name,
                    Subnet = subnet,
                    Sender = sender.ID.Value,
                    Key = key.Value,
                    RandomID = id.Value
                }, out error, out timeoutError);

            try
            {
                var contacts = ret?.Contacts?.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList();

                // Return only contacts with supported protocols.
                return (contacts?.Where(c => c.Protocol != null).ToList(), ret.Value, GetRpcError(id, ret, timeoutError, error));
            }
            catch (Exception ex)
            {
                return (null, null, new RpcError() { ProtocolError = true, ProtocolErrorMessage = ex.Message });
            }
        }

        public RpcError Ping(Contact sender)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindValueResponse, ErrorResponse>(url + ":" + port + "//Ping",
                new PingRequest()
                {
                    Protocol = sender.Protocol,
                    ProtocolName = sender.Protocol.GetType().Name,
                    Subnet = subnet,
                    Sender = sender.ID.Value,
                    RandomID = id.Value
                }, 
                out error, out timeoutError);

            return GetRpcError(id, ret, timeoutError, error);
        }

        public RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            ErrorResponse error;
            ID id = ID.RandomID;
            bool timeoutError;

            var ret = RestCall.Post<FindValueResponse, ErrorResponse>(url + ":" + port + "//Store",
                    new StoreRequest()
                    {
                        Protocol = sender.Protocol,
                        ProtocolName = sender.Protocol.GetType().Name,
                        Subnet = subnet,
                        Sender = sender.ID.Value,
                        Key = key.Value,
                        Value = val,
                        IsCached = isCached,
                        ExpirationTimeSec = expirationTimeSec,
                        RandomID = id.Value
                    }, 
                    out error, out timeoutError);

            return GetRpcError(id, ret, timeoutError, error);
        }

        protected RpcError GetRpcError(ID id, BaseResponse resp, bool timeoutError, ErrorResponse peerError)
        {
            return new RpcError() { IDMismatchError = id != resp.RandomID, TimeoutError = timeoutError, PeerError = peerError != null, PeerErrorMessage = peerError?.ErrorMessage };
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }
    }
}
