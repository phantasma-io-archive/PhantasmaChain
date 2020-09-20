using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Native
{
    public struct StorageEntry
    {
        public string Name;
        public Hash Hash;
        public Address Creator;
        public Timestamp Date;
    }

    public sealed class StorageContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Storage;

        public const string KilobytesPerStakeTag = "storage.stake.kb";

        public const int DefaultForeignSpacedPercent = 20;

        internal StorageMap _storageMap; //<string, Collection<StorageEntry>>
        internal StorageMap _foreignMap; //<Address, BigInt>

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
        public void UploadData(Address sender, Address target, string fileName, byte[] data, ArchiveFlags flags, byte[] key)
        {
            BigInteger fileSize = data.Length;
            Runtime.Expect(fileSize <= MerkleTree.ChunkSize, "data too big");

            var merkle = new MerkleTree(data);
            var serializedMerkle = Serialization.Serialize(merkle);
            UploadFile(sender, target, fileName, fileSize, serializedMerkle, flags, key);

            var archive = Runtime.CreateArchive(sender, target, merkle, fileSize, flags, key);
            Runtime.Expect(archive != null, "failed to create archive");

            Runtime.Expect(Runtime.WriteArchive(archive, 0, data), "failed to write archive content");
        }

        public void UploadFile(Address sender, Address target, string fileName, BigInteger fileSize, byte[] contentMerkle, ArchiveFlags flags, byte[] key)
        {
            Runtime.Expect(Runtime.IsWitness(sender), "invalid witness");
            Runtime.Expect(target.IsUser, "destination address must be user address");
            Runtime.Expect(fileSize >= DomainSettings.ArchiveMinSize, "file too small");
            Runtime.Expect(fileSize <= DomainSettings.ArchiveMaxSize, "file too big");

            BigInteger requiredSize = CalculateRequiredSize(fileName, fileSize);

            var targetUsedSize = GetUsedSpace(target);
            var targetStakedAmount = Runtime.GetStake(target);
            var targetAvailableSize = CalculateStorageSizeForStake(targetStakedAmount);
            targetAvailableSize -= targetUsedSize;

            if (sender == target)
            {
                Runtime.Expect(targetAvailableSize >= requiredSize, "target account does not have available space");
            }
            else // otherwise we need to run some extra checks in case the sender is not the target
            {
                var foreignSpace = GetForeignSpace(target);
                if (foreignSpace < targetAvailableSize) 
                {
                    targetAvailableSize = foreignSpace; // limit available space to max allocated space to foreign addresses
                }

                Runtime.Expect(targetAvailableSize >= requiredSize, "target account does not have available space");

                if (sender.IsUser)
                {
                    // note that here we require sender to have at least free space equal to msg size, mostly a protection against spam
                    var senderUsedSize = GetUsedSpace(sender);
                    var senderStakedAmount = Runtime.GetStake(sender);
                    var senderAvailableSize = CalculateStorageSizeForStake(senderStakedAmount);
                    senderAvailableSize -= senderUsedSize;
                    Runtime.Expect(senderAvailableSize >= requiredSize, "sender account does not have available space");
                }
                else
                {
                    Runtime.Expect(sender.IsSystem, "invalid address type for sender");
                }
            }

            var hashes = MerkleTree.FromBytes(contentMerkle);
            Runtime.CreateArchive(sender, target, hashes, fileSize, flags, key);

            var newEntry = new StorageEntry()
            {
                Name = fileName,
                Hash = hashes.Root,
                Creator = sender,
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

        public BigInteger GetForeignSpace(Address target)
        {
            BigInteger result;

            if (_foreignMap.ContainsKey<Address>(target))
            {
                result = _foreignMap.Get<Address, BigInteger>(target);
            }
            else
            {
                var targetStakedAmount = Runtime.GetStake(target);
                var targetAvailableSize = CalculateStorageSizeForStake(targetStakedAmount);

                result = (targetAvailableSize * DefaultForeignSpacedPercent) / 100;
            }

            if (result < 0)
            {
                result = 0;
            }

            return result;
        }

        public void SetForeignSpace(Address from, BigInteger percent)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination address must be user address");
            Runtime.Expect(percent >= 0, "percent too small");
            Runtime.Expect(percent <= 100, "percent too big");

            var exists = _foreignMap.ContainsKey<Address>(from);

            _foreignMap.Set<Address, BigInteger>(from, percent);

            Runtime.Notify(exists ? EventKind.ValueUpdate : EventKind.ValueCreate, from, new ChainValueEventData() { Name = $"{from.Text}.storage.foreign", Value = percent});
        }

        public static BigInteger CalculateRequiredSize(string fileName, BigInteger contentSize) => contentSize + Hash.Length + fileName.Length;
    }
}
