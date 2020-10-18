using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    public struct StorageEntry
    {
        public string Name;
        public Hash Hash;
        public Timestamp Date;
    }

    public sealed class StorageContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Storage;

        public const string KilobytesPerStakeTag = "storage.stake.kb";

        public const int DefaultForeignSpacedPercent = 20;

        internal StorageMap _storageMap; //<string, Collection<StorageEntry>>

        public StorageContract() : base()
        {
        }

        public BigInteger CalculateStorageSizeForStake(BigInteger stakeAmount)
        {
            var kilobytesPerStake = (int)Runtime.GetGovernanceValue(StorageContract.KilobytesPerStakeTag);
            var availableSize = stakeAmount * kilobytesPerStake * 1024;
            availableSize /= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

            return availableSize;
        }

        // this is an helper method to upload smaller files...
        public void UploadData(Address target, string fileName, byte[] data, ArchiveFlags flags, byte[] key)
        {
            BigInteger fileSize = data.Length;
            Runtime.Expect(fileSize <= MerkleTree.ChunkSize, "data too big");

            var merkle = new MerkleTree(data);
            var serializedMerkle = Serialization.Serialize(merkle);
            UploadFile(target, fileName, fileSize, serializedMerkle, flags, key);

            var archive = Runtime.CreateArchive(merkle, fileSize, flags, key);
            Runtime.Expect(archive != null, "failed to create archive");

            Runtime.Expect(Runtime.WriteArchive(archive, 0, data), "failed to write archive content");
        }

        public void UploadFile(Address target, string fileName, BigInteger fileSize, byte[] contentMerkle, ArchiveFlags flags, byte[] key)
        {
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
            Runtime.Expect(target.IsUser, "destination address must be user address");
            Runtime.Expect(fileSize >= DomainSettings.ArchiveMinSize, "file too small");
            Runtime.Expect(fileSize <= DomainSettings.ArchiveMaxSize, "file too big");

            BigInteger requiredSize = CalculateRequiredSize(fileName, fileSize);

            var targetUsedSize = GetUsedSpace(target);
            var targetStakedAmount = Runtime.GetStake(target);
            var targetAvailableSize = CalculateStorageSizeForStake(targetStakedAmount);
            targetAvailableSize -= targetUsedSize;

            Runtime.Expect(targetAvailableSize >= requiredSize, "target account does not have available space");

            var hashes = MerkleTree.FromBytes(contentMerkle);
            Runtime.CreateArchive(hashes, fileSize, flags, key);

            var newEntry = new StorageEntry()
            {
                Name = fileName,
                Hash = hashes.Root,
                Date = Runtime.Time,
            };

            var list = _storageMap.Get<Address, StorageList>(target);
            list.Add<StorageEntry>(newEntry);

            Runtime.Notify(EventKind.FileCreate, target, newEntry);
        }

        public void DeleteFile(Address from, string name)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            //Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");

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
                    targetHash = entry.Hash;
                    break;
                }
            }

            Runtime.Expect(targetIndex >= 0, "file not found");

            Runtime.Expect(Runtime.DeleteArchive(targetHash), "deletion failed");

            list.RemoveAt<StorageEntry>(targetIndex);
            Runtime.Notify(EventKind.FileDelete, from, name);
        }

        public BigInteger GetUsedSpace(Address from)
        {
            //if (!_storageMap.ContainsKey<Address>(from))
            //{
            //    return 0;
            //}

            var list = GetFiles(from);
            BigInteger usedSize = 0;
            var count = list.Length;
            for (int i = 0; i < count; i++)
            {
                var entry = list[i];
                var archive = Runtime.GetArchive(entry.Hash);
                Runtime.Expect(archive != null, "missing archive");
                usedSize += archive.Size;
                usedSize += entry.Name.Length;
                usedSize += Hash.Length;
            }

            return usedSize;
        }

        public StorageEntry[] GetFiles(Address from)
        {
            //Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");
            var list = _storageMap.Get<Address, StorageList>(from);
            return list.All<StorageEntry>();
        }
        public static BigInteger CalculateRequiredSize(string fileName, BigInteger contentSize) => contentSize + Hash.Length + fileName.Length;
    }
}
