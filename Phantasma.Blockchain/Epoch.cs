using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Ring;
using Phantasma.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Blockchain
{
    public class Epoch
    {
        public static readonly uint DurationInSeconds = 60;
        public static readonly uint SlashLimitInSeconds = 5;

        public readonly Hash PreviousHash;
        public readonly Timestamp StartTime;
        public readonly Timestamp EndTime;
        public readonly Address Validator;
        public IEnumerable<Signature> Signatures => _signatures;
        public IEnumerable<Hash> BlockHashes => _blockHashes;

        public Hash Hash { get; private set; }

        private List<RingSignature> _signatures = new List<RingSignature>();
        private HashSet<Hash> _blockHashes = new HashSet<Hash>();

        public bool WasSlashed => IsSlashed(EndTime);

        public Epoch(Timestamp time, Address validator, Hash previousHash)
        {
            this.Validator = validator;
            this.StartTime = time;
            this.EndTime = StartTime;
            this.PreviousHash = previousHash;
        }

        public override string ToString()
        {
            return $"{Hash}";
        }

        public void AddSignature(RingSignature signature)
        {
            _signatures.Add(signature);
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

            if (!validatorSet.Contains(this.Validator))
            {
                return false;
            }

            var msg = this.ToByteArray(false);

            foreach (var sig in _signatures)
            {
                if (!sig.Verify(msg, validatorSet))
                {
                    return false;
                }
            }

            // TODO currently O(N^2), optimize this or we will have to make sure that number of validators per epoch is always a small number... 
            var validatorArray = knownValidators.ToArray();
            for (int i = 0; i < validatorArray.Length; i++)
            {
                for (int j = 0; j < validatorArray.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    if (_signatures[j].IsLinked(_signatures[i]))
                    {
                        return false;
                    }
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
            writer.WriteAddress(Validator);
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
            foreach (var sig in _signatures)
            {
                writer.WriteSignature(sig);
            }
        }

        public static Epoch Unserialize(BinaryReader reader)
        {
            var validator = reader.ReadAddress();
            var prevHash = reader.ReadHash();
            var startTime = (Timestamp)reader.ReadUInt32();

            var epoch = new Epoch(startTime, validator, prevHash);

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
                    // TODO sig should always be not-null in most cases, but add error handling later
                    if (reader.ReadSignature() is RingSignature sig)
                    {
                        epoch.AddSignature(sig);
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
