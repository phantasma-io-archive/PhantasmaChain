using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Phantasma.VM.Contracts
{
    public struct Address
    {
        public byte[] PublicKey { get; private set; }

        public Address(byte[] publicKey)
        {
            this.PublicKey = publicKey;
        }

        public static bool operator ==(Address A, Address B) { return A.PublicKey.SequenceEqual(B.PublicKey); }

        public static bool operator !=(Address A, Address B) { return !A.PublicKey.SequenceEqual(B.PublicKey); }

        public override bool Equals(object obj)
        {
            if (!(obj is Address))
            {
                return false;
            }

            var address = (Address)obj;
            return EqualityComparer<byte[]>.Default.Equals(PublicKey, address.PublicKey);
        }

        public override int GetHashCode()
        {
            return PublicKey.GetHashCode();
        }
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
        public readonly uint Value;

        public Timestamp(uint value)
        {
            this.Value = value;
        }

        public static Timestamp Now => DateTime.UtcNow;

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

        public static uint operator -(Timestamp A, Timestamp B) { return A.Value - B.Value; }

        public static implicit operator Timestamp(uint ticks)
        {
            return new Timestamp(ticks);
        }

        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static implicit operator Timestamp(DateTime time)
        {
            var ticks = (uint)(time.ToUniversalTime() - unixEpoch).TotalSeconds;
            return new Timestamp(ticks);
        }

        public static implicit operator DateTime(Timestamp timestamp)
        {
            return unixEpoch.AddSeconds(timestamp.Value);
        }
    }

}

