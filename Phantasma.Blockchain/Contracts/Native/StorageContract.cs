using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using System.Runtime.InteropServices;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct StorageEntry
    {
        public string Name;
        public Hash hash;
    }

    public sealed class StorageContract : SmartContract
    {
        public override string Name => "storage";

        public const int KilobytesPerStake = 40;

        internal StorageMap _storageMap; //<string, Collection<StorageEntry>>
        internal StorageMap _referenceMap; //<string, int>

        public StorageContract() : base()
        {
        }

        public void UploadFile(Address from, string name, int contentSize, byte[] contentMerkle, ArchiveFlags flags, byte[] key)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(contentSize >= Archive.MinSize, "file too small");
            Runtime.Expect(contentSize <= Archive.MaxSize, "file too big");

            int requiredSize = contentSize + Hash.Length + name.Length;

            var usedSize = GetUsedSpace(from);

            var temp = Runtime.CallContext("energy", "GetStake", from);
            var totalStaked = (BigInteger)temp;
            totalStaked /= UnitConversion.GetUnitValue(Nexus.StakingTokenDecimals);
            var availableSize = totalStaked * KilobytesPerStake;

            availableSize -= usedSize;
            Runtime.Expect(availableSize >= requiredSize, "account does not have available space");

            var hashes = MerkleTree.FromBytes(contentMerkle);
            Runtime.Expect(Runtime.Nexus.CreateArchive(hashes, contentSize, flags, key) != null, "archive creation failed");

            var newEntry = new StorageEntry()
            {
                Name = name,
                hash = hashes.Root,
            };

            var list = _storageMap.Get<Address, StorageList>(from);
            list.Add<StorageEntry>(newEntry);

            Runtime.Notify(EventKind.FileCreate, from, name);
        }

        public void DeleteFile(Address from, string name)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");

            var list = _storageMap.Get<Address, StorageList>(from);
            var count = list.Count();
            int targetIndex = -1;
            Hash targetHash = Hash.Null;
            for (int i = 0; i < count; i++)
            {
                var entry = list.Get<StorageEntry>(i);
                if (entry.Name == name)
                {
                    targetIndex = i;
                    targetHash = entry.hash;
                    break;
                }
            }

            var archive = Runtime.Nexus.FindArchive(targetHash);
            Runtime.Expect(Runtime.Nexus.DeleteArchive(archive), "deletion failed");

            Runtime.Expect(targetIndex >= 0, "file not found");
            list.RemoveAt<StorageEntry>(targetIndex);
            Runtime.Notify(EventKind.FileDelete, from, name);
        }

        public BigInteger GetUsedSpace(Address from)
        {
            if (!_storageMap.ContainsKey<Address>(from))
            {
                return 0;
            }

            var list = GetFiles(from);
            BigInteger usedSize = 0;
            var count = list.Length;
            for (int i = 0; i < count; i++)
            {
                var entry = list[i];
                var archive = Runtime.Nexus.FindArchive(entry.hash);
                Runtime.Expect(archive != null, "missing archive");
                usedSize += archive.Size;
                usedSize += entry.Name.Length;
                usedSize += Hash.Length;
            }

            return usedSize;
        }

        public StorageEntry[] GetFiles(Address from)
        {
            Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");
            var list = _storageMap.Get<Address, StorageList>(from);
            return list.All<StorageEntry>();
        }

        public BigInteger CalculateRequiredSize(string name, BigInteger contentSize) => contentSize + Hash.Length + name.Length;
    }
}
