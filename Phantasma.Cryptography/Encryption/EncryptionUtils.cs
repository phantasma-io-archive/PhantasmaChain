using Phantasma.Core.Utils;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Phantasma.Cryptography.Encryption
{
    public class EncryptionUtils
    {
        /// <summary>
        /// Use to validate signedData that the frontend sends and let the user use the app.
        /// </summary>
        /// <param name="userAddress">Phantasma Address</param>
        /// <param name="signedData"></param>
        /// <param name="random"></param>
        /// <param name="data">Raw data</param>
        /// <returns></returns>
        public static bool ValidateSignedData(string userAddress, string signedData, string random, string data)
        {
            var msgData = Base16.Decode(data);
            var randomBytes = Base16.Decode(random);
            var signedDataBytes = Base16.Decode(signedData);
            var msgBytes = ByteArrayUtils.ConcatBytes(randomBytes, msgData);
            using (var stream = new MemoryStream(signedDataBytes))
            using (var reader = new BinaryReader(stream))
            {
                var signature = reader.ReadSignature();
                return signature.Verify(msgBytes, Address.FromText(userAddress));
            }

            return false;
        }
    }
}
