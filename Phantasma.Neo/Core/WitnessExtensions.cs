using System.Collections.Generic;
using Neo.Network.P2P.Payloads;
using Phantasma.Storage;
using Neo.Wallets;
using System.Linq;
using Neo;
using Neo.Cryptography;

namespace Phantasma.Neo.Core
{
    public static class WitnessExtensions
    {
        public static Phantasma.Cryptography.Address ExtractAddress(this Witness witness)
        {
            if (witness.VerificationScript.Length == 0)
            {
                return Phantasma.Cryptography.Address.Null;
            }
            var bytes = new byte[34];
            bytes[0] = (byte)Phantasma.Cryptography.AddressKind.User;
            Phantasma.Core.Utils.ByteArrayUtils.CopyBytes(witness.VerificationScript, 1, bytes, 1, 33);
            return Phantasma.Cryptography.Address.FromBytes(bytes);
        }

        public static void Sign(this Transaction tx, NeoKeys key, IEnumerable<Witness> witnesses = null)
        {
            var txdata = tx.Serialize();

            var witList = new List<Witness>();

            if (key != null)
            {
                var privkey = key.PrivateKey;
                var keyPair = new KeyPair(privkey);
                tx.Sign(keyPair);

                //var invocationScript = new byte[] { (byte)OpCode.PUSHBYTES64}.Concat(signature).ToArray();
                //var verificationScript = key.signatureScript;
                //witList.Add(new Witness() { InvocationScript = invocationScript, VerificationScript = verificationScript });
            }

            if (witnesses != null)
            {
                foreach (var entry in witnesses)
                {
                    witList.Add(entry);
                }

                var newWitnesses = new Witness[witnesses.ToArray().Length + tx.Witnesses.Length];
                tx.Witnesses.CopyTo(newWitnesses, 0);
                witnesses.ToArray().CopyTo(newWitnesses, tx.Witnesses.Length);

                tx.Witnesses = newWitnesses;
            }

        }
    }
}
