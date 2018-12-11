using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Phantasma.IO;

namespace Phantasma.Network.P2P
{
    public struct Endpoint
    {
        public string Host { get { return EndPoint.Address.ToString(); } }
        public int Port { get { return EndPoint.Port; } }

        internal readonly IPEndPoint EndPoint;

        internal Endpoint(IPEndPoint ipEndPoint)
        {
            EndPoint = ipEndPoint;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Endpoint))
            {
                return false;
            }
            return EndPoint.Equals(((Endpoint)obj).EndPoint);
        }

        public override string ToString()
        {
            return EndPoint.ToString();
        }

        public override int GetHashCode()
        {
            return EndPoint.GetHashCode();
        }

        public Endpoint(string hostStr, int port)
        {
            IPAddress ipAddress;

            if (!IPAddress.TryParse(hostStr, out ipAddress))
            {
                if (Socket.OSSupportsIPv6)
                {
                    if (hostStr == "localhost")
                    {
                        ipAddress = IPAddress.IPv6Loopback;
                    }
                    else
                    {
                        ipAddress = ResolveAddress(hostStr, AddressFamily.InterNetworkV6);
                    }
                }
                if (ipAddress == null)
                {
                    ipAddress = ResolveAddress(hostStr, AddressFamily.InterNetwork);
                }
            }

            if (ipAddress == null)
            {
                throw new Exception("Invalid address: " + hostStr);
            }

            EndPoint = new IPEndPoint(ipAddress, port);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.WriteShortString(this.Host);
            writer.Write(Port);
        }

        public static Endpoint Unserialize(BinaryReader reader)
        {
            var host = reader.ReadShortString();
            var port = reader.ReadInt32();
            return new Endpoint(host, port);
        }

        private static IPAddress ResolveAddress(string hostStr, AddressFamily addressFamily)
        {
#if NETCORE
            var hostTask = Dns.GetHostEntryAsync(Dns.GetHostName());
            hostTask.Wait();
            var host = hostTask.Result;
#else
            var host = Dns.GetHostEntry(Dns.GetHostName());
#endif
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == addressFamily)
                {
                    return ip;
                }
            }
            return null;
        }
    }
}
