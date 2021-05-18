using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain
{
    public class Organization : IOrganization
    {
        public string ID { get; private set; }
        public string Name { get; private set; }

        private Address _address;
        public Address Address
        {
            get
            {
                if (_address.IsNull)
                {
                    _address = Address.FromHash(ID);
                }

                return _address;
            }
        }

        public byte[] Script { get; private set; }

        public BigInteger Size => GetMemberList().Count();

        private StorageContext Storage;

        public Organization(string name, StorageContext storage)
        {
            this.Storage = storage;
 
            this.ID = name;

            var key = GetKey("script");
            if (Storage.Has(key))
            {
                this.Script = Storage.Get(key);
            }
            else
            {
                this.Script = null;
            }

            key = GetKey("name");
            if (Storage.Has(key))
            {
                this.Name = Storage.Get<string>(key);
            }
            else
            {
                this.Name = null;
            }
        }

        public void Init(string name, byte[] script)
        {
            this.Name = name;
            var key = GetKey("name");
            this.Storage.Put(key, name);

            this.Script = script;
            key = GetKey("script");
            this.Storage.Put(key, script);
        }

        private byte[] GetKey(string key)
        {
            return System.Text.Encoding.UTF8.GetBytes($".org.{ID}.{key}");
        }

        private StorageList GetMemberList()
        {
            var key = GetKey("list");
            return new StorageList(key, this.Storage);
        }

        public Address[] GetMembers()
        {
            var list = GetMemberList();
            return list.All<Address>();
        }

        public bool IsMember(Address address)
        {
            var list = GetMemberList();
            return list.Contains<Address>(address);
        }

        public bool AddMember(RuntimeVM Runtime, Address from, Address target)
        {
            if (from.IsSystem)
            {
                Runtime.Expect(from != this.Address, "can't add organization as member of itself");
            }

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var list = GetMemberList();

            if (list.Contains<Address>(target))
            {
                return false;
            }

            list.Add<Address>(target);

            Runtime.Notify(EventKind.OrganizationAdd, from, new OrganizationEventData(this.ID, target));
            return true;
        }

        public bool RemoveMember(RuntimeVM Runtime, Address from, Address target)
        {
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var list = GetMemberList();

            if (!list.Contains<Address>(target))
            {
                return false;
            }

            list.Remove<Address>(target);

            Runtime.Notify(EventKind.OrganizationRemove, from, new OrganizationEventData(this.ID, target));
            return true;
        }

        public bool IsWitness(Transaction transaction)
        {
            var size = this.Size;
            if (size < 1)
            {
                return false;
            }

            var majorityCount = (size / 2) + 1;
            if (transaction == null || transaction.Signatures.Length < majorityCount)
            {
                return false;
            }

            int witnessCount = 0;

            var members = new List<Address>(this.GetMembers());
            var msg = transaction.ToByteArray(false);

            foreach (var sig in transaction.Signatures)
            {
                if (witnessCount >= majorityCount)
                {
                    break; // dont waste time if we already reached a majority
                }

                //ring signature not supported yet here
                if (sig.Kind == SignatureKind.Ring)
                {
                    continue;
                }

                foreach (var addr in members)
                {
                    if (sig.Verify(msg, addr))
                    {
                        witnessCount++;
                        members.Remove(addr);
                        break;
                    }
                }
            }

            return witnessCount >= majorityCount;
        }

        public bool MigrateMember(RuntimeVM Runtime, Address admin, Address from, Address to)
        {
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            if (to.IsSystem)
            {
                Runtime.Expect(to!= this.Address, "can't add organization as member of itself");
            }

            var list = GetMemberList();

            if (!list.Contains<Address>(from))
            {
                return false;
            }

            Runtime.Expect(!list.Contains<Address>(to), "target address is already a member of organization");

            list.Remove<Address>(from);
            list.Add<Address>(to);         

            Runtime.Notify(EventKind.OrganizationRemove, admin, new OrganizationEventData(this.ID, from));
            Runtime.Notify(EventKind.OrganizationAdd, admin, new OrganizationEventData(this.ID, to));

            return true;
        }
    }
}
