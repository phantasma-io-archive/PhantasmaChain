using System;

namespace Phantasma.Core.Types
{
    public struct Timestamp : IComparable<Timestamp>
    {
        public readonly uint Value;

        public Timestamp(uint value)
        {
            this.Value = value;
        }

        public override string ToString()
        {
            return ((DateTime)this).ToString();
        }

        public static Timestamp Now => DateTime.UtcNow;
        public readonly static Timestamp Null = new Timestamp(0);

        public int CompareTo(Timestamp other)
        {
            if (other.Value.Equals(this.Value))
            {
                return 0;
            }

            if (this.Value < other.Value)
            {
                return -1;
            }

            return 1;
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

        public int GetSize()
        {
            return 4; // sizeof(uint);
        }

        public static bool operator ==(Timestamp A, Timestamp B) { return A.Value == B.Value; }

        public static bool operator !=(Timestamp A, Timestamp B) { return !(A.Value == B.Value); }

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
            if (time.Kind != DateTimeKind.Utc)
            {
                time = time.ToUniversalTime();
            }

            var ticks = (uint)(time - unixEpoch).TotalSeconds;
            return new Timestamp(ticks);
        }

        public static implicit operator DateTime(Timestamp timestamp)
        {
            return unixEpoch.AddSeconds(timestamp.Value);
        }

        public static uint operator +(Timestamp A, TimeSpan B) { return A.Value + (uint)B.TotalSeconds; }
        public static uint operator -(Timestamp A, TimeSpan B) { return A.Value - (uint)B.TotalSeconds; }

    }
}
