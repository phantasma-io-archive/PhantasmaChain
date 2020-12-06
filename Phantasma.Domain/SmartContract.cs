using System.Text;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Core;

namespace Phantasma.Domain
{
    public abstract class SmartContract : IContract
    {
        public const int SecondsInDay = 86400;

        public const string ConstructorName = "Initialize";

        public ContractInterface ABI { get; protected set; }
        public abstract string Name { get; }

        public BigInteger Order { get; internal set; } // TODO remove this?

        private readonly Dictionary<byte[], byte[]> _storage = new Dictionary<byte[], byte[]>(new ByteArrayComparer()); // TODO remove this?

        public IRuntime Runtime { get; protected set; }

        private Address _address;
        public Address Address
        {
            get
            {
                if (_address.IsNull)
                {
                   _address = GetAddressForName(Name);
                }

                return _address;
            }
        }

        public SmartContract()
        {
            this.Order = 0;
            _address = Address.Null;
        }

        public static Address GetAddressForNative(NativeContractKind kind)
        {
            return GetAddressForName(kind.GetContractName());
        }

        public static Address GetAddressForName(string name)
        {
            return Address.FromHash(name);
        }

        public static byte[] GetKeyForField(NativeContractKind nativeContract, string fieldName, bool isProtected)
        {
            return GetKeyForField(nativeContract.GetContractName(), fieldName, isProtected);
        }

        public static byte[] GetKeyForField(string contractName, string fieldName, bool isProtected)
        {
            Throw.If(string.IsNullOrEmpty(contractName), "invalid contract name");
            Throw.If(string.IsNullOrEmpty(fieldName), "invalid field name");

            string prefix = isProtected ? "." : "";

            return Encoding.UTF8.GetBytes($"{prefix}{contractName}.{fieldName}");
        }


    }
}
