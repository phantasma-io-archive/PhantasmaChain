using System;

namespace Phantasma.Contracts.Types
{
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
         
        public static bool operator >=(Timestamp A, Timestamp B) { return A.Value >= B.Value;}
    }

    public static class TimestampExtensions
    {
        public static Timestamp ToTimestamp(this DateTime value)
        {
            long val = (value.Ticks - 621355968000000000) / 10000000;
            return new Timestamp(val);
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);

        // Unix timestamp is seconds past epoch
        public static DateTime ToDateTime(this long unixTimeStamp)
        {
            return epoch.AddSeconds(unixTimeStamp).ToUniversalTime();
        }
    }

}
