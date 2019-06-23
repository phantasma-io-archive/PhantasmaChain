using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;

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

        public const int KilobytesPerStake = 40 * 1024;

        internal StorageMap _storageMap; //<string, Collection<StorageEntry>>
        internal StorageMap _referenceMap; //<string, int>

        public StorageContract() : base()
        {
        }

        public void UploadFile(Address from, string name, int contentSize, byte[] contentMerkle, ArchiveFlags flags)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(contentSize >= Archive.MinSize, "file too small");
            Runtime.Expect(contentSize >= Archive.MaxSize, "file too big");

            int requiredSize = contentSize + Hash.Length + name.Length;

            var list = _storageMap.Get<Address, StorageList>(from);
            int usedSize = 0;
            var count = list.Count();
            for (int i=0; i<count; i++)
            {
                var entry = list.Get<StorageEntry>(i);
                var archive = Runtime.Nexus.FindArchive(entry.hash);
                Runtime.Expect(archive != null, "missing archive");
                usedSize += archive.Size;
            }

            var temp = Runtime.CallContext("energy", "GetStake", from);
            var totalStaked = (BigInteger)temp;
            totalStaked /= UnitConversion.GetUnitValue(Nexus.StakingTokenDecimals);
            var availableSize = totalStaked * KilobytesPerStake;

            availableSize -= usedSize;
            Runtime.Expect(availableSize >= requiredSize, "account does not have available space");

            var hashes = MerkleTree.FromBytes(contentMerkle);
            Runtime.Expect(Runtime.Nexus.CreateArchive(hashes, flags) != null, "archive creation failed");

            var newEntry = new StorageEntry()
            {
                Name = name,
                hash = hashes.Root,
            };

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

            Runtime.Expect(Runtime.Nexus.DeleteArchive(targetHash), "deletion failed");

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

            var list = _storageMap.Get<Address, StorageList>(from);
            int usedSize = 0;
            var count = list.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = list.Get<StorageEntry>(i);
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
    }
}
