using System;
using System.Linq;
using System.Numerics;

namespace Phantasma.Contracts
{
    public abstract class Contract : IContract
    {
        public void Expect(bool assertion)
        {
            throw new NotImplementedException();
        }

        public byte[] PublicKey { get; }

        public IRuntime Runtime;
    }

    public struct Address
    {
        public byte[] PublicKey { get; private set; }

        public Address(byte[] publicKey)
        {
            this.PublicKey = publicKey;
        }

        public static bool operator ==(Address A, Address B) { return A.PublicKey.SequenceEqual(B.PublicKey); }

        public static bool operator !=(Address A, Address B) { return !A.PublicKey.SequenceEqual(B.PublicKey); }
    }

    public interface Surprise<T>
    {
        T Value { get; }
        Timestamp Timestamp { get; }
        bool Hidden { get; }
    }

    public interface Map<Key, Value>
    {
        void Put(Key key, Value val);
        Value Get(Key key);
        bool Remove(Key key);
        bool Contains(Key key);
        void Clear();
        void Iterate(Action<Key, Value> visitor);

        BigInteger Count { get; }
    }

    public struct Timestamp
    {
        public readonly long Value;

        public Timestamp(long value)
        {
            this.Value = value;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Timestamp))
            {
                return false;
            }

            var timestamp = (Timestamp)obj;
            return Value == timestamp.Value;
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        public static bool operator ==(Timestamp A, Timestamp B) { return A.Value == B.Value; }

        public static bool operator !=(Timestamp A, Timestamp B) { return A.Value != B.Value; }

        public static bool operator <(Timestamp A, Timestamp B) { return A.Value < B.Value; }

        public static bool operator >(Timestamp A, Timestamp B) { return A.Value > B.Value; }

        public static bool operator <=(Timestamp A, Timestamp B) { return A.Value <= B.Value; }

        public static bool operator >=(Timestamp A, Timestamp B) { return A.Value >= B.Value; }
    }

}
