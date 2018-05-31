using System.Linq;
using System.Numerics;
using System.Text;

namespace PhantasmaChain.VM
{
    public struct MachineValue
    {
        private static readonly byte[] Empty = new byte[0] { };

        private byte[] _data;
        public byte[] Data
        {
            get
            {
                return _data != null ? _data : Empty;
            }

            set
            {
                _data = value != null ? value : Empty;
            }
        }

        public BigInteger AsNumber()
        {
            return new BigInteger(Data);
        }

        public string AsString()
        {
            return Encoding.UTF8.GetString(Data);
        }

        public bool AsBool()
        {
            var temp = Data;
            return temp.Length > 0 && (temp.Length > 1 || temp[0] != 0);
        }

        public void SetValue(BigInteger val)
        {
            this.Data = val.ToByteArray();
        }

        public void SetValue(string val)
        {
            this.Data = Encoding.UTF8.GetBytes(val);
        }

        public void SetValue(bool val)
        {
            this.Data = new byte[1] { (byte)(val ? 1 : 0) };
        }

        public override bool Equals(object obj)
        {
            if (!(obj is MachineValue))
            {
                return false;
            }

            var temp = (MachineValue)obj;
            return temp == this;
        }

        public override int GetHashCode()
        {
            return Data.GetHashCode();
        }

        public int Length => Data.Length;
        public bool IsEmpty => Data.Length == 0;

        public static bool operator ==(MachineValue a, MachineValue b)
        {
            return a.Data.SequenceEqual(b.Data);
        }

        public static bool operator !=(MachineValue a, MachineValue b)
        {
            return !(a == b);
        }

    }

}
