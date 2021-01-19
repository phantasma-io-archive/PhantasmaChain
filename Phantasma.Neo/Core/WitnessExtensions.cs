using Neo.Network.P2P.Payloads;

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
    }
}
