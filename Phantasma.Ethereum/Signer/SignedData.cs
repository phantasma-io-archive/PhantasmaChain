using Phantasma.Core.Utils;
using Phantasma.Numerics;
using System.IO;

namespace Phantasma.Ethereum.Signer
{
    public class SignedData
    {
        public byte[][] Data { get; set; }
        public byte[] V { get; set; }
        public byte[] R { get; set; }
        public byte[] S { get; set; }

        public EthECDSASignature GetSignature()
        {
            if (!IsSigned()) return null;
            return EthECDSASignatureFactory.FromComponents(R, S, V);
        }

        public bool IsSigned()
        {
            return (V != null);
        }

        public SignedData()
        {

        }

        public SignedData(byte[][] data, EthECDSASignature signature)
        {
            Data = data;
            if (signature != null)
            {
                R = signature.R;
                S = signature.S;
                V = signature.V;
            }
        }

        /// <summary>
        /// Use to validate signedData that the frontend sends and let the user use the app.
        /// </summary>
        /// <param name="userAddress"></param>
        /// <param name="signedData"></param>
        /// <param name="random"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static bool ValidateSignedDataETHBNB(string ethAddress, string signedData, string random, string data)
        {
            var msgData = Base16.Decode(data);
            var randomBytes = Base16.Decode(random);
            var signedDataBytes = Base16.Decode(signedData);
            var msgBytes = ByteArrayUtils.ConcatBytes(randomBytes, msgData);
            using (var stream = new MemoryStream(signedDataBytes))
            using (var reader = new BinaryReader(stream))
            {
               // var signature = reader.ReadSignature();
               // EthECKey accountSenderRecovered = null;
               // EthECDSASignature sign2 = null;


                //sign2 = new EthECDSASignature(signedDataBytes);
                //accountSenderRecovered = EthECKey.RecoverFromSignature(sign2, System.Text.Encoding.UTF8.GetBytes(ethAddress.Replace("0x", "")));
                //var userPK = accountSenderRecovered.GetPubKey();
                //var userAddr = Address.FromBytes(userPK);
                //
                //return signature.Verify(msgBytes, userAddr);
            }

            return false;
        }
    }
}