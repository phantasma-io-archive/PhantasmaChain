using System.Numerics;
using Phantasma.Blockchain.Storage;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class StorageContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Storage;

        public const string KilobytesPerStakeTag = "storage.stake.kb";
        public const string FreeStoragePerContractTag = "storage.contract.kb";

        public const int DefaultForeignSpacedPercent = 20;

        public const int MaxKeySize = 256;

        internal StorageMap _storageMap; //<Address, Collection<StorageEntry>>
        internal StorageMap _permissionMap; //<Address, Collection<StorageEntry>>
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

        public void CreateFile(Address target, string fileName, BigInteger fileSize, byte[] contentMerkle, byte[] encryptionContent)
        {
            Runtime.Expect(Runtime.IsWitness(target), "invalid witness");
            Runtime.Expect(target.IsUser, "destination address must be user address");
            Runtime.Expect(fileSize >= DomainSettings.ArchiveMinSize, "file too small");
            Runtime.Expect(fileSize <= DomainSettings.ArchiveMaxSize, "file too big");

            var merkleTree = MerkleTree.FromBytes(contentMerkle);
            var archive = Runtime.GetArchive(merkleTree.Root);

            if (archive != null && archive.IsOwner(target))
            {
                return;
            }

            if (archive == null)
            {
                var encryption = ArchiveExtensions.ReadArchiveEncryption(encryptionContent);
                archive = Runtime.CreateArchive(merkleTree, target, fileName, fileSize, Runtime.Time, encryption);
            }

            AddFile(target, target, archive);
        }

        public bool HasFile(Address target, Hash hash)
        {
            var archive = Runtime.GetArchive(hash);
            return archive.IsOwner(target);
        }

        public void AddFile(Address from, Address target, Hash archiveHash)
        {
            var archive = Runtime.GetArchive(archiveHash);
            AddFile(from, target, archive);
        }

        private void AddFile(Address from, Address target, IArchive archive)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(HasPermission(from, target), $"permissions missing for {from} to add file to {target}");

            Runtime.Expect(target.IsUser, "destination address must be user address");

            Runtime.Expect(archive != null, "archive does not exist");


            BigInteger requiredSize = archive.Size;

            var targetUsedSize = GetUsedSpace(target);
            var targetStakedAmount = Runtime.GetStake(target);
            var targetAvailableSize = CalculateStorageSizeForStake(targetStakedAmount);
            targetAvailableSize -= targetUsedSize;

            Runtime.Expect(targetAvailableSize >= requiredSize, "target account does not have available space");

            if (!archive.IsOwner(target))
            {
                Runtime.AddOwnerToArchive(archive.Hash, target);
            }

            var list = _storageMap.Get<Address, StorageList>(target);
            list.Add<Hash>(archive.Hash);
        }

        public void DeleteFile(Address from, Hash targetHash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var list = _storageMap.Get<Address, StorageList>(from);

            int targetIndex = -1;
            var count = list.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = list.Get<Hash>(i);
                if (entry == targetHash)
                {
                    targetIndex = i;
                    break;
                }
            }

            Runtime.Expect(targetIndex >= 0, "archive not found");

            Runtime.Expect(Runtime.RemoveOwnerFromArchive(targetHash, from), "owner removal failed");
            list.RemoveAt(targetIndex);
        }

        // Checks if external address has permission to add files to target address
        public bool HasPermission(Address external, Address target)
        {
            if (external == target)
            {
                return true;
            }

            var permissions = _permissionMap.Get<Address, StorageList>(target);
            return permissions.Contains<Address>(external);
        }

        public void AddPermission(Address from, Address externalAddr)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from != externalAddr, "target must be different");

            var permissions = _permissionMap.Get<Address, StorageList>(from);
            Runtime.Expect(!permissions.Contains<Address>(externalAddr), $"permission already exists");

            permissions.Add<Address>(externalAddr);

            Runtime.Notify(EventKind.AddressLink, from, externalAddr);
        }

        public void DeletePermission(Address from, Address externalAddr)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(from != externalAddr, "target must be different");

            var permissions = _permissionMap.Get<Address, StorageList>(from);
            Runtime.Expect(permissions.Contains<Address>(externalAddr), $"permission does not exist");

            permissions.Remove<Address>(externalAddr);

            Runtime.Notify(EventKind.AddressUnlink, from, externalAddr);
        }

        public void MigratePermission(Address target, Address oldAddr, Address newAddr)
        {
            Runtime.Expect(Runtime.IsWitness(oldAddr), "invalid witness");

            var permissions = _permissionMap.Get<Address, StorageList>(target);

            if (target != oldAddr)
            {
                Runtime.Expect(HasPermission(oldAddr, target), $"not permissions from {oldAddr} for target {target}");
                permissions.Remove<Address>(oldAddr);
                Runtime.Notify(EventKind.AddressUnlink, target, oldAddr);
            }

            if (newAddr != target)
            {
                Runtime.Expect(!HasPermission(newAddr, target), $"{newAddr} already has permissions for target {target}");
                permissions.Add<Address>(newAddr);
                Runtime.Notify(EventKind.AddressLink, target, newAddr);
            }
        }

        public void Migrate(Address from, Address target)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == "account", "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(!_dataQuotas.ContainsKey<Address>(target), "target address already in use");
            _dataQuotas.Migrate<Address, BigInteger>(from, target);

            _permissionMap.Migrate<Address, StorageList>(from, target);
            _storageMap.Migrate<Address, StorageList>(from, target);
        }

        public BigInteger GetUsedSpace(Address from)
        {
            //if (!_storageMap.ContainsKey<Address>(from))
            //{
            //    return 0;
            //}

            var hashes = GetFiles(from);
            BigInteger usedSize = 0;
            var count = hashes.Length;
            for (int i = 0; i < count; i++)
            {
                var hash = hashes[i];
                var archive = Runtime.GetArchive(hash);
                Runtime.Expect(archive != null, "missing archive");
                usedSize += archive.Size;
            }

            var usedQuota = GetUsedDataQuota(from);
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

        public Hash[] GetFiles(Address from)
        {
            //Runtime.Expect(_storageMap.ContainsKey<Address>(from), "no files available for address");
            var list = _storageMap.Get<Address, StorageList>(from);
            return list.All<Hash>();
        }

        private void ValidateKey(byte[] key)
        {
            Runtime.Expect(key.Length > 0 && key.Length <= MaxKeySize, "invalid key");

            var firstChar = (char)key[0];
            Runtime.Expect(firstChar != '.', "permission denied"); // NOTE link correct PEPE here
        }

        public BigInteger GetUsedDataQuota(Address address)
        {
            var result = _dataQuotas.Get<Address, BigInteger>(address);
            return result;
        }

        public void WriteData(Address target, byte[] key, byte[] value)
        {
            ValidateKey(key);

            var usedQuota = _dataQuotas.Get<Address, BigInteger>(target);

            BigInteger deleteSize = 0;
            if (Runtime.Storage.Has(key))
            {
                var oldData = Runtime.Storage.Get(key);
                deleteSize = oldData.Length;
            }

            if (Runtime.ProtocolVersion >= 4)
            {
                var writeSize = value.Length;
                if (writeSize > deleteSize)
                {
                    var diff = writeSize - deleteSize;
                    var availableSize = GetAvailableSpace(target);
                    Runtime.Expect(availableSize >= diff, $"not enough storage space available: requires " + diff + ", only have: " + availableSize);
                }

                Runtime.Storage.Put(key, value);

                usedQuota -= deleteSize;
                usedQuota += writeSize;

                if (usedQuota <= 0)
                {
                    usedQuota = writeSize; // fix for data written in previous protocol
                }

                _dataQuotas.Set<Address, BigInteger>(target, usedQuota);

                _dataQuotas.Set<Address, BigInteger>(target, usedQuota);
            }
            else
            {
                Runtime.Storage.Put(key, value);
            }


            var temp = Runtime.Storage.Get(key);
            Runtime.Expect(temp.Length == value.Length, "storage write corruption");
        }

        public void DeleteData(Address target, byte[] key)
        {
            ValidateKey(key);

            Runtime.Expect(Runtime.Storage.Has(key), "key does not exist");

            var value = Runtime.Storage.Get(key);
            var deleteSize = value.Length;

            Runtime.Storage.Delete(key);

            if (Runtime.ProtocolVersion >= 4)
            {
                var usedQuota = _dataQuotas.Get<Address, BigInteger>(target);
                usedQuota -= deleteSize;

                if (usedQuota < 0)
                {
                    usedQuota = 0;
                }

                _dataQuotas.Set<Address, BigInteger>(target, usedQuota);
            }
        }

        public static BigInteger CalculateRequiredSize(string fileName, BigInteger contentSize) => contentSize + Hash.Length + fileName.Length;
    }
}
