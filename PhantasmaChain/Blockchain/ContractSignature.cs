using Phantasma.Cryptography;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Phantasma.Blockchain
{
    public class ContractSignature : Signature
    {
        private SmartContract contract;
        private bool isValid;

        public ContractSignature(SmartContract contract, byte[] signatureKey)
        {
            this.contract = contract;

            var hash = new Hash(signatureKey);
            this.isValid = hash == contract.SignatureHash;
        }

        public override SignatureKind Kind => SignatureKind.Contract;

        // those types of signatures cannot be serialized, they are only for internal VM usage!
        public override void Serialize(BinaryWriter writer)
        {
            throw new InvalidOperationException();
        }

        public override bool Verify(byte[] message, IEnumerable<Address> addresses)
        {
            if (!isValid)
            {
                return false;
            }

            return addresses.Contains(this.contract.Address);
        }
    }
}
