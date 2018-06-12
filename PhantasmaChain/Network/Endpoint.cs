namespace Phantasma.Network
{
    public struct Endpoint
    {
        public readonly string Host;
        public readonly int Port;

        public Endpoint(string host, int port)
        {
            this.Host = host;
            this.Port = port;
        }

        public override bool Equals(object obj)
        {
            return (Endpoint)obj == this;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override string ToString()
        {
            return $"{Host}:{Port}";
        }

        public static bool operator ==(Endpoint obj1, Endpoint obj2)
        {
            return (obj1.Host == obj2.Host && obj1.Port == obj2.Port);
        }

        public static bool operator !=(Endpoint obj1, Endpoint obj2)
        {
            return (obj1.Host != obj2.Host || obj1.Port != obj2.Port);
        }

    }
}
