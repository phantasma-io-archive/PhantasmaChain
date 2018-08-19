using System;

namespace Phantasma.VM.Types
{
    public struct Timestamp : IInteropObject
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

        public int GetSize()
        {
            return sizeof(uint);
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
