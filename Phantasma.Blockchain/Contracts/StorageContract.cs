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
        public const string FreeStoragePerContractTag = "storage.contract.kb";

        public const int DefaultForeignSpacedPercent = 20;

        public const int MaxKeySize = 256;

        internal StorageMap _storageMap; //<string, Collection<StorageEntry>>
        internal StorageMap _dataQuotas; //<Address, BigInteger>

        public StorageContract() : base()
        {
        }

        public BigInteger CalculateStorageSizeForStake(BigInteger stakeAmount)
        {
            var kilobytesPerStake = (int)Runtime.GetGovernanceValue(StorageContract.KilobytesPerStakeTag);
            var totalSize = stakeAmount * kilobytesPerStake * 1024;
            totalSize /= UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

            return totalSize;
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

            var usedQuota = _dataQuotas.Get<Address, BigInteger>(from);
            usedSize += usedQuota;

            return usedSize;
        }

        public BigInteger GetAvailableSpace(Address from)
        {
            var stakedAmount = Runtime.GetStake(from);
            var totalSize = CalculateStorageSizeForStake(stakedAmount);

            if (from.IsSystem)
            {
                totalSize += Runtime.GetGovernanceValue(FreeStoragePerContractTag);
            }

            var usedSize = GetUsedSpace(from);
            Runtime.Expect(usedSize <= totalSize, "error in storage size calculation");
            return totalSize - usedSize;
        }

        public StorageEntry[] GetFiles(Address from)
        {
            //Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");
            var list = _storageMap.Get<Address, StorageList>(from);
            return list.All<StorageEntry>();
        }

        private void ValidateKey(byte[] key)
        {
            Runtime.Expect(key.Length > 0 && key.Length <= MaxKeySize, "invalid key");

            var firstChar = (char)key[0];
            Runtime.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here
        }

        public void WriteData(Address target, byte[] key, byte[] value)
        {
            ValidateKey(key);

            var writeSize = value.Length + key.Length;

            var availableSize = GetAvailableSpace(target);
            Runtime.Expect(availableSize >= writeSize, $"not enough storage space available");

            Runtime.Storage.Put(key, value);

            var usedQuota = _dataQuotas.Get<Address, BigInteger>(target);
            usedQuota += writeSize;
            _dataQuotas.Set<Address, BigInteger>(target, usedQuota);

            var temp = Runtime.Storage.Get(key);
            Runtime.Expect(temp.Length == value.Length, "storage write corruption");
        }

        public void DeleteData(Address target, byte[] key)
        {
            ValidateKey(key);

            Runtime.Expect(Runtime.Storage.Has(key), "key does not exist");

            var value = Runtime.Storage.Get(key);
            var deleteSize = value.Length + key.Length;

            Runtime.Storage.Delete(key);

            var usedQuota = _dataQuotas.Get<Address, BigInteger>(target);
            usedQuota -= deleteSize;
            _dataQuotas.Set<Address, BigInteger>(target, usedQuota);

            Runtime.Expect(usedQuota >= 0, "storage delete corruption");
        }

        public static BigInteger CalculateRequiredSize(string fileName, BigInteger contentSize) => contentSize + Hash.Length + fileName.Length;
    }
}
