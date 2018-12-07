using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.EdDSA;
using Phantasma.IO;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Blockchain
{
    public struct EpochSigner
    {
        public readonly Address address;
        public readonly Ed25519Signature signature;

        public EpochSigner(Address address, Ed25519Signature signature)
        {
            this.address = address;
            this.signature = signature;
        }
    }

    public class Epoch
    {
        public static readonly uint DurationInSeconds = 60;
        public static readonly uint SlashLimitInSeconds = 5;

        public readonly uint Index;
        public readonly Hash PreviousHash;
        public readonly Timestamp StartTime;
        public readonly Timestamp EndTime;
        public readonly Address ValidatorAddress;
        public IEnumerable<Address> Signers => _signatures.Keys;
        public IEnumerable<Hash> BlockHashes => _blockHashes;

        public Hash Hash { get; private set; }

        private Dictionary<Address, Ed25519Signature> _signatures = new Dictionary<Address, Ed25519Signature>();
        private HashSet<Hash> _blockHashes = new HashSet<Hash>();

        public bool WasSlashed => IsSlashed(EndTime);

        public Epoch(uint index, Timestamp time, Address validator, Hash previousHash)
        {
            this.Index = index;
            this.ValidatorAddress = validator;
            this.StartTime = time;
            this.EndTime = StartTime;
            this.PreviousHash = previousHash;
        }

        public override string ToString()
        {
            return $"{Hash}";
        }

        public void AddSigner(Address address, Ed25519Signature signature)
        {
            _signatures[address] = signature;
        }

        public void AddBlockHash(Hash hash)
        {
            _blockHashes.Add(hash);
        }

        public bool Validate(IEnumerable<Address> knownValidators)
        {
            if (_blockHashes.Count <= 0)
            {
                return false;
            }

            var validatorSet = new HashSet<Address>(knownValidators);
            if (_signatures.Count > validatorSet.Count)
            {
                return false;
            }

            // check for majority signatures
            var requiredSignatureCount = 1 + validatorSet.Count / 2;
            if (_signatures.Count < requiredSignatureCount)
            {
                return false;
            }

            if (!validatorSet.Contains(this.ValidatorAddress))
            {
                return false;
            }

            var msg = this.ToByteArray(false);

            foreach (var signer in _signatures)
            {
                if (!signer.Value.Verify(msg, signer.Key))
                {
                    return false;
                }
            }

            return true;
        }

        public byte[] ToByteArray(bool withSignature)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer, withSignature);
                }

                return stream.ToArray();
            }
        }

        private void Serialize(BinaryWriter writer, bool withSignature)
        {
            writer.Write(Index);
            writer.WriteAddress(ValidatorAddress);
            writer.WriteHash(PreviousHash);
            writer.Write(StartTime.Value);

            int blockCount = _blockHashes.Count;
            writer.WriteVarInt(blockCount);
            foreach (var hash in _blockHashes)
            {
                writer.WriteHash(hash);
            }

            if (!withSignature)
            {
                return;
            }

            int sigCount = _signatures.Count;
            writer.WriteVarInt(sigCount);
            foreach (var entry in _signatures)
            {
                writer.WriteAddress(entry.Key);
                writer.WriteSignature(entry.Value);
            }
        }

        public static Epoch Unserialize(BinaryReader reader)
        {
            var index = reader.ReadUInt32();
            var validator = reader.ReadAddress();
            var prevHash = reader.ReadHash();
            var startTime = (Timestamp)reader.ReadUInt32();

            var epoch = new Epoch(index, startTime, validator, prevHash);

            var blockCount = reader.ReadVarInt();
            while (blockCount > 0)
            {
                var hash = reader.ReadHash();
                epoch.AddBlockHash(hash);
            }

            // check if we have some signatures attached
            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var signatureCount = (int)reader.ReadVarInt();
                for (int i = 0; i < signatureCount; i++)
                {
                    var address = reader.ReadAddress();
                    // TODO sig should always be not-null in most cases, but add error handling later
                    if (reader.ReadSignature() is Ed25519Signature sig)
                    {
                        epoch.AddSigner(address, sig);
                    }
                }
            }

            epoch.UpdateHash();
            return epoch;
        }

        public static Epoch Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

        internal void UpdateHash()
        {
            var data = this.ToByteArray(false);
            var hash = CryptoExtensions.Sha256(data);
            this.Hash = new Hash(hash);
        }

        public bool IsSlashed(Timestamp time)
        {
            return ((time - StartTime) - DurationInSeconds) >= SlashLimitInSeconds;
        }
    }
}
