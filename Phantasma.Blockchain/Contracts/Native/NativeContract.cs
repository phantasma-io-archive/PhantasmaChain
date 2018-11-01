using System.Text;

using Phantasma.VM.Contracts;
using Phantasma.VM;
using Phantasma.Cryptography;
using System;
using Phantasma.Core;

namespace Phantasma.Blockchain.Contracts.Native
{
    public abstract class NativeContract : SmartContract
    {
        private Address _address;

        public NativeContract() : base()
        {
            var type = this.GetType();

            var bytes = Encoding.ASCII.GetBytes(type.Name);
            var hash = CryptoExtensions.Sha256(bytes);
            _address = new Address(hash);
        }
    }
}
