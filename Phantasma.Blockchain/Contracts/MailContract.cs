using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class MailContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Mail;

        internal StorageMap _domainMap; //<string, Address>
        internal StorageMap _userMap; //<Address, string>
        internal StorageMap _domainUsers; //<Address, Collection<StorageEntry>>

        public MailContract() : base()
        {
        }

        public void PushMessage(Address from, Address target, Hash archiveHash)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var ownedDomain = GetUserDomain(from);
            Runtime.Expect(!string.IsNullOrEmpty(ownedDomain), $"{from} not associated with any domain");

            var targetDomain = GetUserDomain(target);
            Runtime.Expect(!string.IsNullOrEmpty(targetDomain), $"{target} not associated with any domain");
            Runtime.Expect(ownedDomain == targetDomain, $"{target} is associated with different domain: {targetDomain}");

            Runtime.CallContext(NativeContractKind.Storage, nameof(StorageContract.AddFile), from, target, archiveHash);
        }

        #region domains
        public bool DomainExists(string domainName)
        {
            return _domainMap.ContainsKey<string>(domainName);
        }

        public void RegisterDomain(Address from, string domainName)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination must be user address");

            Runtime.Expect(!DomainExists(domainName), "domain already exists");

            _domainMap.Set<string, Address>(domainName, from);

            JoinDomain(from, domainName);

            Runtime.Notify(EventKind.DomainCreate, from, domainName);
        }

        public void UnregisterDomain(string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            var from = _domainMap.Get<string, Address>(domainName);

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            _domainMap.Remove<string>(domainName);

            Runtime.Notify(EventKind.DomainDelete, from, domainName);
        }

        public void MigrateDomain(string domainName, Address target)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            var currentDomain = GetUserDomain(target);
            Runtime.Expect(string.IsNullOrEmpty(currentDomain), "already associated with domain: " + currentDomain);

            var from = _domainMap.Get<string, Address>(domainName);

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            _domainMap.Set<string, Address>(domainName, target);

            var users = GetDomainUsers(domainName);
            foreach (var user in users)
            {
                Runtime.CallContext(NativeContractKind.Storage, nameof(StorageContract.MigratePermission), user, from, target);
            }

            Runtime.Notify(EventKind.AddressMigration, from, target);
        }

        public Address[] GetDomainUsers(string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            var list = _domainUsers.Get<string, StorageList>(domainName);
            return list.All<Address>();
        }

        public void JoinDomain(Address from, string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination must be user address");

            var currentDomain = GetUserDomain(from);
            Runtime.Expect(string.IsNullOrEmpty(currentDomain), "already associated with domain: " + currentDomain);

            _userMap.Set<Address, string>(from, domainName);
            var list = _domainUsers.Get<string, StorageList>(domainName);
            list.Add<Address>(from);

            Runtime.Notify(EventKind.AddressRegister, from, domainName);
        }

        public void LeaveDomain(Address from, string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination must be user address");

            var currentDomain = GetUserDomain(from);
            Runtime.Expect(currentDomain == domainName, "not associated with domain: " + domainName);

            _userMap.Remove<Address>(from);

            var list = _domainUsers.Get<string, StorageList>(domainName);
            list.Remove<Address>(from);

            Runtime.Notify(EventKind.AddressUnregister, from, domainName);
        }

        public string GetUserDomain(Address target)
        {
            if (_userMap.ContainsKey<Address>(target))
            {
                return _userMap.Get<Address, string>(target);
            }

            return null;
        }
        #endregion

        /*#region groups
        public void registerGroup(domain, name, storage_size)
        {

        }

        public void unregisterGroup(domain, name)
        {

        }

        public void joinGroup(target, domain, group)
        {

        }

        public void leaveGroup(target, domain, group)
        {

        }

        public void getGroupUsers(target)
        {

        }
        public string[] getDomainGroups(string domainName)
        {

        }



        #endregion*/
    }
}
