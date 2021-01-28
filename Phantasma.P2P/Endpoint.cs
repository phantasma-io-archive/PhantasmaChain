using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Network.P2P
{
    public enum PeerProtocol
    {
        Unknown,
        TCP,        
    }

    public struct Endpoint
    {
        public readonly PeerProtocol Protocol;
        public readonly string Host;
        public readonly int Port;

        public Endpoint(PeerProtocol protocol, string host, int port)
        {
            this.Protocol = protocol;
            this.Host = host;
            this.Port = port;
        }

        public static Endpoint FromString(string str)
        {
            var temp = str.Split(':');

            if (temp.Length != 3)
            {
                throw new NodeException("Invalid endpoint format: " + str);
            }

            PeerProtocol protocol;
            if (!Enum.TryParse<PeerProtocol>(temp[0], true, out protocol))
            {
                throw new NodeException("Invalid endpoint protocol: " + str);
            }

            uint port;
            if (!uint.TryParse(temp[2], out port))
            {
                throw new NodeException("Invalid endpoint port: " + str);
            }

            var host = temp[1];
            if (string.IsNullOrEmpty(host))
            {
                throw new NodeException("Invalid endpoint host: " + str);
            }

            return new Endpoint(protocol, host, (int)port);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Endpoint))
            {
                return false;
            }

            var other = (Endpoint)obj;
            return this.Host == other.Host && this.Protocol == other.Protocol && this.Port == other.Port ;
        }

        public override string ToString()
        {
            return $"{Protocol}:{Host}:{Port}";
        }

        // TODO use all fields for hashcode
        public override int GetHashCode()
        {
            return Host.GetHashCode();
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)this.Protocol);
            writer.WriteVarString(this.Host);
            writer.Write(Port);
        }

        public static Endpoint Unserialize(BinaryReader reader)
        {
            var protocol = (PeerProtocol)reader.ReadByte();
            var host = reader.ReadVarString();
            var port = reader.ReadInt32();
            return new Endpoint(protocol, host, port);
        }

        public static IPAddress ResolveAddress(string hostStr, AddressFamily addressFamily)
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
