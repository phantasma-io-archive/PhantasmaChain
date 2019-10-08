using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using System.Runtime.InteropServices;

namespace Phantasma.Contracts.Native
{
    public struct StorageEntry
    {
        public string Name;
        public Hash hash;
    }

    public sealed class StorageContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Storage;

        public const string KilobytesPerStakeTag = "storage.stake.kb";

        internal StorageMap _storageMap; //<string, Collection<StorageEntry>>
        internal StorageMap _referenceMap; //<string, int>

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

        public void UploadFile(Address from, string name, BigInteger contentSize, byte[] contentMerkle, ArchiveFlags flags, byte[] key)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "address must be user address");
            Runtime.Expect(contentSize >= DomainSettings.ArchiveMinSize, "file too small");
            Runtime.Expect(contentSize <= DomainSettings.ArchiveMaxSize, "file too big");

            BigInteger requiredSize = CalculateRequiredSize(name, contentSize);

            var usedSize = GetUsedSpace(from);

            var stakedAmount = Runtime.GetStake(from);
            var availableSize = CalculateStorageSizeForStake(stakedAmount);
           
            availableSize -= usedSize;
            Runtime.Expect(availableSize >= requiredSize, "account does not have available space");

            var hashes = MerkleTree.FromBytes(contentMerkle);
            Runtime.CreateArchive(from, hashes, contentSize, flags, key);

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
                    targetHash = entry.hash;
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
                var archive = Runtime.GetArchive(entry.hash);
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

        public static BigInteger CalculateRequiredSize(string name, BigInteger contentSize) => contentSize + Hash.Length + name.Length;
    }
}
